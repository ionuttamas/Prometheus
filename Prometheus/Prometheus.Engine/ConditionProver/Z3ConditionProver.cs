using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.ReachabilityProver;
using Prometheus.Engine.Types;

namespace Prometheus.Engine.ConditionProver
{
    internal class Z3ConditionProver : IConditionProver
    {
        private readonly ITypeService typeService;
        private HaveCommonReference reachabilityDelegate;
        private readonly Context context;

        public Z3ConditionProver(ITypeService typeService)
        {
            this.typeService = typeService;
            context = new Context();
        }

        public void Configure(HaveCommonReference @delegate)
        {
            reachabilityDelegate = @delegate;
        }

        public bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second)
        {
            BoolExpr firstCondition = ParseConditionalAssignment(first, out var processedMembers);
            BoolExpr secondCondition = ParseConditionalAssignment(second, processedMembers);

            Solver solver = context.MkSolver();
            solver.Assert(firstCondition, secondCondition);
            Status status = solver.Check();

            return status == Status.SATISFIABLE;
        }

        public void Dispose() {
            context.Dispose();
        }

        #region Non-cached processing

        private BoolExpr ParseConditionalAssignment(ConditionalAssignment assignment, out Dictionary<string, NodeType> processedMembers) {
            List<BoolExpr> conditions = new List<BoolExpr>();

            processedMembers = new Dictionary<string, NodeType>();

            foreach (var assignmentCondition in assignment.Conditions) {
                var boolExpr = ParseExpression(assignmentCondition.IfStatement.Condition, out var membersTable);
                processedMembers.Merge(membersTable);

                conditions.Add(assignmentCondition.IsNegated ? context.MkNot(boolExpr) : boolExpr);
            }

            BoolExpr expression = context.MkAnd(conditions.ToArray());

            return expression;
        }

        private BoolExpr ParseExpression(ExpressionSyntax expressionSyntax, out Dictionary<string, NodeType> processedMembers) {
            var expressionKind = expressionSyntax.Kind();

            switch (expressionKind) {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression((PrefixUnaryExpressionSyntax)expressionSyntax, out processedMembers);
                case SyntaxKind.SimpleMemberAccessExpression:
                    processedMembers = new Dictionary<string, NodeType>();
                    return context.MkBoolConst(expressionSyntax.ToString());
            }

            var binaryExpression = (BinaryExpressionSyntax)expressionSyntax;

            switch (expressionKind) {
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

        private BoolExpr ParseBinaryExpression(BinaryExpressionSyntax binaryExpression, out Dictionary<string, NodeType> processedMembers) {
            SyntaxKind expressionKind = binaryExpression.Kind();
            Expr left = ParseExpressionMember(binaryExpression.Left, out var leftProcessedMembers);
            Expr right = ParseExpressionMember(binaryExpression.Right, out var rightProcessedMembers);

            processedMembers = new Dictionary<string, NodeType>();
            processedMembers.Merge(leftProcessedMembers);
            processedMembers.Merge(rightProcessedMembers);

            switch (expressionKind) {
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

        private BoolExpr ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, out Dictionary<string, NodeType> processedMembers) {
            if (prefixUnaryExpression.Kind() == SyntaxKind.LogicalNotExpression) {
                var innerExpression = prefixUnaryExpression.Operand;
                var parsedExpression = ParseExpression(innerExpression, out processedMembers);
                return context.MkNot(parsedExpression);
            }

            throw new NotImplementedException();
        }

        private Expr ParseExpressionMember(ExpressionSyntax memberExpression, out Dictionary<string, NodeType> processedMembers) {
            var expressionKind = memberExpression.Kind();

            if (memberExpression is BinaryExpressionSyntax) {
                return ParseBinaryExpression((BinaryExpressionSyntax)memberExpression, out processedMembers);
            }

            if (expressionKind == SyntaxKind.NumericLiteralExpression) {
                processedMembers = new Dictionary<string, NodeType>();
                return ParseNumericLiteral(memberExpression.ToString());
            }

            if (expressionKind == SyntaxKind.StringLiteralExpression) {
                processedMembers = new Dictionary<string, NodeType>();
                return ParseStringLiteral(memberExpression.ToString());
            }

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName) {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseUnaryExpression(memberExpression, out processedMembers)
                    : ParseExpression(memberExpression, out processedMembers);
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression) {
                return ParseUnaryExpression(memberExpression, out processedMembers);
            }

            var memberType = typeService.GetType(memberExpression);
            processedMembers = new Dictionary<string, NodeType>();

            //TODO: check nested reference chains from different chains: "customer.Address.ShipInfo" & "order.ShipInfo" to be the same
            //TODO: check agains same reference chains: "from.Address.ShipInfo" & "to.Address.ShipInfo"
            //Check against the nodes from already parsed the first conditional assignment
            return ParseVariableExpression(memberExpression, memberType, processedMembers);
        }

        private Expr ParseUnaryExpression(ExpressionSyntax unaryExpression, out Dictionary<string, NodeType> processedMembers) {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)unaryExpression;
            var negatedExpression = ParseExpressionMember(prefixUnaryExpression.Operand, out processedMembers);

            return context.MkUnaryMinus((ArithExpr)negatedExpression);
        }

        private Expr ParseVariableExpression(ExpressionSyntax memberExpression, Type type, Dictionary<string, NodeType> processedMembers) {
            string memberName = memberExpression.ToString();
            Expr constExpr = context.MkConst(memberName, context.RealSort);
            processedMembers[memberName] = new NodeType {
                Expression = constExpr,
                Node = memberExpression,
                Type = type
            };

            return constExpr;
        }

        private Expr ParseNumericLiteral(string numericLiteral) {
            Sort sort = int.TryParse(numericLiteral, out int _) ? context.IntSort : (Sort)context.RealSort;
            return context.MkNumeral(numericLiteral, context.RealSort); //TODO: issue on real>int expression
        }

        private Expr ParseStringLiteral(string stringLiteral) {
            return null;//context.mk.MkString(stringLiteral);
        }

        #endregion

        #region Cached processing

        private BoolExpr ParseConditionalAssignment(ConditionalAssignment assignment, Dictionary<string, NodeType> cachedMembers) {
            BoolExpr[] conditions =
                assignment.Conditions.Select(
                    x =>
                        x.IsNegated
                            ? context.MkNot(ParseExpression(x.IfStatement.Condition, cachedMembers))
                            : ParseExpression(x.IfStatement.Condition, cachedMembers)).ToArray();
            BoolExpr expression = context.MkAnd(conditions);

            return expression;
        }

        private BoolExpr ParseExpression(ExpressionSyntax expressionSyntax, Dictionary<string, NodeType> cachedMembers) {
            var expressionKind = expressionSyntax.Kind();

            switch (expressionKind) {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression((PrefixUnaryExpressionSyntax)expressionSyntax, cachedMembers);
                case SyntaxKind.SimpleMemberAccessExpression:
                    return context.MkBoolConst(expressionSyntax.ToString());
            }

            var binaryExpression = (BinaryExpressionSyntax)expressionSyntax;

            switch (expressionKind) {
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

        private BoolExpr ParseBinaryExpression(BinaryExpressionSyntax binaryExpression, Dictionary<string, NodeType> cachedMembers) {
            SyntaxKind expressionKind = binaryExpression.Kind();
            Expr left = ParseExpressionMember(binaryExpression.Left, cachedMembers);
            Expr right = ParseExpressionMember(binaryExpression.Right, cachedMembers);

            switch (expressionKind) {
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

        private BoolExpr ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, Dictionary<string, NodeType> cachedMembers) {
            if (prefixUnaryExpression.Kind() == SyntaxKind.LogicalNotExpression) {
                var innerExpression = prefixUnaryExpression.Operand;
                var parsedExpression = ParseExpression(innerExpression, cachedMembers);
                return context.MkNot(parsedExpression);
            }

            throw new NotImplementedException();
        }

        private Expr ParseExpressionMember(ExpressionSyntax memberExpression, Dictionary<string, NodeType> cachedMembers) {
            var expressionKind = memberExpression.Kind();

            if (memberExpression is BinaryExpressionSyntax) {
                return ParseBinaryExpression((BinaryExpressionSyntax)memberExpression, cachedMembers);
            }

            if (expressionKind == SyntaxKind.NumericLiteralExpression) {
                return ParseNumericLiteral(memberExpression.ToString());
            }

            if (expressionKind == SyntaxKind.StringLiteralExpression) {
                return ParseStringLiteral(memberExpression.ToString());
            }

            if (expressionKind == SyntaxKind.StringLiteralExpression) {
                return ParseStringLiteral(memberExpression.ToString());
            }

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName) {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseUnaryExpression(memberExpression, cachedMembers)
                    : ParseExpression(memberExpression, cachedMembers);
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression) {
                return ParseUnaryExpression(memberExpression, cachedMembers);
            }

            var memberType = typeService.GetType(memberExpression);
            var memberReference = new Reference(memberExpression);

            foreach (NodeType node in cachedMembers.Values.Where(x => x.Type == memberType)) {
                //TODO: MAJOR
                //TODO: for now, we only match "amount1" with "amount2" (identifier with identifier) or "[from].AccountBalance" with "[from2].AccountBalance"
                //TODO: need to extend to "amount" with "[from].AccountBalance" and other combinations
                if (node.Node is IdentifierNameSyntax && memberExpression is IdentifierNameSyntax) {
                    if (reachabilityDelegate(new Reference(node.Node), memberReference, out Reference _))
                        return node.Expression;
                }

                if (node.Node is MemberAccessExpressionSyntax && memberExpression is MemberAccessExpressionSyntax) {
                    var firstMember = (MemberAccessExpressionSyntax)node.Node;
                    var secondMember = (MemberAccessExpressionSyntax)memberExpression;
                    var firstRootReference = new Reference(GetRootIdentifier(firstMember));
                    var secondRootReference = new Reference(GetRootIdentifier(secondMember));

                    if (reachabilityDelegate(firstRootReference, secondRootReference, out Reference _))
                        return node.Expression;
                }
            }

            return ParseVariableExpression(memberExpression, cachedMembers);
        }

        private Expr ParseUnaryExpression(ExpressionSyntax unaryExpression, Dictionary<string, NodeType> cachedMembers) {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)unaryExpression;
            var negatedExpression = ParseExpressionMember(prefixUnaryExpression.Operand, cachedMembers);

            return context.MkUnaryMinus((ArithExpr)negatedExpression);
        }

        private Expr ParseVariableExpression(ExpressionSyntax memberExpression, Dictionary<string, NodeType> cachedMembers) {
            string memberName = memberExpression.ToString();

            if (cachedMembers.ContainsKey(memberName)) {
                return cachedMembers[memberName].Expression;
            }

            var constExpr = context.MkConst(memberName, context.RealSort);

            return constExpr;
        }

        #endregion

        /// <summary>
        /// For a member access expression such as "person.Address.Street" returns "person" as root identifier.
        /// </summary>
        private IdentifierNameSyntax GetRootIdentifier(MemberAccessExpressionSyntax memberAccess) {
            string rootToken = memberAccess.ToString().Split('.').First();
            var identifier = memberAccess.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == rootToken).First();

            return identifier;
        }

        private class NodeType {
            public SyntaxNode Node { get; set; }
            public Expr Expression { get; set; }
            public Type Type { get; set; }
        }
    }
}