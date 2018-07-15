using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.ConditionProver;
using Prometheus.Engine.Reachability.Prover;
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
        private readonly ConditionCache conditionCache;

        public ReferenceTracker(Solution solution,
                                ThreadSchedule threadSchedule,
                                ITypeService typeService,
                                IReferenceParser referenceParser)
        {
            this.solution = solution;
            this.threadSchedule = threadSchedule;
            this.typeService = typeService;
            this.referenceParser = referenceParser;
            conditionCache = new ConditionCache();
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
        public List<ConditionalAssignment> GetAssignments(SyntaxToken identifier, Stack<ReferenceContext> referenceContexts = null)
        {
            if (!threadSchedule.ContainsLocation(solution, identifier.GetLocation()))
                return new List<ConditionalAssignment>();

            //TODO: this does not take into account previous assignments to parameters: "a=b; ...; a=c; d=a;" => d != b so that case needs to be prunned
            var identifierName = identifier.ToString();
            var method = identifier.GetLocation().GetContainingMethod();
            var classDeclaration = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var matchingField = classDeclaration.FirstDescendantNode<FieldDeclarationSyntax>(x => x.Declaration.Variables.Any(v => v.Identifier.Text == identifierName));
            var conditions = ExtractConditions(identifier.Parent);

            if (matchingField != null && matchingField.Modifiers.All(x=>x.Kind()!=SyntaxKind.StaticKeyword))
            {
                var constructorAssigments = GetConstructorAssignments(identifier, classDeclaration, referenceContexts)
                    .Where(x => x.RightReference.Node == null || IsAssignmentKindAllowed(x.RightReference.Node.Kind()))
                    .ToList();
                constructorAssigments.ForEach(x => x.Conditions.UnionWith(conditions));

                //TODO: append if/else conditions of the identifier location
                return constructorAssigments;
            }

            if (identifier.Text == "null")
            {
                var reference = new Reference(identifier.Parent);

                return new List<ConditionalAssignment>
                {
                    new ConditionalAssignment
                    {
                        Conditions = conditions,
                        LeftReference = reference,
                        RightReference = reference
                    }
                };
            }

            var parameterIndex = method.ParameterList.Parameters.IndexOf(x => x.Identifier.Text == identifierName);
            List<ConditionalAssignment> result;

            //The assignment is not from a method parameter, so we search for the assignment inside
            if (parameterIndex < 0)
            {
                result = GetInMethodAssignments(identifier);
            }
            else
            {
                result = GetReferenceMethodCallAssignments(method, parameterIndex, referenceContexts);
                result.ForEach(x => x.Conditions.UnionWith(conditions));
            }

            // Exclude all except reference names; method calls "a = GetReference(c, d)" are not supported at the moment TODO: double check here
            //TODO: currently we support only one level method call assigment: "a = instance.Get(..);"
            result = result
                .Where(x => x.RightReference.Node == null || IsAssignmentKindAllowed(x.RightReference.Node.Kind()))
                .ToList();

            return result;
        }

        private bool IsAssignmentKindAllowed(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.InvocationExpression:
                case SyntaxKind.ElementAccessExpression:
                case SyntaxKind.IdentifierName:
                case SyntaxKind.VariableDeclarator:
                case SyntaxKind.ArgumentList:
                case SyntaxKind.NumericLiteralExpression:
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.CharacterLiteralExpression:
                case SyntaxKind.NullLiteralExpression:
                case SyntaxKind.TrueLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the assignments made from various calls to the given method along the binding parameter.
        /// </summary>
        private List<ConditionalAssignment> GetReferenceMethodCallAssignments(MethodDeclarationSyntax method, int parameterIndex, Stack<ReferenceContext> referenceContexts = null)
        {
            //TODO: this does not take into account if a parameter was assigned to another value before being assigned again
            var invocations = solution
                .FindReferenceLocations(method)
                .Where(x => threadSchedule.ContainsLocation(solution, x.Location))
                .Select(x => x.GetNode<InvocationExpressionSyntax>());
            var methodCallAssignments = invocations
                .Where(x => referenceContexts == null || referenceContexts.Count==0 || x == referenceContexts.Peek().CallContext?.InvocationExpression)
                .SelectMany(x => GetConditionalAssignments(x, x.ArgumentList.Arguments[parameterIndex]))
                .Select(x => {
                    if (referenceContexts == null)
                        return x;

                    if (x.RightReference.ReferenceContexts.Count != 0) {
                        referenceContexts.Push(x.RightReference.ReferenceContexts.Peek());
                    }

                    x.RightReference.ReferenceContexts = referenceContexts;

                    return x;
                })
                .ToList();

            return methodCallAssignments;
        }

        /// <summary>
        /// Gets the assignments made from the initializing the constructor in any thread path
        /// The assignment comes from a field/property of the current class.
        /// </summary>
        private List<ConditionalAssignment> GetConstructorAssignments(SyntaxToken identifier, ClassDeclarationSyntax classDeclaration, Stack<ReferenceContext> referenceContexts = null)
        {
            //TODO: we dont't know if the field was reasigned in another place (other than the constructor) before assigned to the field
            //TODO: MAJOR: this gets only the first constructor: handle all constructors
            var constructorDeclaration = classDeclaration.DescendantNodes<ConstructorDeclarationSyntax>().First();
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
                throw new NotSupportedException($"Only direct assignments to {nameof(IdentifierNameSyntax)} are currently allowed for fields");
            }

            //TODO: also check for properties/fields assigned outside of constructor "instance.Member = reference" or "instance = new Instance { Member = reference }";
            var constructorParameterIndex = constructorDeclaration
                .ParameterList
                .Parameters
                .IndexOf(x => x.Identifier.Text == parameterIdentifier);
            var constructorAssignments = FindObjectCreations(classDeclaration)
                .Where(x => threadSchedule.ContainsLocation(solution, x.GetLocation()))
                .Where(x =>
                {
                    if (referenceContexts == null || referenceContexts.Count==0)
                        return true;

                    if(!(x.Parent is AssignmentExpressionSyntax))
                        return false;

                    var reference = new Reference(x.Parent
                        .As<AssignmentExpressionSyntax>().Left
                        .As<IdentifierNameSyntax>());

                    //TODO: currently, we do not handle situations in which the found reference assignment happened before the current assignment
                    return reachabilityDelegate(reference, new Reference(referenceContexts.Peek().CallContext.InstanceReference), out var _);
                })
                .SelectMany(x => GetConditionalAssignments(x, x.ArgumentList.Arguments[constructorParameterIndex]))
                .Select(x =>
                {
                    if (referenceContexts == null)
                        return x;

                    if (x.RightReference.ReferenceContexts.Count != 0)
                    {
                        referenceContexts.Push(x.RightReference.ReferenceContexts.Peek());
                    }

                    x.RightReference.ReferenceContexts = referenceContexts;

                    return x;
                })
                .ToList();

            return constructorAssignments;
        }

        /// <summary>
        /// Returns the list of assignments made for that identifier within the method in which the identifier is used.
        /// </summary>
        private List<ConditionalAssignment> GetInMethodAssignments(SyntaxToken identifier)
        {
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
                var invocationExpression = argument.As<InvocationExpressionSyntax>();

                if (invocationExpression.Expression is MemberAccessExpressionSyntax)
                {
                    return ProcessNonLocalMethodCallAssignments(bindingNode, invocationExpression);
                }

                if(invocationExpression.Expression is IdentifierNameSyntax)
                    return ProcessLocalMethodCallAssignments(bindingNode, invocationExpression);

                throw new NotSupportedException($"InvocationExpression {invocationExpression} is not supported");
            }

            if (argument is ObjectCreationExpressionSyntax)
                return new List<ConditionalAssignment>();

            var (rightReference, query) = referenceParser.Parse(argument);

            if (query != null)
            {
                rightReference.ReferenceContexts.Push(new ReferenceContext(null, query));
            }

            var conditionalAssignment = new ConditionalAssignment {
                LeftReference = new Reference(bindingNode),
                RightReference = rightReference
            };
            SyntaxNode currentNode = bindingNode;
            var classDeclaration = argument.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var matchingField = classDeclaration
                .FirstDescendantNode<FieldDeclarationSyntax>(x => x.Declaration.Variables.Any(v => v.Identifier.Text == argument.ToString()));

            if (matchingField != null) {
                //If the assignment is a shared class field/property, we overwrite the reference; otherwise we track its assignments
                conditionalAssignment.RightReference.Node = matchingField.Declaration.Variables[0];
            }

            var ifElseConditions = ExtractConditions(currentNode);
            conditionalAssignment.Conditions.UnionWith(ifElseConditions);

            return new List<ConditionalAssignment>{ conditionalAssignment };
        }

        /// <summary>
        /// Process static or reference method calls assignments.
        /// </summary>
        private List<ConditionalAssignment> ProcessNonLocalMethodCallAssignments(SyntaxNode bindingNode, InvocationExpressionSyntax invocationExpression)
        {
            var className = invocationExpression
                .Expression.As<MemberAccessExpressionSyntax>()
                .Expression.As<IdentifierNameSyntax>()
                .Identifier.Text;

            var isStatic = typeService.TryGetType(className, out var type);

            if (typeService.IsExternal(type))
            {
                var rightReference = new Reference(invocationExpression)
                {
                    IsExternal = true,
                    IsPure = typeService.IsPureMethod(invocationExpression, out var _)
                };

                var conditionalAssignment = new ConditionalAssignment
                {
                    LeftReference = new Reference(bindingNode),
                    RightReference = rightReference,
                    Conditions = ExtractConditions(bindingNode)
                };

                return new List<ConditionalAssignment>{conditionalAssignment};
            }

            return isStatic ?
                ProcessStaticMethodCallAssignments(bindingNode, invocationExpression) :
                ProcessReferenceMethodCallAssignments(bindingNode, invocationExpression);
        }

        /// <summary>
        /// Gets the assignments made from various calls to the given method along the binding parameter.
        /// This handles reference based methods like "instance = Method(...)" where "Method" is instance or static method of the same class containing the assignment.
        /// </summary>
        private List<ConditionalAssignment> ProcessLocalMethodCallAssignments(SyntaxNode bindingNode, InvocationExpressionSyntax invocationExpression) {
            var methodName = invocationExpression.Expression.As<IdentifierNameSyntax>().Identifier.Text;
            var conditions = ExtractConditions(invocationExpression);
            var classDeclaration = invocationExpression.GetContainingClass();
            var parametersCount = invocationExpression.ArgumentList.Arguments.Count;

            //TODO: this only checks the name and the param count and picks the first method
            var method = classDeclaration
                .DescendantNodes<MethodDeclarationSyntax>(x => x.Identifier.Text == methodName &&
                                                               x.ParameterList.Parameters.Count == parametersCount)
                .First();
            var argumentsTable = new Dictionary<ParameterSyntax, ArgumentSyntax>();

            for (int i = 0; i < method.ParameterList.Parameters.Count; i++) {
                argumentsTable[method.ParameterList.Parameters[i]] = invocationExpression.ArgumentList.Arguments[i];
            }

            var returnExpressions = method
                .DescendantNodes<ReturnStatementSyntax>()
                .Where(x => x.Expression.Kind() != SyntaxKind.ObjectCreationExpression) //TODO: are we interested in "return new X()"?
                .Select(x => {
                    var (returnReference, query) = referenceParser.Parse(x.Expression);

                    var callContext = new CallContext {
                        ArgumentsTable = argumentsTable,
                        InvocationExpression = invocationExpression
                    };
                    returnReference.ReferenceContexts.Push(new ReferenceContext(callContext, query));

                    return new ConditionalAssignment {
                        RightReference = returnReference,
                        Conditions = conditions,
                        LeftReference = new Reference(bindingNode)
                    };
                })
                .ToList();

            return returnExpressions;
        }

        /// <summary>
        /// Gets the assignments made from various calls to the given method along the binding parameter.
        /// This handles reference based methods like "instance = StaticClass.Method(...)" where "Method" is static method.
        /// </summary>
        private List<ConditionalAssignment> ProcessStaticMethodCallAssignments(SyntaxNode bindingNode, InvocationExpressionSyntax invocationExpression) {
            var memberAccess = invocationExpression.Expression.As<MemberAccessExpressionSyntax>();
            var className = memberAccess.Expression.As<IdentifierNameSyntax>().Identifier.Text;
            var methodName = memberAccess.Name.Identifier.Text;
            var conditions = ExtractConditions(invocationExpression);
            var classDeclaration = typeService.GetClassDeclaration(className);
            var parametersCount = invocationExpression.ArgumentList.Arguments.Count;

            //TODO: this only checks the name and the param count and picks the first method
            var method = classDeclaration
                .DescendantNodes<MethodDeclarationSyntax>(x => x.Identifier.Text == methodName &&
                                                               x.ParameterList.Parameters.Count == parametersCount)
                .First();
            var argumentsTable = new Dictionary<ParameterSyntax, ArgumentSyntax>();

            for (int i = 0; i < method.ParameterList.Parameters.Count; i++) {
                argumentsTable[method.ParameterList.Parameters[i]] = invocationExpression.ArgumentList.Arguments[i];
            }

            var returnExpressions = method
                .DescendantNodes<ReturnStatementSyntax>()
                .Where(x => x.Expression.Kind() != SyntaxKind.ObjectCreationExpression) //TODO: are we interested in "return new X()"?
                .Select(x => {
                    var (returnReference, query) = referenceParser.Parse(x.Expression);

                    var callContext = new CallContext {
                        ArgumentsTable = argumentsTable,
                        InvocationExpression = invocationExpression
                    };
                    returnReference.ReferenceContexts.Push(new ReferenceContext(callContext, query));

                    return new ConditionalAssignment {
                        RightReference = returnReference,
                        Conditions = conditions,
                        LeftReference = new Reference(bindingNode)
                    };
                })
                .ToList();

            return returnExpressions;
        }

        /// <summary>
        /// Processes assignments such as "instance = reference.Method(...)" or "instance = collection"
        /// </summary>
        /// <param name="bindingNode"></param>
        /// <param name="invocationExpression"></param>
        /// <returns></returns>
        private List<ConditionalAssignment> ProcessReferenceMethodCallAssignments(SyntaxNode bindingNode, InvocationExpressionSyntax invocationExpression) {
            var memberAccess = invocationExpression.Expression.As<MemberAccessExpressionSyntax>();
            var instanceExpression = memberAccess.Expression.As<IdentifierNameSyntax>();
            var methodName = memberAccess.Name.Identifier.Text;

            if (referenceParser.IsBuiltInMethod(methodName))
                return ProcessLinqMethodAssignments(bindingNode, invocationExpression, instanceExpression);

            return ProcessRegularReferenceMethodCallAssignments(bindingNode, invocationExpression, instanceExpression);
        }

        /// <summary>
        /// Processes Linq based methods: First(), FirstOrDefault(), Where().
        /// </summary>
        private List<ConditionalAssignment> ProcessLinqMethodAssignments(SyntaxNode bindingNode,
            InvocationExpressionSyntax invocationExpression,
            IdentifierNameSyntax instanceExpression)
        {
            //We only take into consideration the calling method conditions
            var conditions = ExtractConditions(invocationExpression);
            var (reference, query) = referenceParser.Parse(invocationExpression);
            var callContext = new CallContext
            {
                InstanceReference = instanceExpression
            };
            reference.ReferenceContexts.Push(new ReferenceContext(callContext, query));

            var assignment = new ConditionalAssignment
            {
                RightReference = reference,
                Conditions = conditions,
                LeftReference = new Reference(bindingNode)
            };

            return new List<ConditionalAssignment> {assignment};
        }

        /// <summary>
        /// Processes regular method invocations "instance = reference.Get(...)".
        /// </summary>
        private List<ConditionalAssignment> ProcessRegularReferenceMethodCallAssignments(SyntaxNode bindingNode,
            InvocationExpressionSyntax invocationExpression,
            IdentifierNameSyntax instanceExpression) {
            var memberAccess = invocationExpression.Expression.As<MemberAccessExpressionSyntax>();
            var methodName = memberAccess.Name.Identifier.Text;
            var conditions = ExtractConditions(invocationExpression);

            //TODO: handle when code is outside of the current solution (3rd party code)
            var type = typeService.GetType(instanceExpression);
            var classDeclaration = typeService.GetClassDeclaration(type);
            var parametersCount = invocationExpression.ArgumentList.Arguments.Count;

            if (classDeclaration == null)
                throw new NotSupportedException($"Type {type} is was not found in solution");

            //TODO: this only checks the name and the param count and picks the first method
            var method = classDeclaration
                .DescendantNodes<MethodDeclarationSyntax>(x => x.Identifier.Text == methodName &&
                                                               x.ParameterList.Parameters.Count == parametersCount)
                .First();
            var argumentsTable = new Dictionary<ParameterSyntax, ArgumentSyntax>();

            for (int i = 0; i < method.ParameterList.Parameters.Count; i++) {
                argumentsTable[method.ParameterList.Parameters[i]] = invocationExpression.ArgumentList.Arguments[i];
            }

            var returnExpressions = method
                .DescendantNodes<ReturnStatementSyntax>()
                .Where(x => x.Expression.Kind() != SyntaxKind.ObjectCreationExpression) //TODO: are we interested in "return new X()"?
                .Select(x => {
                    var (returnReference, query) = referenceParser.Parse(x.Expression);

                    var callContext = new CallContext {
                        InstanceReference = instanceExpression,
                        ArgumentsTable = argumentsTable,
                        InvocationExpression = invocationExpression
                    };
                    returnReference.ReferenceContexts.Push(new ReferenceContext(callContext, query));

                    return new ConditionalAssignment {
                        RightReference = returnReference,
                        Conditions = conditions,
                        LeftReference = new Reference(bindingNode)
                    };
                })
                .ToList();

            return returnExpressions;
        }

        private HashSet<Condition> ExtractConditions(SyntaxNode node)
        {
            if (conditionCache.TryGet(node, out var conditions))
                return conditions;

            var exitConditions = ExtractNegatedExitConditions(node);
            conditions = ExtractIfElseConditions(node);
            conditions.UnionWith(exitConditions);
            conditionCache.AddToCache(node, conditions);

            return conditions;
        }

        /// <summary>
        /// In the case of return types, for instance for "return person" statement to be reached,
        /// the previous 2 conditions need to be negated: NOT (person.Age == 20) AND NOT (person.Age == 30).
        /// </summary>
        /// <code>
        ///  public Person DecrementAge(Person person)
        ///  {
        ///      if(person.Age == 20)
        ///      {
        ///          return new Person(20);
        ///      }
        ///      else if(person.Age == 30)
        ///      {
        ///          return new Person(30);
        ///      }
        ///
        ///      return person;
        ///  }
        /// </code>
        private HashSet<Condition> ExtractNegatedExitConditions(SyntaxNode node)
        {
            var nodeSpan = node.GetLocation().SourceSpan;
            var methodDeclaration = node.GetContainingMethod();
            var previousReturnConditions = methodDeclaration
                .DescendantNodes<ReturnStatementSyntax>()
                .Where(x => x.GetLocation().SourceSpan.End < nodeSpan.Start)
                .Select(ExtractIfElseConditions)
                .Where(x=>x.Any())
                .Select(x=> new Condition(x, true))
                .ToList();
            var exitConditions = previousReturnConditions.Any() ? new HashSet<Condition>(previousReturnConditions) : new HashSet<Condition>();

            return exitConditions;
        }

        /// <summary>
        /// In the case of return types, for instance for "return new Person(30)" statement to be reached,
        /// the following conditions need to be satisfied: (person.AccountBalance == 100) AND NOT `(person.Age == 20) AND (person.Age == 30).
        /// </summary>
        /// <code>
        ///  public Person DecrementAge(Person person)
        ///  {
        ///      if(person.AccountBalance == 100)
        ///      {
        ///         if(person.Age == 20)
        ///         {
        ///             return new Person(20);
        ///         }
        ///         else if(person.Age == 30)
        ///         {
        ///             return new Person(30);
        ///         }
        ///      }
        ///
        ///      return person;
        ///  }
        /// </code>
        private HashSet<Condition> ExtractIfElseConditions(SyntaxNode node)
        {
            HashSet<Condition> conditions = new HashSet<Condition>();
            var elseClause = node.FirstAncestor<ElseClauseSyntax>();
            var ifClause = node.FirstAncestor<IfStatementSyntax>();

            //If the node is within the condition of an IfStatement,
            //we exclude the condition and continue with the ancestors
            if (ifClause!=null && ifClause.Condition.Contains(node))
            {
                node = ifClause;
                ifClause = ifClause.FirstAncestor<IfStatementSyntax>();
            }

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

            conditions.Add(new Condition(ifClause.Condition, false));
            var elseClause = ifClause.Parent as ElseClauseSyntax;

            while (elseClause != null) {
                var ifStatement = elseClause.Parent as IfStatementSyntax;

                if (ifStatement == null)
                    break;

                conditions.Add(new Condition(ifStatement.Condition, true));
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

                conditions.Add(new Condition(ifStatement.Condition, true));
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
                                                                  .Where(oce => oce.Type is IdentifierNameSyntax && oce.GetTypeName() == className)));

            return objectCreations;
        }
    }
}
