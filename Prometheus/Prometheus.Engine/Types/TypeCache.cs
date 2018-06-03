using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Types
{
    internal class TypeCache
    {
        private readonly Dictionary<ExpressionSyntax, Type> expressionSyntaxTypeCache;
        private readonly Dictionary<SyntaxToken, Type> syntaxTokenTypeCache;
        private readonly Dictionary<MethodDeclarationSyntax, Dictionary<string, string>> tokenTypeNameCache;

        public TypeCache()
        {
            expressionSyntaxTypeCache = new Dictionary<ExpressionSyntax, Type>();
            syntaxTokenTypeCache = new Dictionary<SyntaxToken, Type>();
            tokenTypeNameCache = new Dictionary<MethodDeclarationSyntax, Dictionary<string, string>>();
        }

        public void AddToCache(ExpressionSyntax expressionSyntax, Type type)
        {
            expressionSyntaxTypeCache[expressionSyntax] = type;
        }

        public void AddToCache(SyntaxToken syntaxToken, Type type)
        {
            syntaxTokenTypeCache[syntaxToken] = type;
        }

        public bool TryGetType(ExpressionSyntax syntax, out Type type)
        {
            return expressionSyntaxTypeCache.TryGetValue(syntax, out type);
        }

        public bool TryGetType(SyntaxToken syntaxToken, out Type type) {
            return syntaxTokenTypeCache.TryGetValue(syntaxToken, out type);
        }

        public bool TryGetTypeName(MethodDeclarationSyntax method, string token, out string typeName)
        {
            typeName = null;

            if (method==null || !tokenTypeNameCache.ContainsKey(method))
                return false;

            return tokenTypeNameCache[method].TryGetValue(token, out typeName);
        }
    }
}