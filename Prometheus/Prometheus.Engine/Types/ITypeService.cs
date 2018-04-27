using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Types
{
    public interface ITypeService
    {
        /// <summary>
        /// Gets the type of the expression syntax.
        /// </summary>
        /// <returns></returns>
        Type GetType(ExpressionSyntax expressionSyntax);
    }
}
