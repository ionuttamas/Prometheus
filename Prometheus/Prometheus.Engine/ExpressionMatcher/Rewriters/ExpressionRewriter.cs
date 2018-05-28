using System.Collections.Generic;
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

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node) {
            if (capturedVariablesTable.ContainsKey(node))
                return capturedVariablesTable[node];

            return base.VisitIdentifierName(node);
        }
    }
}