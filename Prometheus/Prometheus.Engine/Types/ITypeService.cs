using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;

namespace Prometheus.Engine.Types
{
    public interface ITypeService
    {
        bool TryGetType(string typeName, out Type type);
        Type GetType(ExpressionSyntax expressionSyntax);
        Type GetType(SyntaxToken syntaxToken);
        bool IsExternal(Type type);
        bool IsPureMethod(SyntaxNode node, out Type returnType);
        ClassDeclarationSyntax GetClassDeclaration(string className);
        ClassDeclarationSyntax GetClassDeclaration(Type type);
        Sort GetSort(Type type);
    }
}
