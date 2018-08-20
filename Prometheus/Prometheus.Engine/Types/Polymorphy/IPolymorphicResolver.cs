using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Types.Polymorphy
{
    //TODO: add support for multiple implemented types for token
    public interface IPolymorphicResolver
    {
        /// <summary>
        /// When an identifier has an implementation type known by the application developer, this API provides a way to resolve the polymorphic inference.
        /// </summary>
        bool TryGetImplementationTypes(MethodDeclarationSyntax method, string token, out List<Type> implementationTypes);

        /// <summary>
        /// When an identifier can have certain implementation types known by the application developer, this API provides a way to resolve the polymorphic inference.
        /// On processing, it will go through all registered implementation types in parallel.
        /// </summary>
        /// <exception cref="AmbiguousMatchException">If multiple tokens exists in the type method with the same name</exception>
        void Register(MethodInfo method, string token, params Type[] implementationTypes);

        /// <summary>
        /// When an identifier has an implementation type known by the application developer, this API provides a way to resolve the polymorphic inference.
        /// On processing, it will go through all registered implementation types in parallel.
        /// </summary>
        /// <exception cref="AmbiguousMatchException">If multiple methods exists in the type with the same name</exception>
        /// <exception cref="AmbiguousMatchException">If multiple tokens exists in the type method with the same name</exception>
        void Register(Type type, string method, string token, params Type[] implementationTypes);
    }
}