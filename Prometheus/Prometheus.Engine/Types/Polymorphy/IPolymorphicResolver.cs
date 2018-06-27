using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Reflection;

namespace Prometheus.Engine.Types
{
    //TODO: add support for multiple implemented types for token
    public interface IPolymorphicResolver
    {
        /// <summary>
        /// When an identifier has an implementation type known by the application developer, this API provides a way to resolve the polymorphic inference.
        /// </summary>
        /// <exception cref="AmbiguousMatchException">If multiple tokens exists in the type method with the same name</exception>
        Type GetImplementatedType(MethodDeclarationSyntax method, string token);

        /// <summary>
        /// When an identifier has an implementation type known by the application developer, this API provides a way to resolve the polymorphic inference.
        /// </summary>
        /// <exception cref="AmbiguousMatchException">If multiple tokens exists in the type method with the same name</exception>
        void Register(MethodInfo method, string token, Type tokenType);

        /// <summary>
        /// When an identifier has an implementation type known by the application developer, this API provides a way to resolve the polymorphic inference.
        /// </summary>
        /// <exception cref="AmbiguousMatchException">If multiple methods exists in the type with the same name</exception>
        /// <exception cref="AmbiguousMatchException">If multiple tokens exists in the type method with the same name</exception>
        void Register(Type type, string method, string token, Type tokenType);
    }
}