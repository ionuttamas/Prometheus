using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.Reachability.Tracker;

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

            foreach (var condition in conditions)
            {
                var expr = expressionParser.ParseExpression(condition.TestExpression, out var nodes);

                if (condition.IsNegated)
                {
                    expr = context.MkNot(expr);
                }

                processedNodes.Merge(nodes);
                resultExpr = context.MkAnd(resultExpr, expr);
            }

            return resultExpr;
        }
    }
}