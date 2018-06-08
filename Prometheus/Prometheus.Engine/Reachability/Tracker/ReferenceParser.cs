using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.Reachability.Model.Query;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Tracker
{
    internal class ReferenceParser : IReferenceParser
    {
        private const string FIRST_TOKEN = "First";
        private const string FIRST_OR_DEFAULT_TOKEN = "FirstOrDefault";
        private const string WHERE_TOKEN = "Where";

        public bool IsBuildInMethod(string methodName)
        {
            return methodName == FIRST_OR_DEFAULT_TOKEN || methodName == FIRST_TOKEN || methodName == WHERE_TOKEN;
        }

        public (Reference, IReferenceQuery) Parse(SyntaxNode node)
        {
            if (node is ReturnStatementSyntax)
                return InternalParse(node.As<ReturnStatementSyntax>().Expression);

            return InternalParse(node);
        }

        private (Reference, IReferenceQuery) InternalParse(SyntaxNode node)
        {
            if (node is IdentifierNameSyntax)
                return (new Reference(node), null);

            if (node is ElementAccessExpressionSyntax)
                return ParseIndexerExpression(node.As<ElementAccessExpressionSyntax>());

            if (node is ArgumentSyntax)
                return InternalParse(node.As<ArgumentSyntax>().Expression);

            //todo: discern between various invocations: now only supporting "reference = instance.First/Where/FirstOrDefault"
            if (node is InvocationExpressionSyntax)
                return ParseLambdaExpression(node.As<InvocationExpressionSyntax>());

            throw new NotSupportedException($"Only {nameof(IndexArgumentQuery)}, {nameof(FirstExpressionQuery)} and {nameof(WhereExpressionQuery)} reference queries are supported");
        }

        private (Reference, IReferenceQuery) ParseIndexerExpression(ElementAccessExpressionSyntax node)
        {
            var elementAccessExpression = node.As<ElementAccessExpressionSyntax>();
            var referenceNode = elementAccessExpression.Expression.As<IdentifierNameSyntax>();
            var query = elementAccessExpression.ArgumentList.Arguments[0];

            return (new Reference(referenceNode), new IndexArgumentQuery(query));
        }

        private (Reference, IReferenceQuery) ParseLambdaExpression(InvocationExpressionSyntax node)
        {
            var nodeText = node.ToString();

            if (nodeText.Contains(WHERE_TOKEN))
                return ParseWhereExpression(node);

            if (nodeText.Contains(FIRST_TOKEN) || nodeText.Contains(FIRST_OR_DEFAULT_TOKEN))
                return ParseFirstExpression(node);

            throw new NotSupportedException($"Only {FIRST_TOKEN} and {WHERE_TOKEN} lambda expressions are supported");
        }

        private static (Reference, IReferenceQuery) ParseFirstExpression(InvocationExpressionSyntax node)
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

            return (new Reference(referenceNode), new FirstExpressionQuery(query));
        }

        private static (Reference, IReferenceQuery) ParseWhereExpression(InvocationExpressionSyntax node)
        {
            var referenceNode = node.DescendantNodes<IdentifierNameSyntax>().First();
            var expressionIdentifier = node
                .Expression.As<MemberAccessExpressionSyntax>()
                .Name.Identifier.Text;

            if (expressionIdentifier != WHERE_TOKEN)
                throw new NotSupportedException("Only instance.Where() expressions with one call level are allowed");

            var query = node
                .ArgumentList.Arguments[0]
                .Expression.As<SimpleLambdaExpressionSyntax>();

            return (new Reference(referenceNode), new WhereExpressionQuery(query));
        }
    }
}