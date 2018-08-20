using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;

namespace Prometheus.Engine.Types.Polymorphy
{
    public class PolymorphicResolver : IPolymorphicResolver
    {
        private readonly Dictionary<MethodInfo, Dictionary<string, List<Type>>> methodInfoTable;
        private readonly Dictionary<Type, Dictionary<string, Dictionary<string, List<Type>>>> typeMethodTable;

        public PolymorphicResolver()
        {
            methodInfoTable = new Dictionary<MethodInfo, Dictionary<string, List<Type>>>();
            typeMethodTable = new Dictionary<Type, Dictionary<string, Dictionary<string, List<Type>>>>();
        }

        public bool TryGetImplementationTypes(MethodDeclarationSyntax method, string token, out List<Type> implementationTypes) {
            var classDeclaration = method.GetContainingClass();
            var methodName = method.Identifier.Text;

            var tokenTypeEntry = methodInfoTable.FirstOrDefault(x => x.Key.Name == classDeclaration.Identifier.Text &&
                                                x.Key.Name == methodName &&
                                                AreEquivalent(x.Key, method));

            if (!tokenTypeEntry.IsNull())
            {
                implementationTypes = tokenTypeEntry.Value[token];
                return true;
            }

            var typeEntry = typeMethodTable.FirstOrDefault(x => x.Key.Name == classDeclaration.Identifier.Text);

            if (!typeEntry.IsNull())
            {
                var methodEntry = typeEntry.Value.FirstOrDefault(x => x.Key == methodName &&
                                                    AreEquivalent(typeEntry.Key.GetMethod(methodName), method));

                if (!methodEntry.IsNull())
                {
                    implementationTypes = methodEntry.Value[token];
                    return true;
                }
            }

            implementationTypes = null;
            return false;
        }

        public void Register(MethodInfo method, string token, params Type[] implementationTypes)
        {
            if (!methodInfoTable.ContainsKey(method))
            {
                methodInfoTable[method] = new Dictionary<string, List<Type>>();
            }

            if (!methodInfoTable[method].ContainsKey(token)) {
                methodInfoTable[method][token] = new List<Type>();
            }

            methodInfoTable[method][token].AddRange(implementationTypes);
        }

        public void Register(Type classType, string method, string token, params Type[] implementationTypes) {
            if (classType.GetMethods().Count(x => x.Name == method) != 1)
                throw new AmbiguousMatchException($"Type {classType} contains more than one method with name {method}");

            if (!typeMethodTable.ContainsKey(classType)) {
                typeMethodTable[classType] = new Dictionary<string, Dictionary<string, List<Type>>>();
            }

            if (!typeMethodTable[classType].ContainsKey(method))
            {
                typeMethodTable[classType][method] = new Dictionary<string, List<Type>>();
            }

            if (!typeMethodTable[classType][method].ContainsKey(token)) {
                typeMethodTable[classType][method][token] = new List<Type>();
            }

            typeMethodTable[classType][method][token].AddRange(implementationTypes);
        }

        private static bool AreEquivalent(MethodInfo methodInfo, MethodDeclarationSyntax methodDeclaration)
        {
            var methodInfoParams = methodInfo.GetParameters();
            var methodDeclarationParams = methodDeclaration.ParameterList.Parameters;

            if (methodDeclarationParams.Count != methodInfoParams.Length)
                return false;

            for (int i = 0; i < methodDeclarationParams.Count; i++)
            {
                var declarationType = methodDeclarationParams[i].Type.ToString();

                if (declarationType != methodInfoParams[i].ParameterType.Name)
                    return false;
            }

            return true;
        }
    }
}