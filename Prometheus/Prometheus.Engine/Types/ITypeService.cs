using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;

namespace Prometheus.Engine.Types
{
    public interface ITypeService
    {
        Type GetType(ExpressionSyntax expressionSyntax);
        Type GetType(SyntaxToken syntaxToken);
        bool IsExternal(Type type;
        ClassDeclarationSyntax GetClassDeclaration(string className);
        ClassDeclarationSyntax GetClassDeclaration(Type type);
        Sort GetSort(Type type);
    }
}
