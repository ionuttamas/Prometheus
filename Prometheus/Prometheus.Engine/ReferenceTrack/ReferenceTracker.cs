using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine.ReferenceTrack {
    internal class ReferenceProver
    {
        private readonly ReferenceTracker referenceTracker;

        public ReferenceProver(ReferenceTracker referenceTracker)
        {
            this.referenceTracker = referenceTracker;
        }

        /// <summary>
        /// Checks whether two identifiers can possibly have the same value within a thread schedule.
        /// TODO: this does not consider the possible conditional cases for assigning a variable within loops ("for", "while" loops).
        /// TODO: this does not consider assignments like: "order = person.Orders.First()" and "order = instance" where "instance" and "person.Orders.First()" can be the same
        /// </summary>
        public bool HaveCommonValue(IdentifierNameSyntax first, IdentifierNameSyntax second, out SyntaxNode commonNode) {
            var firstAssignment = new ConditionalAssignment {
                Reference = first,
                AssignmentLocation = first.GetLocation()
            };
            var secondAssignment = new ConditionalAssignment {
                Reference = second,
                AssignmentLocation = second.GetLocation()
            };
            return HaveCommonValueInternal(firstAssignment, secondAssignment, out commonNode);
        }

        private bool HaveCommonValueInternal(ConditionalAssignment first, ConditionalAssignment second, out SyntaxNode commonNode) {
            commonNode = null;

            //TODO: need to check scoping: if "first" is a local variable => it cannot match a variable from another function/thread
            var firstAssignments = referenceTracker.GetAssignments((IdentifierNameSyntax)first.Reference); //todo: this needs checking
            var secondAssignments = referenceTracker.GetAssignments((IdentifierNameSyntax)second.Reference);

            foreach (ConditionalAssignment assignment in firstAssignments) {
                assignment.Conditions.AddRange(first.Conditions);
            }

            foreach (ConditionalAssignment assignment in secondAssignments) {
                assignment.Conditions.AddRange(second.Conditions);
            }

            foreach (ConditionalAssignment firstAssignment in firstAssignments) {
                foreach (ConditionalAssignment secondAssignment in secondAssignments) {
                    if (ValidateReachability(firstAssignment, secondAssignment, out commonNode))
                        return true;
                }
            }

            return false;
        }

        private bool ValidateReachability(ConditionalAssignment first, ConditionalAssignment second, out SyntaxNode commonNode) {
            commonNode = null;

            if (!IsSatisfiable(first, second))
                return false;

            if (AreEquivalent(first.Reference, second.Reference)) {
                commonNode = first.Reference;
                return true;
            }

            var firstReferenceAssignment = new ConditionalAssignment {
                Reference = first.Reference,
                AssignmentLocation = first.AssignmentLocation,
                Conditions = first.Conditions
            };
            var secondReferenceAssignment = new ConditionalAssignment {
                Reference = second.Reference,
                AssignmentLocation = second.AssignmentLocation,
                Conditions = second.Conditions
            };

            if (HaveCommonValueInternal(firstReferenceAssignment, second, out commonNode))
                return true;

            return HaveCommonValueInternal(first, secondReferenceAssignment, out commonNode);
        }

        /// <summary>
        /// This checks whether two nodes are the same reference (the shared memory of two threads).
        /// This can be a class field/property used by both thread functions or parameters passed to threads that are the same
        /// TODO: currently this checks only for field equivalence
        /// </summary>
        private static bool AreEquivalent(SyntaxNode first, SyntaxNode second) {
            if (first.ToString() != second.ToString())
                return false;

            if (first.GetLocation() != second.GetLocation())
                return false;

            return true;
        }

        private static bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second) {
            return true;
        }
    }

    //TODO: this will be used for checking that 2 lock object or 2 instances under concurrency modifications are the same or not
    internal class ReferenceTracker
    {
        private readonly Solution solution;
        private readonly ThreadSchedule threadSchedule;

        public ReferenceTracker(Solution solution, ThreadSchedule threadSchedule)
        {
            this.solution = solution;
            this.threadSchedule = threadSchedule;
        }

        /// <summary>
        /// Returns a list of conditional assignments that the identifier referenced and based on what conditions.
        /// The assignments will be the last references that the identifier took its reference from.
        /// <example>
        /// In the example below calling the <see cref="GetAssignments(IdentifierNameSyntax)"/> for the <paramref name="instance"/> parameter
        /// will return a list of 2 <typeparamref name="ConditionalAssignment"/> with {Condition: "person.Age > 20", Reference: "person"} and {Condition: Not("person.Age > 20"), Reference: "new Person()"}.
        /// <code>
        /// class TestClass
        /// {
        ///     public Person DecrementAge(Person person)
        ///     {
        ///         Person instance = null;
        ///
        ///         if(person.Age > 20)
        ///         {
        ///             instance = person;
        ///         }
        ///         else
        ///         {
        ///             instance = new Person();
        ///         }
        ///
        ///         instance.Age--;
        ///
        ///         return instance;
        ///     }
        /// }
        /// </code>
        /// </example>
        /// </summary>
        public List<ConditionalAssignment> GetAssignments(IdentifierNameSyntax identifier)
        {
            if (!threadSchedule.GetThreadPath(solution, identifier.GetLocation()).Invocations.Any())
                return new List<ConditionalAssignment>();

            //TODO: this does not take into account previous assignments to parameters
            var identifierName = identifier.Identifier.Text;
            var method = identifier.GetLocation().GetContainingMethod();
            var classDeclaration = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var matchingField = classDeclaration
                .DescendantNodes<FieldDeclarationSyntax>()
                .FirstOrDefault(x => x.Declaration.Variables.Any(v => v.Identifier.Text == identifierName));

            if (matchingField == null)
            {
                var methodParameterIndex = method.ParameterList.Parameters.IndexOf(x => x.Identifier.Text == identifierName);

                //The assignment is not from a method parameter, so we search for the assignment inside
                return methodParameterIndex < 0
                    ? GetMethodAssignments(identifier)
                    : GetMethodCallAssignments(identifier, method, methodParameterIndex);
            }

            return GetConstructorAssignments(identifier, classDeclaration);
        }

        /// <summary>
        /// Gets the assignments made from various calls to the given method along the binding parameter.
        /// </summary>
        private List<ConditionalAssignment> GetMethodCallAssignments(IdentifierNameSyntax identifier, MethodDeclarationSyntax method, int parameterIndex)
        {
            //TODO: this does not take into account if a parameter was assigned to another value before being assigned again
            var methodCallAssignments = solution
                .FindReferenceLocations(method)
                .Where(x =>
                {
                    var threadPath = threadSchedule.GetThreadPath(solution, x.Location);
                    return threadPath != null && threadPath.Invocations.Any();
                })
                .Select(x => x.GetNode<InvocationExpressionSyntax>())
                .Select(x => GetConditionalAssignment(x, x.ArgumentList.Arguments[parameterIndex]))
                .ToList();
            var withinMethodAssignments = GetMethodAssignments(identifier);
            var result = new List<ConditionalAssignment>();

            if (withinMethodAssignments.Any())
            {
                foreach (var assignment in methodCallAssignments) {
                    foreach (ConditionalAssignment withinMethodAssignment in withinMethodAssignments)
                    {
                        var clonedAssignment = assignment.Clone();
                        clonedAssignment.Conditions.AddRange(withinMethodAssignment.Conditions);
                        result.Add(clonedAssignment);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the assignments made from the initializing the constructor in any thread path
        /// The assignment comes from a field/property of the current class.
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="classDeclaration"></param>
        /// <returns></returns>
        private List<ConditionalAssignment> GetConstructorAssignments(IdentifierNameSyntax identifier, ClassDeclarationSyntax classDeclaration)
        {
            //TODO: we need to track exactly how the field gets initialized in the constructor; for now we enforce an argument with the same name in the constructor
            //TODO: we dont't know if the field was reasigned in another place (other than the constructor) before assigned to the field
            var constructorParameterIndex = identifier
                .GetLocation()
                .GetContainingConstructor()
                .ParameterList
                .Parameters
                .IndexOf(x => x.Identifier.Text == identifier.Identifier.Text);
            var constructorAssignments = FindObjectCreations(classDeclaration)
                .Where(x => {
                    var threadPath = threadSchedule.GetThreadPath(solution, x.GetLocation());
                    return threadPath != null && threadPath.Invocations.Any();
                })
                .Select(x => GetConditionalAssignment(x, x.ArgumentList.Arguments[constructorParameterIndex]))
                .ToList();

            return constructorAssignments;
        }

        /// <summary>
        /// Returns the list of assignments made for that identifier within the method in which the identifier is used.
        /// </summary>
        private List<ConditionalAssignment> GetMethodAssignments(IdentifierNameSyntax identifier)
        {
            var method = identifier.GetLocation().GetContainingMethod();
            var identifierName = identifier.Identifier.Text;
            var assignments = method
                .DescendantNodes<AssignmentExpressionSyntax>()
                .Where(x=>x.Kind() == SyntaxKind.SimpleAssignmentExpression)
                .Where(x=>x.Left.ToString() == identifierName);
            var conditionalAssignments = assignments.Select(GetConditionalAssignment).ToList();

            return conditionalAssignments;
        }

        /// <summary>
        /// Gets the conditional assignment for the given assignment within its method.
        /// </summary>
        private ConditionalAssignment GetConditionalAssignment(AssignmentExpressionSyntax assignment)
        {
            var elseClause = assignment.FirstAncestor<ElseClauseSyntax>();
            var ifClause = assignment.FirstAncestor<IfStatementSyntax>();
            var conditionalAssignment = new ConditionalAssignment
            {
                Reference = assignment.Right,
                AssignmentLocation = assignment.GetLocation()
            };
            SyntaxNode currentNode = assignment;

            var classDeclaration = assignment.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var matchingField = classDeclaration
                .DescendantNodes<FieldDeclarationSyntax>()
                .FirstOrDefault(x => x.Declaration.Variables.Any(v => v.Identifier.Text == assignment.Right.ToString()));

            if (matchingField != null)
            {
                //If the assignment is a shared class field/property, we overwrite the reference; otherwise we track its assignments
                conditionalAssignment.Reference = matchingField.Declaration.Variables[0];
            }

            while (currentNode != null) {
                if (ifClause!=null && !ifClause.Contains(elseClause))
                {
                    conditionalAssignment.Conditions.AddRange(ProcessIfStatement(currentNode, out currentNode).Conditions);
                }
                else if (elseClause != null)
                {
                    conditionalAssignment.Conditions.AddRange(ProcessElseStatement(currentNode, out currentNode).Conditions);
                } else {
                    return conditionalAssignment;
                }

                elseClause = currentNode.FirstAncestor<ElseClauseSyntax>();
                ifClause = currentNode.FirstAncestor<IfStatementSyntax>();
            }

            return conditionalAssignment;
        }

        /// <summary>
        /// Gets the conditional assignment for the given invocation or object creation within its method.
        /// </summary>
        private ConditionalAssignment GetConditionalAssignment(SyntaxNode bindingNode, SyntaxNode argument) {
            //TODO: check if we can merge this method and the one above
            var elseClause = bindingNode.FirstAncestor<ElseClauseSyntax>();
            var ifClause = bindingNode.FirstAncestor<IfStatementSyntax>();
            var conditionalAssignment = new ConditionalAssignment {
                Reference = argument,
                AssignmentLocation = bindingNode.GetLocation()
            };
            SyntaxNode currentNode = bindingNode;
            var classDeclaration = argument.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var matchingField = classDeclaration
                .DescendantNodes<FieldDeclarationSyntax>()
                .FirstOrDefault(x => x.Declaration.Variables.Any(v => v.Identifier.Text == argument.ToString()));

            if (matchingField != null) {
                //If the assignment is a shared class field/property, we overwrite the reference; otherwise we track its assignments
                conditionalAssignment.Reference = matchingField.Declaration.Variables[0];
            }

            while (currentNode != null) {
                if (ifClause != null && !ifClause.Contains(elseClause)) {
                    conditionalAssignment.Conditions.AddRange(ProcessIfStatement(currentNode, out currentNode).Conditions);
                } else if (elseClause != null) {
                    conditionalAssignment.Conditions.AddRange(ProcessElseStatement(currentNode, out currentNode).Conditions);
                }
                else {
                    return conditionalAssignment;
                }

                elseClause = currentNode.FirstAncestor<ElseClauseSyntax>();
                ifClause = currentNode.FirstAncestor<IfStatementSyntax>();
            }

            return conditionalAssignment;
        }

        private ConditionalAssignment ProcessIfStatement(SyntaxNode node, out SyntaxNode lastNode) {
            var ifClause = node.FirstAncestor<IfStatementSyntax>();
            var conditionalAssignment = new ConditionalAssignment();
            lastNode = ifClause;

            if (ifClause == null)
                return conditionalAssignment;

            conditionalAssignment.AddCondition(ifClause.Condition.ToString(), ifClause.GetLocation());
            var elseClause = ifClause.Parent as ElseClauseSyntax;

            while (elseClause != null) {
                var ifStatement = elseClause.Parent as IfStatementSyntax;

                if (ifStatement == null)
                    break;

                conditionalAssignment.AddCondition($"!({ifStatement.Condition})", ifStatement.GetLocation());
                lastNode = ifStatement;
                elseClause = ifStatement.Parent as ElseClauseSyntax;
            }

            return conditionalAssignment;
        }

        private ConditionalAssignment ProcessElseStatement(SyntaxNode node, out SyntaxNode lastNode)
        {
            var elseClause = node.FirstAncestor<ElseClauseSyntax>();
            var conditionalAssignment = new ConditionalAssignment();
            lastNode = elseClause;

            while (elseClause != null) {
                var ifStatement = elseClause.Parent as IfStatementSyntax;

                if (ifStatement == null)
                    break;

                conditionalAssignment.AddCondition($"!({ifStatement.Condition})", ifStatement.GetLocation());
                lastNode = ifStatement;
                elseClause = ifStatement.Parent as ElseClauseSyntax;
            }

            return conditionalAssignment;
        }

        private IEnumerable<ObjectCreationExpressionSyntax> FindObjectCreations(ClassDeclarationSyntax node) {
            var className = node.Identifier.Text;
            var objectCreations = solution.Projects
                .SelectMany(x => x.GetCompilation()
                                  .SyntaxTrees.SelectMany(st => st.GetRoot()
                                                                  .DescendantNodes<ObjectCreationExpressionSyntax>()
                                                                  .Where(oce => oce.GetTypeName() == className)));

            return objectCreations;
        }
    }
}
