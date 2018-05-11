using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;

namespace Prometheus.Engine.Types
{
    public interface ITypeService
    {
        Type GetType(ExpressionSyntax expressionSyntax);
        Type GetType(SyntaxToken syntaxToken);
        ClassDeclarationSyntax GetClassDeclaration(Type type);
        Sort GetSort(Context context, Type type);
    }
}
