using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;

namespace Prometheus.Engine.Types
{
    public interface ITypeService
    {
        bool TryGetType(string typeName, out Type type);
        List<Type> GetTypes(ExpressionSyntax expressionSyntax);
        List<Type> GetTypes(SyntaxToken syntaxToken);
        bool IsExternal(Type type);
        bool IsPureMethod(SyntaxNode node, out Type returnType);
        ClassDeclarationSyntax GetClassDeclaration(string className);
        ClassDeclarationSyntax GetClassDeclaration(Type type);
        Sort GetSort(Type type);
    }
}
