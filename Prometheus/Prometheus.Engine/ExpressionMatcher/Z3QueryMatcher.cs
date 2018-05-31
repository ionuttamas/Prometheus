using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.ExpressionMatcher.Rewriters;
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

        public bool AreEquivalent(IReferenceQuery first, IReferenceQuery second, out Dictionary<SyntaxNode, SyntaxNode> satisfiableTable)
        {
            var type = first.GetType();

            if (type != second.GetType())
                throw new ArgumentException($"Cannot compare reference queries of type {type} and {second.GetType()}");

            if (first is PredicateExpressionQuery)
            {
                return ArePredicateLambdasEquivalent(first.As<PredicateExpressionQuery>().Predicate,
                                                     second.As<PredicateExpressionQuery>().Predicate,
                                                     out satisfiableTable);
            }

            if (first is IndexArgumentQuery) {
                return AreGeneralExpressionsEquivalent(first.As<IndexArgumentQuery>().Argument.Expression,
                                                       second.As<IndexArgumentQuery>().Argument.Expression,
                                                       out satisfiableTable);
            }

            throw new ArgumentException($"{type} reference query is currently not supported");
        }

        private bool AreGeneralExpressionsEquivalent(ExpressionSyntax first, ExpressionSyntax second, out Dictionary<SyntaxNode, SyntaxNode> satisfiableTable) {
            //todo: this is more tricky given multiple return types
            satisfiableTable = null;
            List<SyntaxNode> sourceVariables = GetVariables(first);
            List<SyntaxNode> targetVariables = GetVariables(second);

            if (sourceVariables.Count != targetVariables.Count)
                return false;

            Expr firstExpression = ParseExpression(first.As<ExpressionSyntax>(), out var processedMembers);

            foreach (IEnumerable<SyntaxNode> permutation in GetPermutations(sourceVariables))
            {
                var sourcePermutation = permutation.ToList();
                var variablesMap = targetVariables.Select((x, ix) => new { Index = ix, Node = x }).ToDictionary(x => x.Node, x => sourcePermutation[x.Index]);
                var rewriter = new ExpressionRewriter(variablesMap);
                var rewrittenExpression = rewriter.Visit(second).As<ExpressionSyntax>();

                var transformation = new ExpressionTransformation
                {
                    OriginalExpression = second,
                    RewrittenExpression = rewrittenExpression,
                    VariablesMap = variablesMap
                };

                Expr secondExpression = ParseExpression(transformation, processedMembers);
                Solver solver = context.MkSolver();
                BoolExpr equalityExpression = context.MkEq(firstExpression, secondExpression);
                solver.Assert(equalityExpression);
                Status status = solver.Check();

                if (status == Status.SATISFIABLE) {
                    satisfiableTable = variablesMap;
                    return true;
                }
            }

            return false;
        }

        private bool ArePredicateLambdasEquivalent(SimpleLambdaExpressionSyntax first, SimpleLambdaExpressionSyntax second, out Dictionary<SyntaxNode, SyntaxNode> satisfiableTable)
        {
            satisfiableTable = null;
            List<SyntaxNode> sourceCapturedVariables = GetCapturedVariables(first);
            List<SyntaxNode> targetCapturedVariables = GetCapturedVariables(second);

            if (sourceCapturedVariables.Count != targetCapturedVariables.Count)
                return false;

            var firstParameter = first.Parameter;
            Expr firstExpression = ParseExpression(first.Body.As<ExpressionSyntax>(), out var processedMembers);

            foreach (IEnumerable<SyntaxNode> permutation in GetPermutations(sourceCapturedVariables))
            {
                var sourcePermutation = permutation.ToList();
                var variablesMapping = targetCapturedVariables.Select((x, ix) => new {Index = ix, Node = x}).ToDictionary(x => x.Node, x => sourcePermutation[x.Index]);
                var predicateRewriter = new PredicateRewriter(firstParameter, variablesMapping);
                var rewrittenLambda = predicateRewriter.Visit(second).As<SimpleLambdaExpressionSyntax>();

                Expr secondExpression = ParseExpression(rewrittenLambda.Body.As<ExpressionSyntax>(), processedMembers);
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

        private Expr ParseExpression(ExpressionSyntax expressionSyntax, out Dictionary<string, NodeType> processedMembers) {
            var expressionKind = expressionSyntax.Kind();

            switch (expressionKind) {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression((PrefixUnaryExpressionSyntax)expressionSyntax, out processedMembers);
                case SyntaxKind.SimpleMemberAccessExpression:
                    processedMembers = new Dictionary<string, NodeType>();
                    return context.MkConst(expressionSyntax.ToString(), typeService.GetSort(context, typeService.GetType(expressionSyntax)));
                case SyntaxKind.ParenthesizedExpression:
                    return ParseExpression(expressionSyntax.As<ParenthesizedExpressionSyntax>().Expression, out processedMembers);
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
                case SyntaxKind.AddExpression:
                case SyntaxKind.SubtractExpression:
                case SyntaxKind.DivideExpression:
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.ModuloExpression:
                    return ParseBinaryExpression(binaryExpression, out processedMembers);
                default:
                    throw new NotImplementedException();
            }

        }

        private Expr ParseBinaryExpression(BinaryExpressionSyntax binaryExpression, out Dictionary<string, NodeType> processedMembers) {
            SyntaxKind expressionKind = binaryExpression.Kind();
            Expr left = ParseExpressionMember(binaryExpression.Left, out var leftProcessedMembers);
            Expr right = ParseExpressionMember(binaryExpression.Right, out var rightProcessedMembers);

            processedMembers = new Dictionary<string, NodeType>();
            processedMembers.Merge(leftProcessedMembers);
            processedMembers.Merge(rightProcessedMembers);

            switch (expressionKind) {
                case SyntaxKind.LogicalAndExpression:
                    return context.MkAnd(left.As<BoolExpr>(), right.As<BoolExpr>());
                case SyntaxKind.LogicalOrExpression:
                    return context.MkOr(left.As<BoolExpr>(), right.As<BoolExpr>());

                case SyntaxKind.GreaterThanExpression:
                    //todo: fix comparison expression for string expressions
                    return context.MkGt(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return context.MkGe(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.LessThanExpression:
                    return context.MkLt(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.LessThanOrEqualExpression:
                    return context.MkLe(left.As<ArithExpr>(), right.As<ArithExpr>());

                case SyntaxKind.AddExpression:
                    return context.MkAdd(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.SubtractExpression:
                    return context.MkSub(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.MultiplyExpression:
                    return context.MkMul(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.DivideExpression:
                    return context.MkDiv(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.ModuloExpression:
                    return context.MkMod(left.As<IntExpr>(), right.As<IntExpr>());

                //TODO: Fix this: since this is only for numeric values
                case SyntaxKind.EqualsExpression:
                    return context.MkEq(left, right);
                case SyntaxKind.NotEqualsExpression:
                    return context.MkNot(context.MkEq(left, right));
                default:
                    throw new NotImplementedException();
            }
        }

        private Expr ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, out Dictionary<string, NodeType> processedMembers) {
            if (prefixUnaryExpression.Kind() == SyntaxKind.LogicalNotExpression) {
                var innerExpression = prefixUnaryExpression.Operand;
                var parsedExpression = ParseExpression(innerExpression, out processedMembers).As<BoolExpr>();
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

        private Expr ParseExpression(ExpressionTransformation expressionSyntax, Dictionary<string, NodeType> cachedMembers) {
            var expressionKind = expressionSyntax.RewrittenExpression.Kind();

            switch (expressionKind) {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression((PrefixUnaryExpressionSyntax)expressionSyntax, cachedMembers);
                case SyntaxKind.SimpleMemberAccessExpression:
                    return context.MkBoolConst(expressionSyntax.ToString());
                case SyntaxKind.ParenthesizedExpression:
                    return ParseExpression(expressionSyntax.As<ParenthesizedExpressionSyntax>().Expression, cachedMembers);
            }

            var binaryExpression = (BinaryExpressionSyntax)expressionSyntax.RewrittenExpression;

            switch (expressionKind) {
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                case SyntaxKind.AddExpression:
                case SyntaxKind.SubtractExpression:
                case SyntaxKind.DivideExpression:
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.ModuloExpression:
                    return ParseBinaryExpression(binaryExpression, cachedMembers);
                default:
                    throw new NotImplementedException();
            }

        }

        private Expr ParseBinaryExpression(BinaryExpressionSyntax binaryExpression, Dictionary<string, NodeType> cachedMembers) {
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

                case SyntaxKind.AddExpression:
                    return context.MkAdd(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.SubtractExpression:
                    return context.MkSub(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.MultiplyExpression:
                    return context.MkMul(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.DivideExpression:
                    return context.MkDiv(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.ModuloExpression:
                    return context.MkMod(left.As<IntExpr>(), right.As<IntExpr>());

                //TODO: Fix this: since this is only for numeric values
                case SyntaxKind.EqualsExpression:
                    return context.MkEq(left, right);
                case SyntaxKind.NotEqualsExpression:
                    return context.MkNot(context.MkEq((ArithExpr)left, (ArithExpr)right));
                default:
                    throw new NotImplementedException();
            }
        }

        private Expr ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, Dictionary<string, NodeType> cachedMembers) {
            if (prefixUnaryExpression.Kind() == SyntaxKind.LogicalNotExpression) {
                var innerExpression = prefixUnaryExpression.Operand;
                var parsedExpression = ParseExpression(innerExpression, cachedMembers).As<BoolExpr>();
                return context.MkNot(parsedExpression);
            }

            throw new NotImplementedException();
        }

        private Expr ParseExpressionMember(ExpressionSyntax memberExpression, Dictionary<string, NodeType> cachedMembers) {
            var expressionKind = memberExpression.Kind();

            if (memberExpression is BinaryExpressionSyntax)
                return ParseBinaryExpression((BinaryExpressionSyntax)memberExpression, cachedMembers);

            if (expressionKind == SyntaxKind.NumericLiteralExpression)
                return ParseNumericLiteral(memberExpression.ToString());

            if (expressionKind == SyntaxKind.StringLiteralExpression)
                return ParseStringLiteral(memberExpression.ToString());

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName) {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseUnaryExpression(memberExpression, cachedMembers)
                    : ParseExpression(memberExpression, cachedMembers);
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression)
                return ParseUnaryExpression(memberExpression, cachedMembers);

            var memberType = typeService.GetType(memberExpression);
            var cachedMember = cachedMembers.Values.FirstOrDefault(x => x.Type == memberType && x.Node.ToString()==memberExpression.ToString());

            if (cachedMember != null)
                return cachedMember.Expression;

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

        private List<SyntaxNode> GetVariables(ExpressionSyntax expression) {
            //TODO: currently we support only IdentifierNameSyntax
            var variables = expression
                .DescendantNodes<IdentifierNameSyntax>()
                .Select(x => x.As<SyntaxNode>())
                .DistinctBy(x => x.ToString())
                .ToList();

            return variables;
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

        private static IEnumerable<IEnumerable<T>> GetPermutations<T>(List<T> list)
        {
            return GetPermutations(list, list.Count);
        }

        private static IEnumerable<IEnumerable<T>> GetPermutations<T>(List<T> list, int length) {
            if (length == 1)
                return list.Select(x => new[] { x });

            var result = GetPermutations(list, length - 1)
                        .SelectMany(x => list.Where(e => !x.Contains(e)), (t1, t2) => t1.Concat(new[] { t2 }));

            return result;
        }

        private class NodeType {
            public SyntaxNode Node { get; set; }
            public Expr Expression { get; set; }
            public Type Type { get; set; }
        }

        private class ExpressionTransformation
        {
            public ExpressionSyntax OriginalExpression { get; set; }
            public ExpressionSyntax RewrittenExpression { get; set; }
            public Dictionary<SyntaxNode, SyntaxNode> VariablesMap { get; set; }
        }
    }
}