using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.ReachabilityProver.Model;
using Prometheus.Engine.Thread;
using Prometheus.Engine.Types;

namespace Prometheus.Engine.Reachability.Tracker {
    internal class ReferenceTracker
    {
        private readonly Solution solution;
        private readonly ITypeService typeService;
        private readonly ThreadSchedule threadSchedule;
        private readonly IReferenceParser referenceParser;

        public ReferenceTracker(Solution solution,
                                ThreadSchedule threadSchedule,
                                ITypeService typeService,
                                IReferenceParser referenceParser)
        {
            this.solution = solution;
            this.threadSchedule = threadSchedule;
            this.typeService = typeService;
            this.referenceParser = referenceParser;
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
        public List<ConditionalAssignment> GetAssignments(SyntaxToken identifier, InvocationExpressionSyntax invocationRestriction = null)
        {
            if (!threadSchedule.GetThreadPath(solution, identifier.GetLocation()).Invocations.Any())
                return new List<ConditionalAssignment>();

            //TODO: this does not take into account previous assignments to parameters: "a=b; ...; a=c; d=a;" => d != b so that case needs to be prunned
            var identifierName = identifier.ToString();
            var method = identifier.GetLocation().GetContainingMethod();
            var classDeclaration = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var matchingField = classDeclaration
                .DescendantNodes<FieldDeclarationSyntax>()
                .FirstOrDefault(x => x.Declaration.Variables.Any(v => v.Identifier.Text == identifierName));

            if (matchingField != null)
            {
                return GetConstructorAssignments(identifier, classDeclaration)
                    .Where(x => x.Reference.Node == null || x.Reference.Node.Kind() == SyntaxKind.IdentifierName)
                    .ToList();
            }

            var parameterIndex = method.ParameterList.Parameters.IndexOf(x => x.Identifier.Text == identifierName);

            //The assignment is not from a method parameter, so we search for the assignment inside
            var result = parameterIndex < 0
                ? GetMethodAssignments(identifier)
                : GetMethodCallAssignments(method, parameterIndex, invocationRestriction);
            // Exclude all except reference names; method calls "a = GetReference(c, d)" are not supported at the moment TODO: double check here
            //TODO: currently we support only one level method call assigment: "a = instance.Get(..);"
            result = result
                .Where(x => x.Reference.Node == null || (x.Reference.Node.Kind() == SyntaxKind.InvocationExpression ||
                                                         x.Reference.Node.Kind() == SyntaxKind.ElementAccessExpression ||
                                                         x.Reference.Node.Kind() == SyntaxKind.IdentifierName ||
                                                         x.Reference.Node.Kind() == SyntaxKind.VariableDeclarator ||
                                                         x.Reference.Node.Kind() == SyntaxKind.Argument))
                .ToList();

            return result;
        }

        /// <summary>
        /// Gets the assignments made from various calls to the given method along the binding parameter.
        /// </summary>
        private List<ConditionalAssignment> GetMethodCallAssignments(MethodDeclarationSyntax method, int parameterIndex, InvocationExpressionSyntax invocationRestriction)
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
                .Where(x => invocationRestriction == null || x == invocationRestriction)
                .SelectMany(x => GetConditionalAssignments(x, x.ArgumentList.Arguments[parameterIndex]))
                .ToList();

            return methodCallAssignments;
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
                .SelectMany(x => GetConditionalAssignments(x, x.ArgumentList.Arguments[constructorParameterIndex]))
                .ToList();

            return constructorAssignments;
        }

        /// <summary>
        /// Returns the list of assignments made for that identifier within the method in which the identifier is used.
        /// </summary>
        private List<ConditionalAssignment> GetMethodAssignments(SyntaxToken identifier)
        {
            //TODO
            var method = identifier.GetLocation().GetContainingMethod();
            var identifierName = identifier.ToString();
            var simpleAssignments = method
                .DescendantNodes<AssignmentExpressionSyntax>()
                .Where(x=>x.Kind() == SyntaxKind.SimpleAssignmentExpression)
                .Where(x=>x.Left.ToString() == identifierName)
                .SelectMany(x=>GetConditionalAssignments(x, x.Right));
            var localDeclarationAssignments = method
                .DescendantNodes<LocalDeclarationStatementSyntax>()
                .Where(x => x.Declaration.Variables[0].Identifier.Text == identifierName && x.Declaration.Variables[0].Initializer!=null)
                .SelectMany(x => GetConditionalAssignments(x, x.Declaration.Variables[0].Initializer.Value));
            var conditionalAssignments = simpleAssignments.Concat(localDeclarationAssignments).ToList();

            return conditionalAssignments;
        }

        /// <summary>
        /// Gets the conditional assignment for the given invocation or object creation within its method.
        /// </summary>
        private List<ConditionalAssignment> GetConditionalAssignments(SyntaxNode bindingNode, SyntaxNode argument) {

            if (argument is InvocationExpressionSyntax)
            {
                return ProcessMethodInvocationAssigment(argument.As<InvocationExpressionSyntax>());
            }

            var conditionalAssignment = new ConditionalAssignment {
                Reference = {Node = argument},
                AssignmentLocation = bindingNode.GetLocation()
            };
            SyntaxNode currentNode = bindingNode;
            var classDeclaration = argument.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var matchingField = classDeclaration
                .DescendantNodes<FieldDeclarationSyntax>()
                .FirstOrDefault(x => x.Declaration.Variables.Any(v => v.Identifier.Text == argument.ToString()));

            if (matchingField != null) {
                //If the assignment is a shared class field/property, we overwrite the reference; otherwise we track its assignments
                conditionalAssignment.Reference.Node = matchingField.Declaration.Variables[0];
            }

            var ifElseConditions = ExtractIfElseConditions(currentNode);
            conditionalAssignment.Conditions.UnionWith(ifElseConditions);

            return new List<ConditionalAssignment>{ conditionalAssignment };
        }

        private List<ConditionalAssignment> ProcessMethodInvocationAssigment(InvocationExpressionSyntax invocationExpression) {
            var memberAccess = invocationExpression.Expression.As<MemberAccessExpressionSyntax>();
            var instanceExpression = memberAccess.Expression.As<IdentifierNameSyntax>();
            var methodName = memberAccess.Name.Identifier.Text;
            var type = typeService.GetType(instanceExpression);
            var classDeclaration = typeService.GetClassDeclaration(type);
            var parametersCount = invocationExpression.ArgumentList.Arguments.Count;

            if (classDeclaration==null)
                throw new NotSupportedException($"Type {type} is was not found in solution");

            //We only take into consideration the calling method conditions
            var conditions = ExtractIfElseConditions(invocationExpression);
            //TODO: this only checks the name and the param count and picks the first method
            var method = classDeclaration
                .DescendantNodes<MethodDeclarationSyntax>(x => x.Identifier.Text == methodName && x.ParameterList.Parameters.Count==parametersCount)
                .First();
            var returnExpressions = method
                .DescendantNodes<ReturnStatementSyntax>()
                .Where(x => x.Expression.Kind()!=SyntaxKind.ObjectCreationExpression) //TODO: are we interested in "return new X()"?
                .Select(x =>
                {
                    var reference = referenceParser.Parse(x.Expression);
                    reference.InstanceReference = instanceExpression;

                    return new ConditionalAssignment
                    {
                        Reference = reference,
                        Conditions = conditions,
                        AssignmentLocation = invocationExpression.GetLocation()
                    };
                })
                .ToList();

            return returnExpressions;
        }

        private HashSet<Condition> ExtractIfElseConditions(SyntaxNode node)
        {
            var conditions = new HashSet<Condition>();
            var elseClause = node.FirstAncestor<ElseClauseSyntax>();
            var ifClause = node.FirstAncestor<IfStatementSyntax>();

            while (node != null) {
                if (ifClause != null && !ifClause.Contains(elseClause)) {
                    conditions.UnionWith(ProcessIfStatement(node, out node));
                } else if (elseClause != null) {
                    conditions.UnionWith(ProcessElseStatement(node, out node));
                } else {
                    return conditions;
                }

                elseClause = node.FirstAncestor<ElseClauseSyntax>();
                ifClause = node.FirstAncestor<IfStatementSyntax>();
            }

            return conditions;
        }

        private HashSet<Condition> ProcessIfStatement(SyntaxNode node, out SyntaxNode lastNode) {
            var ifClause = node.FirstAncestor<IfStatementSyntax>();
            var conditions = new HashSet<Condition>();

            lastNode = ifClause;

            if (ifClause == null)
                return conditions;

            conditions.Add(new Condition(ifClause, false));
            var elseClause = ifClause.Parent as ElseClauseSyntax;

            while (elseClause != null) {
                var ifStatement = elseClause.Parent as IfStatementSyntax;

                if (ifStatement == null)
                    break;

                conditions.Add(new Condition(ifClause, true));
                lastNode = ifStatement;
                elseClause = ifStatement.Parent as ElseClauseSyntax;
            }

            return conditions;
        }

        private HashSet<Condition> ProcessElseStatement(SyntaxNode node, out SyntaxNode lastNode)
        {
            var elseClause = node.FirstAncestor<ElseClauseSyntax>();
            var conditions = new HashSet<Condition>();
            lastNode = elseClause;

            while (elseClause != null) {
                var ifStatement = elseClause.Parent as IfStatementSyntax;

                if (ifStatement == null)
                    break;

                conditions.Add(new Condition(ifStatement, true));
                lastNode = ifStatement;
                elseClause = ifStatement.Parent as ElseClauseSyntax;
            }

            return conditions;
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
