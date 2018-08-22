using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;

namespace Prometheus.Engine.Types
{
    public interface ITypeService
    {
        bool TryGetType(string typeName, out Type type);
        TypeContainer GetTypeContainer(ExpressionSyntax expressionSyntax);
        TypeContainer GetTypeContainer(SyntaxToken syntaxToken);
        bool AreParentChild(TypeContainer first, TypeContainer second);
        bool IsExternal(Type type);
        bool IsPureMethod(SyntaxNode node, out Type returnType);
        ClassDeclarationSyntax GetClassDeclaration(string className);
        ClassDeclarationSyntax GetClassDeclaration(Type type);
        Sort GetSort(Type type);
    }
}
