using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Tracker
{
    internal class ReferenceParser : IReferenceParser
    {
        private const string FIRST_TOKEN = "First";
        private const string WHERE_TOKEN = "Where";

        public Reference Parse(SyntaxNode node)
        {
            if (node is ReturnStatementSyntax)
                return InternalParse(node.As<ReturnStatementSyntax>().Expression);

            return InternalParse(node);
        }

        private Reference InternalParse(SyntaxNode node)
        {
            if (node is IdentifierNameSyntax)
                return new Reference(node);

            if (node is ElementAccessExpressionSyntax)
                return ParseElementAccessExpression(node.As<ElementAccessExpressionSyntax>());

            //todo: discern between various invocations
            if (node is InvocationExpressionSyntax)
                return ParseLambdaExpression(node.As<InvocationExpressionSyntax>());

            throw new NotSupportedException($"Only {nameof(IndexArgumentQuery)}, {nameof(FirstExpressionQuery)} and {nameof(WhereExpressionQuery)} reference queries are supported");
        }

        private Reference ParseElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            var elementAccessExpression = node.As<ElementAccessExpressionSyntax>();
            var referenceNode = elementAccessExpression.Expression.As<IdentifierNameSyntax>();
            var query = elementAccessExpression.ArgumentList.Arguments[0];

            return new Reference(referenceNode) {
                Query = new IndexArgumentQuery(query)
            };
        }

        private Reference ParseLambdaExpression(InvocationExpressionSyntax node)
        {
            var referenceNode = node.DescendantNodes<IdentifierNameSyntax>().First();
            var memberExpression = node.Expression.As<MemberAccessExpressionSyntax>();
            var expressionIdentifier = memberExpression.Name;
            var query = node.ArgumentList.Arguments[0].Expression.As<SimpleLambdaExpressionSyntax>();

            if (expressionIdentifier.Identifier.Text == FIRST_TOKEN)
            {
                return new Reference(referenceNode) {
                    Query = new FirstExpressionQuery(query)
                };
            }

            if (expressionIdentifier.Identifier.Text == WHERE_TOKEN) {
                return new Reference(referenceNode) {
                    Query = new WhereExpressionQuery(query)
                };
            }

            throw new NotSupportedException($"Only {FIRST_TOKEN} and {WHERE_TOKEN} lambda expressions are supported");
        }
    }
}