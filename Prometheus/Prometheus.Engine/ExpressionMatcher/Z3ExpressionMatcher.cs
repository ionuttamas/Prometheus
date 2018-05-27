using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.ConditionProver;
using Prometheus.Engine.Reachability.Model.Query;
using Prometheus.Engine.ReachabilityProver.Model;
using Prometheus.Engine.Types;

namespace Prometheus.Engine.ExpressionMatcher
{
    internal class Z3QueryMatcher : IQueryMatcher {
        private readonly ITypeService typeService;
        private readonly Context context;

        public Z3QueryMatcher(ITypeService typeService)
        {
            this.typeService = typeService;
            context = new Context();
        }

        public void Dispose() {
            context.Dispose();
        }

        /// <summary>
        /// Checks whether it is structurally equivalent to another query.
        /// It handles commutativity and other variations for clauses such as: "x + y ≡ c + d" or "x.Age > 30 && x.Balance > 100 ≡ y.Balance > 100 && y.Age > 30"
        /// </summary>
        public bool AreEquivalent(IReferenceQuery first, IReferenceQuery second, out Dictionary<SyntaxNode, SyntaxNode> satisfiableTable)
        {
            var type = first.GetType();

            if (type != second.GetType())
                throw new ArgumentException($"Cannot compare reference queries of type {type} and {second.GetType()}");

            if (first is PredicateExpressionQuery)
            {
                return ArePredicateLambdasEquivalent(first.As<PredicateExpressionQuery>().Predicate,
                    second.As<PredicateExpressionQuery>().Predicate, out satisfiableTable);
            }

            if (first is IndexArgumentQuery) {
                return AreGeneralExpressionsEquivalent(first.As<IndexArgumentQuery>().Argument.Expression,
                    second.As<IndexArgumentQuery>().Argument.Expression, out satisfiableTable);
            }

            throw new ArgumentException($"{type} reference query is currently not supported");
        }

        private bool AreGeneralExpressionsEquivalent(ExpressionSyntax first, ExpressionSyntax second, out Dictionary<SyntaxNode, SyntaxNode> satisfiableTable) {
            //todo: this is more tricky given multiple return types
            satisfiableTable = null;
            return false;
        }

        private bool ArePredicateLambdasEquivalent(SimpleLambdaExpressionSyntax first, SimpleLambdaExpressionSyntax second, out Dictionary<SyntaxNode, SyntaxNode> satisfiableTable)
        {
            var firstParameter = first.Parameter;
            BoolExpr firstExpression = ParseExpression(first.Body.As<ExpressionSyntax>(), out var processedMembers);
            satisfiableTable = new Dictionary<SyntaxNode, SyntaxNode>();


            List<SyntaxNode> sourceCapturedVariables = GetCapturedVariables(first);
            List<SyntaxNode> targetCapturedVariables = GetCapturedVariables(second);

            if (sourceCapturedVariables.Count != targetCapturedVariables.Count)
                return false;

            foreach (IEnumerable<SyntaxNode> permutation in GetPermutations(sourceCapturedVariables))
            {
                var variablesMapping = permutation.Select((x, ix) => new {Index = ix, Node = x}).ToDictionary(x => x.Node, x => targetCapturedVariables[x.Index]);
                var predicateRewriter = new PredicateRewriter(firstParameter, variablesMapping);
                var rewrittenLambda = predicateRewriter.Visit(second).As<SimpleLambdaExpressionSyntax>();

                BoolExpr secondExpression = ParseExpression(rewrittenLambda.Body.As<ExpressionSyntax>(), processedMembers);
                Solver solver = context.MkSolver();
                BoolExpr expression = context.MkEq(firstExpression, secondExpression);
                solver.Assert(expression);
                Status status = solver.Check();

                if (status == Status.SATISFIABLE)
                {
                    satisfiableTable = variablesMapping;
                    return true;
                }
            }

            return false;
        }

        #region Non-cached processing

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
                    //todo: fix comparison expression for string expressions
                    return context.MkGt((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return context.MkGe((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.LessThanExpression:
                    return context.MkLt((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.LessThanOrEqualExpression:
                    return context.MkLe((ArithExpr)left, (ArithExpr)right);

                //TODO: Fix this: since this is only for numeric values
                case SyntaxKind.EqualsExpression:
                    return context.MkEq(left, right);
                case SyntaxKind.NotEqualsExpression:
                    return context.MkNot(context.MkEq(left, right));
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
            Sort sort = typeService.GetSort(context, type);
            Expr constExpr = context.MkConst(memberName, sort);

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
            return context.MkString(stringLiteral);
        }

        #endregion

        #region Cached processing

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
                    return context.MkEq(left, right);
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

            return ParseCachedVariableExpression(memberExpression, memberType, cachedMembers);
        }

        private Expr ParseUnaryExpression(ExpressionSyntax unaryExpression, Dictionary<string, NodeType> cachedMembers) {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)unaryExpression;
            var negatedExpression = ParseExpressionMember(prefixUnaryExpression.Operand, cachedMembers);

            return context.MkUnaryMinus((ArithExpr)negatedExpression);
        }

        private Expr ParseCachedVariableExpression(ExpressionSyntax memberExpression, Type type, Dictionary<string, NodeType> cachedMembers) {
            string memberName = memberExpression.ToString();

            if (cachedMembers.ContainsKey(memberName)) {
                return cachedMembers[memberName].Expression;
            }

            var constExpr = context.MkConst(memberName, typeService.GetSort(context, type));

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

        private List<SyntaxNode> GetCapturedVariables(SimpleLambdaExpressionSyntax lambda)
        {
            var parameter = lambda.Parameter;
            //TODO: currently we support only IdentifierNameSyntax
            var capturedVariables = lambda
                .Body
                .DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text != parameter.Identifier.Text)
                .Select(x=>x.As<SyntaxNode>())
                .DistinctBy(x=>x.ToString())
                .ToList();

            return capturedVariables;
        }

        private static IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> collection)
        {
            return GetPermutations(collection, collection.Count());
        }

        private static IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> collection, int length) {
            if (length == 1)
                return collection.Select(x => new[] { x });

            var list = collection.ToList();
            var result = GetPermutations(list, length - 1)
                        .SelectMany(x => list.Where(e => !x.Contains(e)), (t1, t2) => t1.Concat(new[] { t2 }));

            return result;
        }

        private class NodeType {
            public SyntaxNode Node { get; set; }
            public Expr Expression { get; set; }
            public Type Type { get; set; }
        }
    }
}