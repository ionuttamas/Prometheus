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
    }
}