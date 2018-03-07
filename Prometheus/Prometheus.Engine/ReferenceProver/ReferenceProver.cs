using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using TypeInfo = System.Reflection.TypeInfo;

namespace Prometheus.Engine.ReferenceProver
{
    internal class ReferenceProver : IDisposable
    {
        private readonly ReferenceTracker referenceTracker;
        private readonly List<TypeInfo> solutionTypes;
        private readonly Dictionary<string, Type> coreTypeAliases;
        private readonly Context context;

        public ReferenceProver(ReferenceTracker referenceTracker, Solution solution)
        {
            this.referenceTracker = referenceTracker;
            //todo: needs to get projects referenced assemblies
            solutionTypes = solution.Projects.Select(x => Assembly.Load(x.AssemblyName)).SelectMany(x => x.DefinedTypes)
                .ToList();
            solutionTypes.AddRange(Assembly.GetAssembly(typeof(int)).DefinedTypes);
            coreTypeAliases = new Dictionary<string, Type>
            {
                {"byte", typeof(byte)},
                {"sbyte",typeof(sbyte)},
                {"short", typeof(short)},
                {"ushort", typeof(ushort)},
                {"int", typeof(int)},
                {"uint", typeof(uint)},
                {"long", typeof(long)},
                {"ulong", typeof(ulong)},
                {"float", typeof(float)},
                {"double", typeof(double)},
                {"decimal", typeof(decimal)},
                {"object", typeof(object)},
                {"bool", typeof(bool)},
                {"char", typeof(char)},
                {"byte?", typeof(byte?)},
                {"sbyte?",typeof(sbyte?)},
                {"short?", typeof(short?)},
                {"ushort?", typeof(ushort?)},
                {"int?", typeof(int?)},
                {"uint?", typeof(uint?)},
                {"long?", typeof(long?)},
                {"ulong?", typeof(ulong?)},
                {"float?", typeof(float?)},
                {"double?", typeof(double?)},
                {"decimal?", typeof(decimal?)},
                {"bool?", typeof(bool?)},
                {"char?", typeof(char?)},
                {"string", typeof(string)}
            };
            context = new Context();
        }

        public void Dispose()
        {
            context.Dispose();
        }

        /// <summary>
        /// Checks whether two identifiers can possibly have the same value within a thread schedule.
        /// TODO: this does not consider the possible conditional cases for assigning a variable within loops ("for", "while" loops).
        /// TODO: this does not consider assignments like: "order = person.Orders.First()" and "order = instance" where "instance" and "person.Orders.First()" can be the same
        /// </summary>
        public bool HaveCommonValue(SyntaxToken first, SyntaxToken second, out object commonNode)
        {
            var firstAssignment = new ConditionalAssignment
            {
                TokenReference = first,
                AssignmentLocation = first.GetLocation()
            };
            var secondAssignment = new ConditionalAssignment
            {
                TokenReference = second,
                AssignmentLocation = second.GetLocation()
            };
            return HaveCommonValueInternal(firstAssignment, secondAssignment, out commonNode);
        }

        /// <summary>
        /// Checks whether two syntax nodes can possibly have the same value within a thread schedule.
        /// TODO: this does not consider the possible conditional cases for assigning a variable within loops ("for", "while" loops).
        /// TODO: this does not consider assignments like: "order = person.Orders.First()" and "order = instance" where "instance" and "person.Orders.First()" can be the same
        /// </summary>
        public bool HaveCommonValue(SyntaxNode first, SyntaxNode second, out object commonNode) {
            var firstAssignment = new ConditionalAssignment {
                NodeReference = first,
                AssignmentLocation = first.GetLocation()
            };
            var secondAssignment = new ConditionalAssignment {
                NodeReference = second,
                AssignmentLocation = second.GetLocation()
            };
            return HaveCommonValueInternal(firstAssignment, secondAssignment, out commonNode);
        }

        private bool HaveCommonValueInternal(ConditionalAssignment first, ConditionalAssignment second, out object commonNode)
        {
            commonNode = null;

            //TODO: need to check scoping: if "first" is a local variable => it cannot match a variable from another function/thread
            var firstAssignments = referenceTracker.GetAssignments(first.NodeReference?.DescendantTokens().First() ?? first.TokenReference);
            //todo: this needs checking
            var secondAssignments = referenceTracker.GetAssignments(second.NodeReference?.DescendantTokens().First() ?? second.TokenReference);

            foreach (ConditionalAssignment assignment in firstAssignments)
            {
                assignment.Conditions.UnionWith(first.Conditions);
            }

            foreach (ConditionalAssignment assignment in secondAssignments)
            {
                assignment.Conditions.UnionWith(second.Conditions);
            }

            if (!firstAssignments.Any())
            {
                firstAssignments = new List<ConditionalAssignment> {first};
            }

            if (!secondAssignments.Any())
            {
                secondAssignments = new List<ConditionalAssignment> {second};
            }

            foreach (ConditionalAssignment firstAssignment in firstAssignments)
            {
                foreach (ConditionalAssignment secondAssignment in secondAssignments)
                {
                    if (ValidateReachability(firstAssignment, secondAssignment, out commonNode))
                        return true;
                }
            }

            return false;
        }

        private bool ValidateReachability(ConditionalAssignment first, ConditionalAssignment second,
            out object commonNode)
        {
            commonNode = null;

            if (!IsSatisfiable(first, second))
                return false;

            if (AreEquivalent(first, second))
            {
                commonNode = first.NodeReference ?? (object) first.TokenReference;
                return true;
            }

            var firstReferenceAssignment = new ConditionalAssignment
            {
                NodeReference = first.NodeReference,
                TokenReference = first.TokenReference,
                AssignmentLocation = first.AssignmentLocation,
                Conditions = first.Conditions
            };
            var secondReferenceAssignment = new ConditionalAssignment
            {
                NodeReference = second.NodeReference,
                TokenReference = second.TokenReference,
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
        private static bool AreEquivalent(ConditionalAssignment first, ConditionalAssignment second)
        {
            var firstReferenceName = first.NodeReference?.ToString() ?? first.TokenReference.ToString();
            var secondReferenceName = second.NodeReference?.ToString() ?? second.TokenReference.ToString();
            var firstLocation = first.NodeReference?.GetLocation() ?? first.TokenReference.GetLocation();
            var secondLocation = second.NodeReference?.GetLocation() ?? second.TokenReference.GetLocation();

            if (firstReferenceName != secondReferenceName)
                return false;

            return firstLocation == secondLocation;
        }

        #region Conditional prover

        //TODO: move this to separate service
        private class NodeType
        {
            public SyntaxNode Node { get; set; }
            public Expr Expression { get; set; }
            public List<Type> TypeChain { get; set; }
            public Type Type => TypeChain.Last();
        }

        private readonly List<NodeType> conditionalNodeTable = new List<NodeType>();
        private readonly Dictionary<string, NodeType> currentNodeTable = new Dictionary<string, NodeType>();
        private bool matchExpressions;

        private bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second)
        {
            BoolExpr firstCondition = ParseConditionalAssignment(first);
            matchExpressions = true;
            currentNodeTable.Clear();

            BoolExpr secondCondition = ParseConditionalAssignment(second);
            matchExpressions = false;
            currentNodeTable.Clear();
            conditionalNodeTable.Clear();

            Solver solver = context.MkSolver();
            solver.Assert(firstCondition, secondCondition);
            Status status = solver.Check();

            return status == Status.SATISFIABLE;
        }

        private BoolExpr ParseConditionalAssignment(ConditionalAssignment assignment)
        {
            BoolExpr[] conditions =
                assignment.Conditions.Select(
                    x =>
                        x.IsNegated
                            ? context.MkNot(ParseExpression(x.IfStatement.Condition))
                            : ParseExpression(x.IfStatement.Condition)).ToArray();
            BoolExpr expression = context.MkAnd(conditions);

            return expression;
        }

        private BoolExpr ParseExpression(ExpressionSyntax expressionSyntax)
        {
            var expressionKind = expressionSyntax.Kind();

            switch (expressionKind)
            {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression((PrefixUnaryExpressionSyntax) expressionSyntax);
                case SyntaxKind.SimpleMemberAccessExpression:
                    return context.MkBoolConst(expressionSyntax.ToString());
            }

            var binaryExpression = (BinaryExpressionSyntax) expressionSyntax;

            switch (expressionKind)
            {
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                    return ParseBinaryExpression(binaryExpression);
                default:
                    throw new NotImplementedException();
            }

        }

        private BoolExpr ParseBinaryExpression(BinaryExpressionSyntax binaryExpression)
        {
            SyntaxKind expressionKind = binaryExpression.Kind();
            Expr left = ParseExpressionMember(binaryExpression.Left);
            Expr right = ParseExpressionMember(binaryExpression.Right);

            switch (expressionKind)
            {
                case SyntaxKind.LogicalAndExpression:
                    return context.MkAnd((BoolExpr) left, (BoolExpr) right);
                case SyntaxKind.LogicalOrExpression:
                    return context.MkOr((BoolExpr) left, (BoolExpr) right);
                case SyntaxKind.GreaterThanExpression:
                    return context.MkGt((ArithExpr) left, (ArithExpr) right);
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return context.MkGe((ArithExpr) left, (ArithExpr) right);
                case SyntaxKind.LessThanExpression:
                    return context.MkLt((ArithExpr) left, (ArithExpr) right);
                case SyntaxKind.LessThanOrEqualExpression:
                    return context.MkLe((ArithExpr) left, (ArithExpr) right);

                //TODO: Fix this: since this is only for numeric values
                case SyntaxKind.EqualsExpression:
                    return context.MkEq((ArithExpr) left, (ArithExpr) right);
                case SyntaxKind.NotEqualsExpression:
                    return context.MkNot(context.MkEq((ArithExpr) left, (ArithExpr) right));
                default:
                    throw new NotImplementedException();
            }
        }

        private BoolExpr ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression)
        {
            if (prefixUnaryExpression.Kind() == SyntaxKind.LogicalNotExpression)
            {
                var innerExpression = prefixUnaryExpression.Operand;
                var parsedExpression = ParseExpression(innerExpression);
                return context.MkNot(parsedExpression);
            }

            throw new NotImplementedException();
        }

        private Expr ParseExpressionMember(ExpressionSyntax memberExpression)
        {
            var expressionKind = memberExpression.Kind();

            if (memberExpression is BinaryExpressionSyntax) {
                return ParseBinaryExpression((BinaryExpressionSyntax)memberExpression);
            }

            if (expressionKind == SyntaxKind.NumericLiteralExpression)
            {
                return ParseNumericLiteral(memberExpression.ToString());
            }

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName)
            {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseUnaryExpression(memberExpression)
                    : ParseExpression(memberExpression);
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression)
            {
                return ParseUnaryExpression(memberExpression);
            }

            var typeChain = expressionKind == SyntaxKind.SimpleMemberAccessExpression
                ? GetNodeTypes((MemberAccessExpressionSyntax)memberExpression)
                : new List<Type> { GetNodeType((IdentifierNameSyntax)memberExpression) };
            var nodeType = typeChain.Last();

            if (!matchExpressions)
            {
                //TODO: check nested reference chains from different chains: "customer.Address.ShipInfo" & "order.ShipInfo" to be the same
                //TODO: check agains same reference chains: "from.Address.ShipInfo" & "to.Address.ShipInfo"
                //Check against the nodes from already parsed the first conditional assignment
                return ParseVariableExpression(memberExpression, typeChain);
            }

            foreach (NodeType node in conditionalNodeTable.Where(x => x.Type == nodeType))
            {
                //TODO: MAJOR
                //TODO: for now, we only match "amount1" with "amount2" (identifier with identifier) or "[from].AccountBalance" with "[from2].AccountBalance"
                //TODO: need to extend to "amount" with "[from].AccountBalance" and other combinations
                if (node.Node is IdentifierNameSyntax && memberExpression is IdentifierNameSyntax)
                {
                    if (HaveCommonValue(node.Node, memberExpression, out object _))
                        return node.Expression;
                }

                if (node.Node is MemberAccessExpressionSyntax && memberExpression is MemberAccessExpressionSyntax)
                {
                    var firstMember = (MemberAccessExpressionSyntax) node.Node;
                    var secondMember = (MemberAccessExpressionSyntax)memberExpression;

                    if (HaveCommonValue(GetRootIdentifier(firstMember), GetRootIdentifier(secondMember), out object _))
                        return node.Expression;
                }
            }

            return ParseVariableExpression(memberExpression, typeChain);
        }

        private Expr ParseUnaryExpression(ExpressionSyntax unaryExpression)
        {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax) unaryExpression;
            var negatedExpression = ParseExpressionMember(prefixUnaryExpression.Operand);

            return context.MkUnaryMinus((ArithExpr) negatedExpression);
        }

        private Expr ParseVariableExpression(ExpressionSyntax memberExpression, List<Type> typeChain)
        {
            string memberName = memberExpression.ToString();

            if (currentNodeTable.ContainsKey(memberName))
            {
                return currentNodeTable[memberName].Expression;
            }

            var constExpr = context.MkConst(memberName, context.RealSort);
            var nodeTypes = new NodeType
            {
                Expression = constExpr,
                Node = memberExpression,
                TypeChain = typeChain
            };
            currentNodeTable[memberName] = nodeTypes;
            conditionalNodeTable.Add(nodeTypes);

            return constExpr;
        }

        private Expr ParseNumericLiteral(string numericLiteral)
        {
            Sort sort = int.TryParse(numericLiteral, out int _) ? context.IntSort : (Sort) context.RealSort;
            return context.MkNumeral(numericLiteral, context.RealSort); //TODO: issue on real>int expression
        }

        #endregion

        #region Type retrieval

        public IdentifierNameSyntax GetRootIdentifier(MemberAccessExpressionSyntax memberAccess)
        {
            var rootToken = memberAccess.ToString().Split('.').First();
            var identifier = memberAccess.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == rootToken).First();

            return identifier;
        }

        public List<Type> GetNodeTypes(MemberAccessExpressionSyntax memberExpression) {
            //TODO: this only gets the type for variables with explicit defined type: we don't process "var"
            Queue<string> memberTokens = new Queue<string>(memberExpression.ToString().Split('.'));
            string rootToken = memberTokens.First();
            var typeName = GetTypeName(memberExpression, rootToken);
            memberTokens.Dequeue();
            var types = new List<Type>();

            //todo: there can be multiple classes with the same name
            Type rootType = GetType(typeName);
            Type currentType = rootType;
            types.Add(currentType);

            while (memberTokens.Count > 0) {
                var member = currentType.GetMember(memberTokens.Dequeue(),
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.GetProperty |
                    BindingFlags.GetField)[0];

                switch (member.MemberType) {
                    case MemberTypes.Field:
                        currentType = member.As<FieldInfo>().FieldType;
                        break;
                    case MemberTypes.Property:
                        currentType = member.As<PropertyInfo>().PropertyType;
                        break;
                    default:
                        throw new NotSupportedException();
                }

                types.Add(currentType);
            }

            return types;
        }

        public Type GetNodeType(IdentifierNameSyntax identifierNameSyntax) {
            string typeName = GetTypeName(identifierNameSyntax, identifierNameSyntax.Identifier.Text);
            Type type = GetType(typeName);

            return type;
        }

        private string GetTypeName(SyntaxNode node, string rootToken) {
            //TODO: this only gets the type for variables with explicit defined type: we don't process "var"
            MethodDeclarationSyntax method = node.GetLocation().GetContainingMethod();
            var parameter = method.ParameterList.Parameters.FirstOrDefault(x => x.Identifier.Text == rootToken);
            string typeName;

            if (parameter != null) {
                typeName = parameter.Type.ToString();
            } else {
                var localDeclaration = method
                    .DescendantNodes<LocalDeclarationStatementSyntax>()
                    .FirstOrDefault(x => x.Declaration.Variables[0].Identifier.Text == rootToken);

                if (localDeclaration != null) {
                    typeName = localDeclaration.Declaration.Type.ToString();
                } else {
                    throw new NotSupportedException("The type name was not found");
                }
            }

            return typeName;
        }

        private Type GetType(string typeName)
        {
            //todo: there can be multiple classes with the same name
            Type type = solutionTypes.FirstOrDefault(x => x.Name==typeName);

            return type ?? coreTypeAliases[typeName];
        }

        #endregion
    }
}