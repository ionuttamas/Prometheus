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
        private const string FIRST_OR_DEFAULT_TOKEN = "FirstOrDefault";
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
            var nodeText = node.ToString();

            if (nodeText.Contains(WHERE_TOKEN))
                return ParseWhereExpression(node);

            if (nodeText.Contains(FIRST_TOKEN) || nodeText.Contains(FIRST_OR_DEFAULT_TOKEN))
                return ParseFirstExpression(node);

            throw new NotSupportedException($"Only {FIRST_TOKEN} and {WHERE_TOKEN} lambda expressions are supported");
        }

        private static Reference ParseFirstExpression(InvocationExpressionSyntax node)
        {
            var referenceNode = node.DescendantNodes<IdentifierNameSyntax>().First();
            var expressionIdentifier = node
                .Expression.As<MemberAccessExpressionSyntax>()
                .Name.Identifier.Text;

            if (expressionIdentifier != FIRST_TOKEN && expressionIdentifier != FIRST_OR_DEFAULT_TOKEN)
                throw new NotSupportedException(
                    "Only instance.First() or instance.FirstOrDefault() expressions with one call level are allowed");

            var query = node
                .ArgumentList.Arguments[0]
                .Expression.As<SimpleLambdaExpressionSyntax>();
            return new Reference(referenceNode)
            {
                Query = new FirstExpressionQuery(query)
            };
        }

        private static Reference ParseWhereExpression(InvocationExpressionSyntax node)
        {
            var referenceNode = node.DescendantNodes<IdentifierNameSyntax>().First();
            var expressionIdentifier = node
                .Expression.As<MemberAccessExpressionSyntax>()
                .Expression.As<InvocationExpressionSyntax>()
                .Expression.As<MemberAccessExpressionSyntax>()
                .Name.Identifier.Text;

            if (expressionIdentifier != WHERE_TOKEN)
                throw new NotSupportedException("Only instance.Where() expressions with one call level are allowed");

            var query = node
                .Expression.As<MemberAccessExpressionSyntax>()
                .Expression.As<InvocationExpressionSyntax>()
                .ArgumentList.Arguments[0]
                .Expression.As<SimpleLambdaExpressionSyntax>();
            return new Reference(referenceNode)
            {
                Query = new WhereExpressionQuery(query)
            };
        }
    }
}