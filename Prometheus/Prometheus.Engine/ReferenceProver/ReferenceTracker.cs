using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine.ReferenceProver {
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
        public List<ConditionalAssignment> GetAssignments(SyntaxToken identifier)
        {
            if (!threadSchedule.GetThreadPath(solution, identifier.GetLocation()).Invocations.Any())
                return new List<ConditionalAssignment>();

            //TODO: this does not take into account previous assignments to parameters
            var identifierName = identifier.ToString();
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
        private List<ConditionalAssignment> GetMethodCallAssignments(SyntaxToken identifier, MethodDeclarationSyntax method, int parameterIndex)
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

            if (!withinMethodAssignments.Any())
                return methodCallAssignments;

            foreach (var assignment in methodCallAssignments) {
                foreach (ConditionalAssignment withinMethodAssignment in withinMethodAssignments)
                {
                    var clonedAssignment = assignment.Clone();
                    clonedAssignment.Conditions.UnionWith(withinMethodAssignment.Conditions);
                    result.Add(clonedAssignment);
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
        private List<ConditionalAssignment> GetConstructorAssignments(SyntaxToken identifier, ClassDeclarationSyntax classDeclaration)
        {
            //TODO: we need to track exactly how the field gets initialized in the constructor; for now we enforce an argument with the same name in the constructor
            //TODO: we dont't know if the field was reasigned in another place (other than the constructor) before assigned to the field
            var constructorParameterIndex = identifier
                .GetLocation()
                .GetContainingConstructor()
                .ParameterList
                .Parameters
                .IndexOf(x => x.Identifier.Text == identifier.ToString());
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
        private List<ConditionalAssignment> GetMethodAssignments(SyntaxToken identifier)
        {
            var method = identifier.GetLocation().GetContainingMethod();
            var identifierName = identifier.ToString();
            var simpleAssignments = method
                .DescendantNodes<AssignmentExpressionSyntax>()
                .Where(x=>x.Kind() == SyntaxKind.SimpleAssignmentExpression)
                .Where(x=>x.Left.ToString() == identifierName)
                .Select(x=>GetConditionalAssignment(x, x.Right));
            var localDeclarationAssignments = method
                .DescendantNodes<LocalDeclarationStatementSyntax>()
                .Where(x => x.Declaration.Variables[0].Identifier.Text == identifierName && x.Declaration.Variables[0].Initializer!=null)
                .Select(x=> GetConditionalAssignment(x, x.Declaration.Variables[0].Initializer.Value));
            var conditionalAssignments = simpleAssignments.Concat(localDeclarationAssignments).ToList();

            return conditionalAssignments;
        }

        /// <summary>
        /// Gets the conditional assignment for the given invocation or object creation within its method.
        /// </summary>
        private ConditionalAssignment GetConditionalAssignment(SyntaxNode bindingNode, SyntaxNode argument) {
            //TODO: check if we can merge this method and the one above
            var elseClause = bindingNode.FirstAncestor<ElseClauseSyntax>();
            var ifClause = bindingNode.FirstAncestor<IfStatementSyntax>();
            var conditionalAssignment = new ConditionalAssignment {
                NodeReference = argument,
                AssignmentLocation = bindingNode.GetLocation()
            };
            SyntaxNode currentNode = bindingNode;
            var classDeclaration = argument.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var matchingField = classDeclaration
                .DescendantNodes<FieldDeclarationSyntax>()
                .FirstOrDefault(x => x.Declaration.Variables.Any(v => v.Identifier.Text == argument.ToString()));

            if (matchingField != null) {
                //If the assignment is a shared class field/property, we overwrite the reference; otherwise we track its assignments
                conditionalAssignment.NodeReference = matchingField.Declaration.Variables[0];
            }

            while (currentNode != null) {
                if (ifClause != null && !ifClause.Contains(elseClause)) {
                    conditionalAssignment.Conditions.UnionWith(ProcessIfStatement(currentNode, out currentNode).Conditions);
                } else if (elseClause != null) {
                    conditionalAssignment.Conditions.UnionWith(ProcessElseStatement(currentNode, out currentNode).Conditions);
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

            conditionalAssignment.AddCondition(ifClause, false);
            var elseClause = ifClause.Parent as ElseClauseSyntax;

            while (elseClause != null) {
                var ifStatement = elseClause.Parent as IfStatementSyntax;

                if (ifStatement == null)
                    break;

                conditionalAssignment.AddCondition(ifStatement, true);
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

                conditionalAssignment.AddCondition(ifStatement, true);
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
