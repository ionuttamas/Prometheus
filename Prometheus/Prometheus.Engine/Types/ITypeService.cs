using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Types
{
    public interface ITypeService
    {
        Type GetType(ExpressionSyntax expressionSyntax);
        Type GetType(SyntaxToken syntaxToken);
    }
}
