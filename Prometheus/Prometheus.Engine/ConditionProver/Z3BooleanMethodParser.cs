using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.Reachability.Tracker;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.ConditionProver
{
    internal class Z3BooleanMethodParser
    {
        private readonly Z3BooleanExpressionParser expressionParser;
        private readonly ConditionExtractor conditionExtractor;
        private readonly Context context;

        public Z3BooleanMethodParser(Z3BooleanExpressionParser expressionParser, ConditionExtractor conditionExtractor, Context context)
        {
            this.expressionParser = expressionParser;
            this.conditionExtractor = conditionExtractor;
            this.context = context;
        }

        public BoolExpr ParseBooleanMethod(MethodDeclarationSyntax methodDeclaration, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedNodes)
        {
            var returnExpressions = methodDeclaration
                .DescendantNodes<ReturnStatementSyntax>()
                .Where(x => x.Expression.Kind() != SyntaxKind.ObjectCreationExpression) //TODO: are we interested in "return new X()"?
                .ToList();
            processedNodes = new Dictionary<string, NodeType>();
            var possibleReturnExprs = new List<BoolExpr>();

            foreach (var returnExpression in returnExpressions)
            {
                var expr = ParseReturnStatement(returnExpression, contexts, out var nodes);
                processedNodes.Merge(nodes);
                possibleReturnExprs.Add(expr);
            }

            var resultExpr = context.MkOr(possibleReturnExprs);

            return resultExpr;
        }

        public BoolExpr ParseCachedBooleanMethod(MethodDeclarationSyntax methodDeclaration, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedNodes) {
            var returnExpressions = methodDeclaration
                .DescendantNodes<ReturnStatementSyntax>()
                .Where(x => x.Expression.Kind() != SyntaxKind.ObjectCreationExpression) //TODO: are we interested in "return new X()"?
                .ToList();
            var possibleReturnExprs = returnExpressions
                .Select(x => ParseCachedReturnStatement(x, contexts, cachedNodes))
                .ToList();
            var resultExpr = context.MkOr(possibleReturnExprs);

            return resultExpr;
        }


        private BoolExpr ParseReturnStatement(ReturnStatementSyntax returnStatement, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedNodes) {
            var conditions = conditionExtractor.ExtractConditions(returnStatement);
            var returnExpr = expressionParser.ParseExpression(returnStatement.Expression, contexts, out processedNodes);
            var resultExpr = returnExpr;
            var condition = new Condition(conditions, false);
            var testExpr = ProcessCondition(condition, contexts, out processedNodes);
            resultExpr = context.MkAnd(resultExpr, testExpr);

            return resultExpr;
        }

        private BoolExpr ProcessCondition(Condition condition, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedNodes) {
            if (!condition.Conditions.Any()) {
                var expr = expressionParser.ParseExpression(condition.TestExpression, contexts, out processedNodes);

                return condition.IsNegated ? context.MkNot(expr) : expr;
            }

            var resultExpr = context.MkTrue();
            processedNodes = new Dictionary<string, NodeType>();

            foreach (var nestedCondition in condition.Conditions) {
                var expr = ProcessCondition(nestedCondition, contexts, out var nodes);

                if (expr != context.MkTrue())
                {
                    resultExpr = context.MkAnd(resultExpr, expr);
                }

                processedNodes.Merge(nodes);
            }

            return resultExpr;
        }

        private BoolExpr ParseCachedReturnStatement(ReturnStatementSyntax returnStatement, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedNodes) {
            var conditions = conditionExtractor.ExtractConditions(returnStatement);
            var returnExpr = expressionParser.ParseRawCachedExpression(returnStatement.Expression, contexts, cachedNodes);
            var condition = new Condition(conditions, false);
            var testExpr = ProcessCachedCondition(condition, contexts, cachedNodes);

            var resultExpr = context.MkAnd(testExpr, returnExpr);
            return resultExpr;
        }

        private BoolExpr ProcessCachedCondition(Condition condition, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedNodes) {
            if (!condition.Conditions.Any()) {
                var expr = expressionParser.ParseRawCachedExpression(condition.TestExpression, contexts, cachedNodes);
                expr = condition.IsNegated ? context.MkNot(expr) : expr;

                return expr;
            }

            //TODO: double check this if ORs are well-handled
            var resultExpr = context.MkAnd(condition.Conditions.Select(x => ProcessCachedCondition(x, contexts, cachedNodes)));

            return resultExpr;
        }
    }
}