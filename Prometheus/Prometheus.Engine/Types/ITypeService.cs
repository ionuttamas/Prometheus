using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;

namespace Prometheus.Engine.Types
{
    public interface ITypeService
    {
        bool TryGetType(string typeName, out Type type);
        TypeContainer GetTypeContainer(SyntaxNode node);
        TypeContainer GetTypeContainer(SyntaxToken syntaxToken);
        bool AreParentChild(TypeContainer first, TypeContainer second);
        bool Is3rdParty(Type type);
        bool IsPureMethod(SyntaxNode node, out Type returnType);
        ClassDeclarationSyntax GetClassDeclaration(string className);
        ClassDeclarationSyntax GetClassDeclaration(Type type);
        Sort GetSort(Type type);
    }
}
