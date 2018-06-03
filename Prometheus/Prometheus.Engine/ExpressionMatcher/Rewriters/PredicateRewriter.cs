using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;

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
            var keyValue = capturedVariablesTable.FirstOrDefault(x => x.Key.ToString() == node.ToString());

            if (!keyValue.Equals(default(KeyValuePair<SyntaxNode, SyntaxNode>)))
                return keyValue.Value;

            return base.VisitIdentifierName(node);
        }

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.GetRootIdentifier().Identifier.Text != sourceParameter.Identifier.Text)
            {
                var keyValue = capturedVariablesTable.FirstOrDefault(x => x.Key.ToString() == node.ToString());

                if (!keyValue.Equals(default(KeyValuePair<SyntaxNode, SyntaxNode>)))
                    return keyValue.Value;

                return base.VisitMemberAccessExpression(node);
            }

            var targetIdentifier = SyntaxFactory.IdentifierName(targetParameter.Identifier.Text);
            node = node.WithExpression(targetIdentifier);
            return node;
        }
    }
}