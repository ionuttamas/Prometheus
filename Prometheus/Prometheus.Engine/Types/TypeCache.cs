using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Types
{
    internal class TypeCache
    {
        private readonly Dictionary<ExpressionSyntax, TypeContainer> expressionSyntaxTypeCache;
        private readonly Dictionary<SyntaxToken, TypeContainer> syntaxTokenTypeCache;

        public TypeCache()
        {
            expressionSyntaxTypeCache = new Dictionary<ExpressionSyntax, TypeContainer>();
            syntaxTokenTypeCache = new Dictionary<SyntaxToken, TypeContainer>();
        }

        public void AddToCache(ExpressionSyntax expressionSyntax, TypeContainer container)
        {
            expressionSyntaxTypeCache[expressionSyntax] = container;
        }

        public void AddToCache(SyntaxToken syntaxToken, TypeContainer container)
        {
            syntaxTokenTypeCache[syntaxToken] = container;
        }

        public bool TryGetType(ExpressionSyntax syntax, out TypeContainer container)
        {
            return expressionSyntaxTypeCache.TryGetValue(syntax, out container);
        }

        public bool TryGetType(SyntaxToken syntaxToken, out TypeContainer container)
        {
            return syntaxTokenTypeCache.TryGetValue(syntaxToken, out container);
        }
    }
}