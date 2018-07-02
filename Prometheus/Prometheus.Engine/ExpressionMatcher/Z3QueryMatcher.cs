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

        public Z3QueryMatcher(ITypeService typeService, Context context)
        {
            this.typeService = typeService;
            this.context = context;
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

            var firstResult = ParseExpression(first.As<ExpressionSyntax>(), out var processedMembers);

            foreach (IEnumerable<SyntaxNode> permutation in GetPermutations(sourceVariables))
            {
                var sourcePermutation = permutation.ToList();
                var variablesMap = targetVariables.Select((x, ix) => new { Index = ix, Node = x }).ToDictionary(x => x.Node, x => sourcePermutation[x.Index]);

                if (variablesMap.Any(x => typeService.GetType(x.Key.As<ExpressionSyntax>()) != typeService.GetType(x.Value.As<ExpressionSyntax>())))
                    continue;

                var rewriter = new ExpressionRewriter(variablesMap);
                var rewrittenExpression = rewriter.Visit(second).As<ExpressionSyntax>();
                var transformation = new ExpressionTransformation
                {
                    OriginalExpression = second,
                    RewrittenExpression = rewrittenExpression
                };

                var secondResult = ParseExpression(transformation, processedMembers);
                Solver solver = context.MkSolver();
                BoolExpr equalityExpression = context.MkEq(firstResult.Item1, secondResult.Item1);
                BoolExpr forAllExpression = context.MkForall(firstResult.Item2.Concat(secondResult.Item2).DistinctBy(x=>x.ToString()).ToArray(), equalityExpression);
                solver.Assert(forAllExpression);
                Status status = solver.Check();

                if (status == Status.SATISFIABLE) {
                    satisfiableTable = variablesMap
                        .ToDictionary(x=>x.Key is MemberAccessExpressionSyntax ? x.Key.As<MemberAccessExpressionSyntax>().GetRootIdentifier() :x.Key,
                                      x => x.Value is MemberAccessExpressionSyntax ? x.Value.As<MemberAccessExpressionSyntax>().GetRootIdentifier() : x.Value)
                        .DistinctBy(x => new { Key = x.Key.ToString(), Value = x.Value.ToString() })
                        .ToDictionary(x => x.Key, x => x.Value);

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
            var firstResult = ParseExpression(first.Body.As<ExpressionSyntax>(), out var processedMembers);

            foreach (IEnumerable<SyntaxNode> permutation in GetPermutations(sourceCapturedVariables))
            {
                var sourcePermutation = permutation.ToList();
                var variablesMap = targetCapturedVariables.Select((x, ix) => new {Index = ix, Node = x}).ToDictionary(x => x.Node, x => sourcePermutation[x.Index]);

                if(variablesMap.Any(x=>typeService.GetType(x.Key.As<ExpressionSyntax>())!= typeService.GetType(x.Value.As<ExpressionSyntax>())))
                    continue;

                var predicateRewriter = new PredicateRewriter(firstParameter, variablesMap);
                var rewrittenLambda = predicateRewriter.Visit(second).As<SimpleLambdaExpressionSyntax>();
                var expressionTransformation = new ExpressionTransformation(second.Body.As<ExpressionSyntax>(), rewrittenLambda.Body.As<ExpressionSyntax>());
                var secondResult = ParseExpression(expressionTransformation, processedMembers);
                Solver solver = context.MkSolver();
                BoolExpr equalityExpression = context.MkEq(firstResult.Item1, secondResult.Item1);
                Quantifier forAllExpression = context.MkForall(firstResult.Item2.Concat(secondResult.Item2).DistinctBy(x=>x.ToString()).ToArray(), equalityExpression);
                solver.Assert(forAllExpression);
                Status status = solver.Check();

                if (status == Status.SATISFIABLE)
                {
                    satisfiableTable = variablesMap
                        .ToDictionary(x => x.Key is MemberAccessExpressionSyntax ? x.Key.As<MemberAccessExpressionSyntax>().GetRootIdentifier() : x.Key,
                            x => x.Value is MemberAccessExpressionSyntax ? x.Value.As<MemberAccessExpressionSyntax>().GetRootIdentifier() : x.Value)
                        .DistinctBy(x => new { Key = x.Key.ToString(), Value = x.Value.ToString() })
                        .ToDictionary(x=>x.Key, x=>x.Value);

                    return true;
                }
            }

            return false;
        }

        #region Non-cached processing

        private (Expr, List<Expr>) ParseExpression(ExpressionSyntax expressionSyntax, out Dictionary<string, NodeType> processedMembers) {
            var expressionKind = expressionSyntax.Kind();

            switch (expressionKind) {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression((PrefixUnaryExpressionSyntax)expressionSyntax, out processedMembers);
                case SyntaxKind.SimpleMemberAccessExpression:
                case SyntaxKind.IdentifierName:
                    processedMembers = new Dictionary<string, NodeType>();
                    var expression = context.MkConst(expressionSyntax.ToString(), typeService.GetSort(typeService.GetType(expressionSyntax)));
                    return (expression, new List<Expr> { expression });
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

        private (Expr, List<Expr>) ParseBinaryExpression(BinaryExpressionSyntax binaryExpression, out Dictionary<string, NodeType> processedMembers) {
            SyntaxKind expressionKind = binaryExpression.Kind();
            var left = ParseExpressionMember(binaryExpression.Left, out var leftProcessedMembers);
            var right = ParseExpressionMember(binaryExpression.Right, out var rightProcessedMembers);

            processedMembers = new Dictionary<string, NodeType>();
            processedMembers.Merge(leftProcessedMembers);
            processedMembers.Merge(rightProcessedMembers);

            switch (expressionKind) {
                case SyntaxKind.LogicalAndExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkAnd(left.Item1.As<BoolExpr>(), right.Item1.As<BoolExpr>()), left.Item2);
                case SyntaxKind.LogicalOrExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkOr(left.Item1.As<BoolExpr>(), right.Item1.As<BoolExpr>()), left.Item2);

                case SyntaxKind.GreaterThanExpression:
                    //todo: fix comparison expression for string expressions
                    left.Item2.AddRange(right.Item2);
                    return (context.MkGt(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.GreaterThanOrEqualExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkGe(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.LessThanExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkLt(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.LessThanOrEqualExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkLe(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);

                case SyntaxKind.AddExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkAdd(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.SubtractExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkSub(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.MultiplyExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkMul(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.DivideExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkDiv(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.ModuloExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkMod(left.Item1.As<IntExpr>(), right.Item1.As<IntExpr>()), left.Item2);

                //TODO: Fix this: since this is only for numeric values
                case SyntaxKind.EqualsExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkEq(left.Item1, right.Item1), left.Item2);
                case SyntaxKind.NotEqualsExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkNot(context.MkEq(left.Item1, right.Item1)), left.Item2);
                default:
                    throw new NotImplementedException();
            }
        }

        private (Expr, List<Expr>) ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, out Dictionary<string, NodeType> processedMembers) {
            if (prefixUnaryExpression.Kind() == SyntaxKind.LogicalNotExpression) {
                var innerExpression = prefixUnaryExpression.Operand;
                var result = ParseExpression(innerExpression, out processedMembers);
                return (context.MkNot(result.Item1.As<BoolExpr>()), result.Item2);
            }

            throw new NotImplementedException();
        }

        private (Expr, List<Expr>) ParseExpressionMember(ExpressionSyntax memberExpression, out Dictionary<string, NodeType> processedMembers) {
            var expressionKind = memberExpression.Kind();

            if (memberExpression is BinaryExpressionSyntax) {
                return ParseBinaryExpression((BinaryExpressionSyntax)memberExpression, out processedMembers);
            }

            if (expressionKind == SyntaxKind.NumericLiteralExpression) {
                processedMembers = new Dictionary<string, NodeType>();
                Expr numericLiteral = ParseNumericLiteral(memberExpression.ToString());
                return (numericLiteral, new List<Expr>{ numericLiteral });
            }

            if (expressionKind == SyntaxKind.StringLiteralExpression) {
                processedMembers = new Dictionary<string, NodeType>();
                Expr stringLiteral = ParseNumericLiteral(memberExpression.ToString());
                return (stringLiteral, new List<Expr> { stringLiteral });
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
            var variableExpression = ParseVariableExpression(memberExpression, memberType, processedMembers);
            return (variableExpression, new List<Expr> {variableExpression});
        }

        private (Expr, List<Expr>) ParseUnaryExpression(ExpressionSyntax unaryExpression, out Dictionary<string, NodeType> processedMembers) {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)unaryExpression;
            var result = ParseExpressionMember(prefixUnaryExpression.Operand, out processedMembers);

            return (context.MkUnaryMinus(result.As<ArithExpr>()), result.Item2);
        }

        private Expr ParseVariableExpression(ExpressionSyntax memberExpression, Type type, Dictionary<string, NodeType> processedMembers) {
            string memberName = memberExpression.ToString();
            Sort sort = typeService.GetSort(type);
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

        private (Expr, List<Expr>) ParseExpression(ExpressionTransformation expression, Dictionary<string, NodeType> cachedMembers) {
            var expressionKind = expression.RewrittenExpression.Kind();

            switch (expressionKind) {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression(expression, cachedMembers);
                case SyntaxKind.SimpleMemberAccessExpression:
                    //TODO: why is this bool only
                    var memberType = typeService.GetType(expression.OriginalExpression);
                    var sort = typeService.GetSort(memberType);
                    var memberExpression = context.MkConst(expression.RewrittenExpression.ToString(), sort);
                    return (memberExpression, new List<Expr> {memberExpression});
                case SyntaxKind.ParenthesizedExpression:
                    expression = new ExpressionTransformation(expression.OriginalExpression.As<ParenthesizedExpressionSyntax>().Expression, expression.RewrittenExpression.As<ParenthesizedExpressionSyntax>().Expression);
                    return ParseExpression(expression, cachedMembers);
            }

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
                    return ParseBinaryExpression(expression, cachedMembers);
                default:
                    throw new NotImplementedException();
            }

        }

        private (Expr, List<Expr>) ParseBinaryExpression(ExpressionTransformation expression, Dictionary<string, NodeType> cachedMembers) {
            SyntaxKind expressionKind = expression.RewrittenExpression.Kind();
            var leftExpression = new ExpressionTransformation(expression.OriginalExpression.As<BinaryExpressionSyntax>().Left, expression.RewrittenExpression.As<BinaryExpressionSyntax>().Left);
            var rightExpression = new ExpressionTransformation(expression.OriginalExpression.As<BinaryExpressionSyntax>().Right, expression.RewrittenExpression.As<BinaryExpressionSyntax>().Right);
            var left = ParseExpressionMember(leftExpression, cachedMembers);
            var right = ParseExpressionMember(rightExpression, cachedMembers);

            switch (expressionKind) {
                case SyntaxKind.LogicalAndExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkAnd(left.Item1.As<BoolExpr>(), right.Item1.As<BoolExpr>()), left.Item2);
                case SyntaxKind.LogicalOrExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkOr(left.Item1.As<BoolExpr>(), right.Item1.As<BoolExpr>()), left.Item2);
                case SyntaxKind.GreaterThanExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkGt(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.GreaterThanOrEqualExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkGe(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.LessThanExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkLt(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.LessThanOrEqualExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkLe(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);

                case SyntaxKind.AddExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkAdd(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.SubtractExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkSub(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.MultiplyExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkMul(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.DivideExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkDiv(left.Item1.As<ArithExpr>(), right.Item1.As<ArithExpr>()), left.Item2);
                case SyntaxKind.ModuloExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkMod(left.Item1.As<IntExpr>(), right.Item1.As<IntExpr>()), left.Item2);

                //TODO: Fix this: since this is only for numeric values
                case SyntaxKind.EqualsExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkEq(left.Item1, right.Item1), left.Item2);
                case SyntaxKind.NotEqualsExpression:
                    left.Item2.AddRange(right.Item2);
                    return (context.MkNot(context.MkEq(left.Item1, right.Item1)), left.Item2);
                default:
                    throw new NotImplementedException();
            }
        }

        private (Expr, List<Expr>) ParsePrefixUnaryExpression(ExpressionTransformation expression, Dictionary<string, NodeType> cachedMembers) {
            if (expression.RewrittenExpression.Kind() == SyntaxKind.LogicalNotExpression) {
                expression = new ExpressionTransformation(expression.OriginalExpression.As<PrefixUnaryExpressionSyntax>().Operand,
                                                          expression.RewrittenExpression.As<PrefixUnaryExpressionSyntax>().Operand);
                var result = ParseExpression(expression, cachedMembers);
                return (context.MkNot(result.Item1.As<BoolExpr>()), result.Item2);
            }

            throw new NotImplementedException();
        }

        private (Expr, List<Expr>) ParseExpressionMember(ExpressionTransformation expression, Dictionary<string, NodeType> cachedMembers) {
            var expressionKind = expression.RewrittenExpression.Kind();

            if (expression.RewrittenExpression is BinaryExpressionSyntax)
                return ParseBinaryExpression(expression, cachedMembers);

            if (expressionKind == SyntaxKind.NumericLiteralExpression)
            {
                var numericLiteral = ParseNumericLiteral(expression.RewrittenExpression.ToString());
                return (numericLiteral, new List<Expr> {numericLiteral});
            }

            if (expressionKind == SyntaxKind.StringLiteralExpression)
            {
                var stringLiteral = ParseStringLiteral(expression.RewrittenExpression.ToString());
                return (stringLiteral, new List<Expr> { stringLiteral });
            }

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName) {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseUnaryExpression(expression, cachedMembers)
                    : ParseExpression(expression, cachedMembers);
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression)
                return ParseUnaryExpression(expression, cachedMembers);

            var memberType = typeService.GetType(expression.OriginalExpression);
            var cachedMember = cachedMembers.Values.FirstOrDefault(x => x.Type == memberType && x.Node.ToString()==expression.RewrittenExpression.ToString());
            //TODO: why do we do this check?
            if (cachedMember != null)
            {
                return (cachedMember.Expression, new List<Expr> { cachedMember.Expression });
            }

            var cachedVariable = ParseCachedVariableExpression(expression.RewrittenExpression, memberType, cachedMembers);
            return (cachedVariable, new List<Expr> {cachedVariable});
        }

        private (Expr, List<Expr>) ParseUnaryExpression(ExpressionTransformation expression, Dictionary<string, NodeType> cachedMembers) {
            expression = new ExpressionTransformation(expression.OriginalExpression.As<PrefixUnaryExpressionSyntax>().Operand, expression.RewrittenExpression.As<PrefixUnaryExpressionSyntax>().Operand);
            var result = ParseExpressionMember(expression, cachedMembers);

            return (context.MkUnaryMinus(result.Item1.As<ArithExpr>()), result.Item2);
        }

        private Expr ParseCachedVariableExpression(ExpressionSyntax memberExpression, Type type, Dictionary<string, NodeType> cachedMembers) {
            string memberName = memberExpression.ToString();

            if (cachedMembers.ContainsKey(memberName)) {
                return cachedMembers[memberName].Expression;
            }

            var constExpr = context.MkConst(memberName, typeService.GetSort(type));

            return constExpr;
        }

        #endregion

        #region Utils

        private static List<SyntaxNode> GetVariables(ExpressionSyntax expression) {
            var variables = expression
                .DescendantNodes<IdentifierNameSyntax>(x => x.Parent.Kind() != SyntaxKind.SimpleMemberAccessExpression)
                .Select(x => x.As<SyntaxNode>())
                .Concat(expression.DescendantNodes<MemberAccessExpressionSyntax>())
                .DistinctBy(x => x.ToString())
                .ToList();

            return variables;
        }

        private static List<SyntaxNode> GetCapturedVariables(SimpleLambdaExpressionSyntax lambda) {
            var parameter = lambda.Parameter;
            var members = lambda.Body.DescendantNodes<MemberAccessExpressionSyntax>()
                .Where(x => x.GetRootIdentifier().Identifier.Text != parameter.Identifier.Text)
                .ToList();

            members.RemoveAll(x => members.Any(m => m.DescendantNodes<MemberAccessExpressionSyntax>().Contains(x)));

            var variables = lambda.Body
                .DescendantNodes<IdentifierNameSyntax>().Where(x => x.Identifier.Text != parameter.Identifier.Text && x.Parent.Kind() != SyntaxKind.SimpleMemberAccessExpression)
                .Select(x => x.As<SyntaxNode>())
                .Concat(members)
                .DistinctBy(x => x.ToString())
                .ToList();



            return variables;
        }

        private static IEnumerable<IEnumerable<T>> GetPermutations<T>(List<T> list)
        {
            if (list.Count == 0)
                return Enumerable.Empty<IEnumerable<T>>();

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

        private class ExpressionTransformation {
            public ExpressionSyntax OriginalExpression { get; set; }
            public ExpressionSyntax RewrittenExpression { get; set; }

            public ExpressionTransformation() {
            }

            public ExpressionTransformation(ExpressionSyntax originalExpression, ExpressionSyntax rewrittenExpression) {
                OriginalExpression = originalExpression;
                RewrittenExpression = rewrittenExpression;
            }
        }

        #endregion
    }
}