using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ExpressionMatcher.Rewriters
{
    internal class PredicateRewriter : CSharpSyntaxRewriter
    {
        private ParameterSyntax sourceParameter;
        private readonly ParameterSyntax targetParameter;
        private readonly Dictionary<SyntaxNode, SyntaxNode> capturedVariablesTable;

        public PredicateRewriter(ParameterSyntax targetParameter, Dictionary<SyntaxNode, SyntaxNode> capturedVariablesTable)
        {
            this.targetParameter = targetParameter;
            this.capturedVariablesTable = capturedVariablesTable;
        }

        public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            sourceParameter = node.Parameter;

            return base.VisitSimpleLambdaExpression(node);
        }

        public override SyntaxNode VisitParameter(ParameterSyntax node)
        {
            return targetParameter;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (capturedVariablesTable.ContainsKey(node))
                return capturedVariablesTable[node];

            return base.VisitIdentifierName(node);
        }

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Name.Identifier.Text != sourceParameter.Identifier.Text)
                return base.VisitMemberAccessExpression(node);

            var targetIdentifier = SyntaxFactory.IdentifierName(targetParameter.Identifier.Text);
            node = node.WithName(targetIdentifier);
            return node;
        }
    }
}