using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ExpressionMatcher.Rewriters
{
    internal class ExpressionRewriter : CSharpSyntaxRewriter {
        private readonly Dictionary<SyntaxNode, SyntaxNode> capturedVariablesTable;

        public ExpressionRewriter(Dictionary<SyntaxNode, SyntaxNode> capturedVariablesTable) {
            this.capturedVariablesTable = capturedVariablesTable;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            var keyValue = capturedVariablesTable.FirstOrDefault(x => x.Key.ToString() == node.ToString());

            if (!keyValue.Equals(default(KeyValuePair<SyntaxNode, SyntaxNode>)))
                return keyValue.Value;

            return base.VisitIdentifierName(node);
        }

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var keyValue = capturedVariablesTable.FirstOrDefault(x => x.Key.ToString() == node.ToString());

            if (!keyValue.Equals(default(KeyValuePair<SyntaxNode, SyntaxNode>)))
                return keyValue.Value;

            return base.VisitMemberAccessExpression(node);
        }
    }
}