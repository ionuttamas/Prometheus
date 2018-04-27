using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.Types;
using TypeInfo = System.Reflection.TypeInfo;

namespace Prometheus.Engine.ReachabilityProver
{
    internal class ReachabilityProver : IDisposable
    {
        private readonly ReferenceTracker referenceTracker;
        private readonly ITypeService typeService;
        private readonly ReachabilityCache reachabilityCache;
        private readonly Context context;

        public ReachabilityProver(ReferenceTracker referenceTracker, ITypeService typeService)
        {
            this.referenceTracker = referenceTracker;
            this.typeService = typeService;
            context = new Context();
            reachabilityCache = new ReachabilityCache();
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
            return CheckReachability(firstAssignment, secondAssignment, out commonNode);
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
            return CheckReachability(firstAssignment, secondAssignment, out commonNode);
        }

        private bool CheckReachability(ConditionalAssignment first, ConditionalAssignment second, out object commonNode)
        {
            commonNode = null;

            if (reachabilityCache.Contains(first.AssignmentLocation, second.AssignmentLocation))
            {
                commonNode = reachabilityCache.GetFromCache(first.AssignmentLocation, second.AssignmentLocation);
                return commonNode!=null && IsSatisfiable(first, second);
            }

            if (!IsSatisfiable(first, second))
            {
                reachabilityCache.AddToCache(first.AssignmentLocation, second.AssignmentLocation, null);
                return false;
            }

            if (AreEquivalent(first, second))
            {
                commonNode = first.NodeReference ?? (object) first.TokenReference;
                reachabilityCache.AddToCache(first.AssignmentLocation, second.AssignmentLocation, commonNode);
                return true;
            }

            List<ConditionalAssignment> firstAssignments = referenceTracker.GetAssignments(first.NodeReference?.DescendantTokens().First() ?? first.TokenReference);
            List<ConditionalAssignment> secondAssignments = referenceTracker.GetAssignments(second.NodeReference?.DescendantTokens().First() ?? second.TokenReference);

            if (!firstAssignments.Any() && !secondAssignments.Any())
            {
                reachabilityCache.AddToCache(first.AssignmentLocation, second.AssignmentLocation, null);
                return false;
            }

            foreach (ConditionalAssignment assignment in firstAssignments)
            {
                assignment.Conditions.UnionWith(first.Conditions);
            }

            foreach (ConditionalAssignment assignment in secondAssignments)
            {
                assignment.Conditions.UnionWith(second.Conditions);
            }

            foreach (var firstAssignment in firstAssignments)
            {
                if (CheckReachability(firstAssignment, second, out commonNode))
                    return true;
            }

            foreach (var secondAssignment in secondAssignments)
            {
                if (CheckReachability(first, secondAssignment, out commonNode))
                    return true;
            }

            return false;
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

        private class NodeType
        {
            public SyntaxNode Node { get; set; }
            public Expr Expression { get; set; }
            public List<Type> TypeChain { get; set; }
            public Type Type => TypeChain.Last();
        }

        private bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second)
        {
            BoolExpr firstCondition = ParseConditionalAssignment(first, out var processedMembers);
            BoolExpr secondCondition = ParseConditionalAssignment(second, processedMembers);

            Solver solver = context.MkSolver();
            solver.Assert(firstCondition, secondCondition);
            Status status = solver.Check();

            return status == Status.SATISFIABLE;
        }

        #region Non-cached processing

        private BoolExpr ParseConditionalAssignment(ConditionalAssignment assignment, out Dictionary<string, NodeType> processedMembers)
        {
            List<BoolExpr> conditions = new List<BoolExpr>();

            processedMembers = new Dictionary<string, NodeType>();

            foreach (var assignmentCondition in assignment.Conditions)
            {
                var boolExpr = ParseExpression(assignmentCondition.IfStatement.Condition, out var membersTable);
                processedMembers.Merge(membersTable);

                conditions.Add(assignmentCondition.IsNegated ? context.MkNot(boolExpr) : boolExpr);
            }

            BoolExpr expression = context.MkAnd(conditions.ToArray());

            return expression;
        }

        private BoolExpr ParseExpression(ExpressionSyntax expressionSyntax, out Dictionary<string, NodeType> processedMembers)
        {
            var expressionKind = expressionSyntax.Kind();

            switch (expressionKind)
            {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression((PrefixUnaryExpressionSyntax)expressionSyntax, out processedMembers);
                case SyntaxKind.SimpleMemberAccessExpression:
                    processedMembers = new Dictionary<string, NodeType>();
                    return context.MkBoolConst(expressionSyntax.ToString());
            }

            var binaryExpression = (BinaryExpressionSyntax)expressionSyntax;

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
                    return ParseBinaryExpression(binaryExpression, out processedMembers);
                default:
                    throw new NotImplementedException();
            }

        }

        private BoolExpr ParseBinaryExpression(BinaryExpressionSyntax binaryExpression, out Dictionary<string, NodeType> processedMembers)
        {
            SyntaxKind expressionKind = binaryExpression.Kind();
            Expr left = ParseExpressionMember(binaryExpression.Left, out var leftProcessedMembers);
            Expr right = ParseExpressionMember(binaryExpression.Right, out var rightProcessedMembers);

            processedMembers = new Dictionary<string, NodeType>();
            processedMembers.Merge(leftProcessedMembers);
            processedMembers.Merge(rightProcessedMembers);

            switch (expressionKind)
            {
                case SyntaxKind.LogicalAndExpression:
                    return context.MkAnd((BoolExpr)left, (BoolExpr)right);
                case SyntaxKind.LogicalOrExpression:
                    return context.MkOr((BoolExpr)left, (BoolExpr)right);
                case SyntaxKind.GreaterThanExpression:
                    return context.MkGt((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return context.MkGe((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.LessThanExpression:
                    return context.MkLt((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.LessThanOrEqualExpression:
                    return context.MkLe((ArithExpr)left, (ArithExpr)right);

                //TODO: Fix this: since this is only for numeric values
                case SyntaxKind.EqualsExpression:
                    return context.MkEq((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.NotEqualsExpression:
                    return context.MkNot(context.MkEq((ArithExpr)left, (ArithExpr)right));
                default:
                    throw new NotImplementedException();
            }
        }

        private BoolExpr ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, out Dictionary<string, NodeType> processedMembers)
        {
            if (prefixUnaryExpression.Kind() == SyntaxKind.LogicalNotExpression)
            {
                var innerExpression = prefixUnaryExpression.Operand;
                var parsedExpression = ParseExpression(innerExpression, out processedMembers);
                return context.MkNot(parsedExpression);
            }

            throw new NotImplementedException();
        }

        private Expr ParseExpressionMember(ExpressionSyntax memberExpression, out Dictionary<string, NodeType> processedMembers)
        {
            var expressionKind = memberExpression.Kind();

            if (memberExpression is BinaryExpressionSyntax)
            {
                return ParseBinaryExpression((BinaryExpressionSyntax)memberExpression, out processedMembers);
            }

            if (expressionKind == SyntaxKind.NumericLiteralExpression)
            {
                processedMembers = new Dictionary<string, NodeType>();
                return ParseNumericLiteral(memberExpression.ToString());
            }

            if (expressionKind == SyntaxKind.StringLiteralExpression)
            {
                processedMembers = new Dictionary<string, NodeType>();
                return ParseStringLiteral(memberExpression.ToString());
            }

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName)
            {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseUnaryExpression(memberExpression, out processedMembers)
                    : ParseExpression(memberExpression, out processedMembers);
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression)
            {
                return ParseUnaryExpression(memberExpression, out processedMembers);
            }

            var typeChain = expressionKind == SyntaxKind.SimpleMemberAccessExpression
                ? GetNodeTypes((MemberAccessExpressionSyntax)memberExpression)
                : new List<Type> { GetNodeType((IdentifierNameSyntax)memberExpression) };

            processedMembers = new Dictionary<string, NodeType>();

            //TODO: check nested reference chains from different chains: "customer.Address.ShipInfo" & "order.ShipInfo" to be the same
            //TODO: check agains same reference chains: "from.Address.ShipInfo" & "to.Address.ShipInfo"
            //Check against the nodes from already parsed the first conditional assignment
            return ParseVariableExpression(memberExpression, typeChain, processedMembers);
        }

        private Expr ParseUnaryExpression(ExpressionSyntax unaryExpression, out Dictionary<string, NodeType> processedMembers)
        {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)unaryExpression;
            var negatedExpression = ParseExpressionMember(prefixUnaryExpression.Operand, out processedMembers);

            return context.MkUnaryMinus((ArithExpr)negatedExpression);
        }

        private Expr ParseVariableExpression(ExpressionSyntax memberExpression, List<Type> typeChain, Dictionary<string, NodeType> processedMembers)
        {
            string memberName = memberExpression.ToString();
            Expr constExpr = context.MkConst(memberName, context.RealSort);
            processedMembers[memberName] = new NodeType
            {
                Expression = constExpr,
                Node = memberExpression,
                TypeChain = typeChain
            };

            return constExpr;
        }

        private Expr ParseNumericLiteral(string numericLiteral)
        {
            Sort sort = int.TryParse(numericLiteral, out int _) ? context.IntSort : (Sort)context.RealSort;
            return context.MkNumeral(numericLiteral, context.RealSort); //TODO: issue on real>int expression
        }

        private Expr ParseStringLiteral(string stringLiteral)
        {
            return context.MkString(stringLiteral);
        }

        #endregion

        #region Cached processing

        private BoolExpr ParseConditionalAssignment(ConditionalAssignment assignment, Dictionary<string, NodeType> cachedMembers)
        {
            BoolExpr[] conditions =
                assignment.Conditions.Select(
                    x =>
                        x.IsNegated
                            ? context.MkNot(ParseExpression(x.IfStatement.Condition, cachedMembers))
                            : ParseExpression(x.IfStatement.Condition, cachedMembers)).ToArray();
            BoolExpr expression = context.MkAnd(conditions);

            return expression;
        }

        private BoolExpr ParseExpression(ExpressionSyntax expressionSyntax, Dictionary<string, NodeType> cachedMembers)
        {
            var expressionKind = expressionSyntax.Kind();

            switch (expressionKind)
            {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression((PrefixUnaryExpressionSyntax)expressionSyntax, cachedMembers);
                case SyntaxKind.SimpleMemberAccessExpression:
                    return context.MkBoolConst(expressionSyntax.ToString());
            }

            var binaryExpression = (BinaryExpressionSyntax)expressionSyntax;

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
                    return ParseBinaryExpression(binaryExpression, cachedMembers);
                default:
                    throw new NotImplementedException();
            }

        }

        private BoolExpr ParseBinaryExpression(BinaryExpressionSyntax binaryExpression, Dictionary<string, NodeType> cachedMembers)
        {
            SyntaxKind expressionKind = binaryExpression.Kind();
            Expr left = ParseExpressionMember(binaryExpression.Left, cachedMembers);
            Expr right = ParseExpressionMember(binaryExpression.Right, cachedMembers);

            switch (expressionKind)
            {
                case SyntaxKind.LogicalAndExpression:
                    return context.MkAnd((BoolExpr)left, (BoolExpr)right);
                case SyntaxKind.LogicalOrExpression:
                    return context.MkOr((BoolExpr)left, (BoolExpr)right);
                case SyntaxKind.GreaterThanExpression:
                    return context.MkGt((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return context.MkGe((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.LessThanExpression:
                    return context.MkLt((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.LessThanOrEqualExpression:
                    return context.MkLe((ArithExpr)left, (ArithExpr)right);

                //TODO: Fix this: since this is only for numeric values
                case SyntaxKind.EqualsExpression:
                    return context.MkEq((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.NotEqualsExpression:
                    return context.MkNot(context.MkEq((ArithExpr)left, (ArithExpr)right));
                default:
                    throw new NotImplementedException();
            }
        }

        private BoolExpr ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, Dictionary<string, NodeType> cachedMembers)
        {
            if (prefixUnaryExpression.Kind() == SyntaxKind.LogicalNotExpression)
            {
                var innerExpression = prefixUnaryExpression.Operand;
                var parsedExpression = ParseExpression(innerExpression, cachedMembers);
                return context.MkNot(parsedExpression);
            }

            throw new NotImplementedException();
        }

        private Expr ParseExpressionMember(ExpressionSyntax memberExpression, Dictionary<string, NodeType> cachedMembers)
        {
            var expressionKind = memberExpression.Kind();

            if (memberExpression is BinaryExpressionSyntax)
            {
                return ParseBinaryExpression((BinaryExpressionSyntax)memberExpression, cachedMembers);
            }

            if (expressionKind == SyntaxKind.NumericLiteralExpression)
            {
                return ParseNumericLiteral(memberExpression.ToString());
            }

            if (expressionKind == SyntaxKind.StringLiteralExpression)
            {
                return ParseStringLiteral(memberExpression.ToString());
            }

            if (expressionKind == SyntaxKind.StringLiteralExpression)
            {
                return ParseStringLiteral(memberExpression.ToString());
            }

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName)
            {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseUnaryExpression(memberExpression, cachedMembers)
                    : ParseExpression(memberExpression, cachedMembers);
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression)
            {
                return ParseUnaryExpression(memberExpression, cachedMembers);
            }

            var typeChain = expressionKind == SyntaxKind.SimpleMemberAccessExpression
                ? GetNodeTypes((MemberAccessExpressionSyntax)memberExpression)
                : new List<Type> { GetNodeType((IdentifierNameSyntax)memberExpression) };
            var nodeType = typeChain.Last();

            foreach (NodeType node in cachedMembers.Values.Where(x => x.Type == nodeType))
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
                    var firstMember = (MemberAccessExpressionSyntax)node.Node;
                    var secondMember = (MemberAccessExpressionSyntax)memberExpression;

                    if (HaveCommonValue(GetRootIdentifier(firstMember), GetRootIdentifier(secondMember), out object _))
                        return node.Expression;
                }
            }

            return ParseVariableExpression(memberExpression, cachedMembers);
        }

        private Expr ParseUnaryExpression(ExpressionSyntax unaryExpression, Dictionary<string, NodeType> cachedMembers)
        {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)unaryExpression;
            var negatedExpression = ParseExpressionMember(prefixUnaryExpression.Operand, cachedMembers);

            return context.MkUnaryMinus((ArithExpr)negatedExpression);
        }

        private Expr ParseVariableExpression(ExpressionSyntax memberExpression, Dictionary<string, NodeType> cachedMembers)
        {
            string memberName = memberExpression.ToString();

            if (cachedMembers.ContainsKey(memberName))
            {
                return cachedMembers[memberName].Expression;
            }

            var constExpr = context.MkConst(memberName, context.RealSort);

            return constExpr;
        }

        #endregion

        #endregion

        #region Type retrieval

        private IdentifierNameSyntax GetRootIdentifier(MemberAccessExpressionSyntax memberAccess)
        {
            var rootToken = memberAccess.ToString().Split('.').First();
            var identifier = memberAccess.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == rootToken).First();

            return identifier;
        }

        private List<Type> GetNodeTypes(MemberAccessExpressionSyntax memberExpression) {
            Queue<string> memberTokens = new Queue<string>(memberExpression.ToString().Split('.'));
            string rootToken = memberTokens.First();
            var typeName = GetTypeName(memberExpression.GetContainingMethod(), rootToken);
            memberTokens.Dequeue();
            var types = new List<Type>();

            //todo: there can be multiple classes with the same name
            Type rootType = typeService.GetType(typeName);
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

        private Type GetNodeType(IdentifierNameSyntax identifierNameSyntax) {
            string typeName = GetTypeName(identifierNameSyntax.GetContainingMethod(), identifierNameSyntax.Identifier.Text);
            Type type = typeService.GetType(typeName);

            return type;
        }

        private string GetTypeName(MethodDeclarationSyntax containingMethod, string rootToken) {
            //TODO: this only gets the type for variables with explicit defined type: we don't process "var"
            var parameter = containingMethod.ParameterList.Parameters.FirstOrDefault(x => x.Identifier.Text == rootToken);
            string typeName;

            if (parameter != null) {
                typeName = parameter.Type.ToString();
            } else {
                var localDeclaration = containingMethod
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

        #endregion
    }
}