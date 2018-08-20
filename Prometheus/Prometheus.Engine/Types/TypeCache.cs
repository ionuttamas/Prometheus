using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Types
{
    internal class TypeCache
    {
        private readonly Dictionary<ExpressionSyntax, List<Type>> expressionSyntaxTypeCache;
        private readonly Dictionary<SyntaxToken, List<Type>> syntaxTokenTypeCache;

        public TypeCache()
        {
            expressionSyntaxTypeCache = new Dictionary<ExpressionSyntax, List<Type>>();
            syntaxTokenTypeCache = new Dictionary<SyntaxToken, List<Type>>();
        }

        public void AddToCache(ExpressionSyntax expressionSyntax, List<Type> types)
        {
            expressionSyntaxTypeCache[expressionSyntax] = types;
        }

        public void AddToCache(SyntaxToken syntaxToken, List<Type> types)
        {
            syntaxTokenTypeCache[syntaxToken] = types;
        }

        public bool TryGetType(ExpressionSyntax syntax, out List<Type> types)
        {
            return expressionSyntaxTypeCache.TryGetValue(syntax, out types);
        }

        public bool TryGetType(SyntaxToken syntaxToken, out List<Type> types) {
            return syntaxTokenTypeCache.TryGetValue(syntaxToken, out types);
        }
    }
}