using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Common
{
    public static class InvocationExpressionExtensions {
        public static string GetMethodName(this InvocationExpressionSyntax invocationExpression) {
            if (invocationExpression.Expression is IdentifierNameSyntax)
                return invocationExpression.Expression.ToString();

            return invocationExpression
                .Expression.As<MemberAccessExpressionSyntax>()
                .Name.As<IdentifierNameSyntax>()
                .Identifier.Text;
        }

        public static IdentifierNameSyntax GetReferenceNode(this InvocationExpressionSyntax invocationExpression) {

            if (!(invocationExpression.Expression is MemberAccessExpressionSyntax))
                throw new ArgumentException($"The invocation expression {invocationExpression} is not a reference method call");

            var memberAccess = invocationExpression.Expression.As<MemberAccessExpressionSyntax>();

            if (!(memberAccess.Expression is IdentifierNameSyntax))
                throw new ArgumentException($"The invocation expression {invocationExpression} is not a reference method call");

            var instanceExpression = memberAccess.Expression.As<IdentifierNameSyntax>();

            return instanceExpression;
        }
    }
}