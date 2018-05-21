using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.ConditionProver;
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
        private HaveCommonReference reachabilityDelegate;

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

        public void Configure(HaveCommonReference @delegate) {
            reachabilityDelegate = @delegate;
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
        public List<ConditionalAssignment> GetAssignments(SyntaxToken identifier, CallContext callContext = null)
        {
            if (!threadSchedule.GetThreadPath(solution, identifier.GetLocation()).Invocations.Any())
                return new List<ConditionalAssignment>();

            //TODO: this does not take into account previous assignments to parameters: "a=b; ...; a=c; d=a;" => d != b so that case needs to be prunned
            var identifierName = identifier.ToString();
            var method = identifier.GetLocation().GetContainingMethod();
            var classDeclaration = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var matchingField = classDeclaration
                .FirstDescendantNode<FieldDeclarationSyntax>(x => x.Declaration.Variables.Any(v => v.Identifier.Text == identifierName));

            if (matchingField != null)
            {
                return GetConstructorAssignments(identifier, classDeclaration, callContext)
                    .Where(x => x.RightReference.Node == null || x.RightReference.Node.Kind() == SyntaxKind.IdentifierName)
                    .ToList();
            }

            var parameterIndex = method.ParameterList.Parameters.IndexOf(x => x.Identifier.Text == identifierName);

            //The assignment is not from a method parameter, so we search for the assignment inside
            var result = parameterIndex < 0
                ? GetMethodAssignments(identifier)
                : GetMethodCallAssignments(method, parameterIndex, callContext);
            // Exclude all except reference names; method calls "a = GetReference(c, d)" are not supported at the moment TODO: double check here
            //TODO: currently we support only one level method call assigment: "a = instance.Get(..);"
            result = result
                .Where(x => x.RightReference.Node == null || (x.RightReference.Node.Kind() == SyntaxKind.InvocationExpression ||
                                                         x.RightReference.Node.Kind() == SyntaxKind.ElementAccessExpression ||
                                                         x.RightReference.Node.Kind() == SyntaxKind.IdentifierName ||
                                                         x.RightReference.Node.Kind() == SyntaxKind.VariableDeclarator ||
                                                         x.RightReference.Node.Kind() == SyntaxKind.Argument))
                .ToList();

            return result;
        }

        /// <summary>
        /// Gets the assignments made from various calls to the given method along the binding parameter.
        /// </summary>
        private List<ConditionalAssignment> GetMethodCallAssignments(MethodDeclarationSyntax method, int parameterIndex, CallContext callContext = null)
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
                .Where(x => callContext == null || x == callContext.InvocationExpression)
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
        /// <param name="callContext"></param>
        /// <returns></returns>
        private List<ConditionalAssignment> GetConstructorAssignments(SyntaxToken identifier, ClassDeclarationSyntax classDeclaration, CallContext callContext = null)
        {
            //TODO: we dont't know if the field was reasigned in another place (other than the constructor) before assigned to the field
            var constructorDeclaration = identifier
                .GetLocation()
                .GetContainingConstructor();
            string parameterIdentifier;
            var thisAssignment = constructorDeclaration
                .DescendantNodes<AssignmentExpressionSyntax>(x => x.Left is MemberAccessExpressionSyntax &&
                                                                  x.Left.As<MemberAccessExpressionSyntax>()
                                                                   .Expression is ThisExpressionSyntax)
                .FirstOrDefault(x => x.Left.As<MemberAccessExpressionSyntax>().Name.Identifier.Text == identifier.Text);
            var normalAssignment = constructorDeclaration
                .DescendantNodes<AssignmentExpressionSyntax>(x => x.Left is IdentifierNameSyntax)
                .FirstOrDefault(x => x.Left.As<IdentifierNameSyntax>().Identifier.Text == identifier.Text);

            if (thisAssignment != null)
            {
                parameterIdentifier = thisAssignment.Right.As<IdentifierNameSyntax>().Identifier.Text;
            }
            else if (normalAssignment != null)
            {
                parameterIdentifier = normalAssignment.Right.As<IdentifierNameSyntax>().Identifier.Text;
            }
            else
            {
                throw new NotSupportedException(
                    "Only direct assignments to IdentifierNameSyntax are currently allowed for fields");
            }

            var constructorParameterIndex = constructorDeclaration
                .ParameterList
                .Parameters
                .IndexOf(x => x.Identifier.Text == parameterIdentifier);
            var constructorAssignments = FindObjectCreations(classDeclaration)
                .Where(x =>
                {
                    var threadPath = threadSchedule.GetThreadPath(solution, x.GetLocation());
                    return threadPath != null && threadPath.Invocations.Any();
                })
                .Where(x =>
                {
                    if (callContext == null)
                        return true;

                    if(!(x.Parent is AssignmentExpressionSyntax))
                        return false;

                    var reference = new Reference(x.Parent
                        .As<AssignmentExpressionSyntax>().Left
                        .As<IdentifierNameSyntax>());

                    return reachabilityDelegate(reference, new Reference(callContext.InstanceReference), out var _);
                })
                //TODO: pass call context info here in case of not null
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
                return ProcessMethodInvocationAssigment(bindingNode, argument.As<InvocationExpressionSyntax>());

            var conditionalAssignment = new ConditionalAssignment {
                RightReference = {Node = argument},
                LeftReference = new Reference(bindingNode)
            };
            SyntaxNode currentNode = bindingNode;
            var classDeclaration = argument.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var matchingField = classDeclaration
                .FirstDescendantNode<FieldDeclarationSyntax>(x => x.Declaration.Variables.Any(v => v.Identifier.Text == argument.ToString()));

            if (matchingField != null) {
                //If the assignment is a shared class field/property, we overwrite the reference; otherwise we track its assignments
                conditionalAssignment.RightReference.Node = matchingField.Declaration.Variables[0];
            }

            var ifElseConditions = ExtractIfElseConditions(currentNode);
            conditionalAssignment.Conditions.UnionWith(ifElseConditions);

            return new List<ConditionalAssignment>{ conditionalAssignment };
        }

        private List<ConditionalAssignment> ProcessMethodInvocationAssigment(SyntaxNode bindingNode, InvocationExpressionSyntax invocationExpression) {
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
            var argumentsTable = new Dictionary<ParameterSyntax, ArgumentSyntax>();

            for (int i = 0; i < method.ParameterList.Parameters.Count; i++)
            {
                argumentsTable[method.ParameterList.Parameters[i]] = invocationExpression.ArgumentList.Arguments[i];
            }

            var returnExpressions = method
                .DescendantNodes<ReturnStatementSyntax>()
                .Where(x => x.Expression.Kind()!=SyntaxKind.ObjectCreationExpression) //TODO: are we interested in "return new X()"?
                .Select(x =>
                {
                    var returnReference = referenceParser.Parse(x.Expression);
                    returnReference.CallContext = new CallContext
                    {
                        InstanceReference = instanceExpression,
                        ArgumentsTable = argumentsTable,
                        InvocationExpression = invocationExpression
                    };

                    return new ConditionalAssignment
                    {
                        RightReference = returnReference,
                        Conditions = conditions,
                        LeftReference = new Reference(bindingNode)
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
