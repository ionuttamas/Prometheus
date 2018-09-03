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

        public Expr ParseBooleanMethod(MethodDeclarationSyntax methodDeclaration, out Dictionary<string, NodeType> processedNodes)
        {
            var returnExpressions = methodDeclaration
                .DescendantNodes<ReturnStatementSyntax>()
                .Where(x => x.Expression.Kind() != SyntaxKind.ObjectCreationExpression) //TODO: are we interested in "return new X()"?
                .ToList();
            processedNodes = new Dictionary<string, NodeType>();
            var resultExpr = context.MkTrue();

            foreach (var returnExpression in returnExpressions)
            {
                var expr = ParseReturnStatement(returnExpression, out var nodes);
                processedNodes.Merge(nodes);
                resultExpr = context.MkOr(resultExpr, expr);
            }

            return resultExpr;
        }

        private BoolExpr ParseReturnStatement(ReturnStatementSyntax returnStatement, out Dictionary<string, NodeType> processedNodes)
        {
            var conditions = conditionExtractor.ExtractConditions(returnStatement);
            var returnExpr = expressionParser.ParseExpression(returnStatement.Expression, out processedNodes);
            var resultExpr = returnExpr;
            var condition = new Condition(conditions, false);
            var testExpr = ProcessCondition(condition, out processedNodes);
            resultExpr = context.MkAnd(resultExpr, testExpr);

            return resultExpr;
        }

        private BoolExpr ProcessCondition(Condition condition, out Dictionary<string, NodeType> processedNodes)
        {
            if (!condition.Conditions.Any())
            {
                var expr = expressionParser.ParseExpression(condition.TestExpression, out processedNodes);

                return condition.IsNegated ? context.MkNot(expr) : expr;
            }

            var resultExpr = context.MkTrue();
            processedNodes = new Dictionary<string, NodeType>();

            foreach (var nestedCondition in condition.Conditions)
            {
                var expr = ProcessCondition(nestedCondition, out var nodes);
                resultExpr = context.MkAnd(resultExpr, expr);
                processedNodes.Merge(nodes);
            }

            return resultExpr;
        }
    }
}