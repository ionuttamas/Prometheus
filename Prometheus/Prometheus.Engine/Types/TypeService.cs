using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using TypeInfo = System.Reflection.TypeInfo;

namespace Prometheus.Engine.Types
{
    internal class TypeService : ITypeService
    {
        private readonly List<TypeInfo> solutionTypes;
        private readonly Dictionary<string, Type> coreTypes;

        public TypeService(Solution solution)
        {
            //todo: needs to get projects referenced assemblies
            solutionTypes = solution.Projects.Select(x => Assembly.Load(x.AssemblyName)).SelectMany(x => x.DefinedTypes)
                .ToList();
            solutionTypes.AddRange(Assembly.GetAssembly(typeof(int)).DefinedTypes);
            coreTypes = new Dictionary<string, Type>
            {
                {"byte", typeof(byte)},
                {"sbyte",typeof(sbyte)},
                {"short", typeof(short)},
                {"ushort", typeof(ushort)},
                {"int", typeof(int)},
                {"uint", typeof(uint)},
                {"long", typeof(long)},
                {"ulong", typeof(ulong)},
                {"float", typeof(float)},
                {"double", typeof(double)},
                {"decimal", typeof(decimal)},
                {"object", typeof(object)},
                {"bool", typeof(bool)},
                {"char", typeof(char)},
                {"byte?", typeof(byte?)},
                {"sbyte?",typeof(sbyte?)},
                {"short?", typeof(short?)},
                {"ushort?", typeof(ushort?)},
                {"int?", typeof(int?)},
                {"uint?", typeof(uint?)},
                {"long?", typeof(long?)},
                {"ulong?", typeof(ulong?)},
                {"float?", typeof(float?)},
                {"double?", typeof(double?)},
                {"decimal?", typeof(decimal?)},
                {"bool?", typeof(bool?)},
                {"char?", typeof(char?)},
                {"string", typeof(string)}
            };
        }

        public Type GetType(ExpressionSyntax memberExpression)
        {
            var expressionKind = memberExpression.Kind();
            var type = expressionKind == SyntaxKind.SimpleMemberAccessExpression
                ? GetExpressionTypes((MemberAccessExpressionSyntax)memberExpression).Last()
                : GetNodeType((IdentifierNameSyntax)memberExpression);

            return type;
        }

        /// <summary>
        /// Gets all the types of a given member expression.
        /// E.g. for person.Address.Street returns {typeof(Person), typeof(Address), typeof(string)}
        /// </summary>
        private List<Type> GetExpressionTypes(MemberAccessExpressionSyntax memberExpression) {
            Queue<string> memberTokens = new Queue<string>(memberExpression.ToString().Split('.'));
            string rootToken = memberTokens.First();
            var typeName = GetTypeName(memberExpression.GetContainingMethod(), rootToken);
            memberTokens.Dequeue();

            var types = new List<Type>();
            Type rootType = GetType(typeName);
            Type currentType = rootType;
            types.Add(currentType);

            while (memberTokens.Count > 0) {
                var member = currentType.GetMember(memberTokens.Dequeue(),
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.GetProperty |
                    BindingFlags.GetField)[0];

                switch (member.MemberType) {
                    case MemberTypes.Field:
                        currentType = member.As<FieldInfo>().FieldType;
                        break;
                    case MemberTypes.Property:
                        currentType = member.As<PropertyInfo>().PropertyType;
                        break;
                    default:
                        throw new NotSupportedException();
                }

                types.Add(currentType);
            }

            return types;
        }

        /// <summary>
        /// Gets the type for a given identifier.
        /// </summary>
        private Type GetNodeType(IdentifierNameSyntax identifierNameSyntax) {
            string typeName = GetTypeName(identifierNameSyntax.GetContainingMethod(), identifierNameSyntax.Identifier.Text);
            Type type = GetType(typeName);

            return type;
        }

        private static string GetTypeName(MethodDeclarationSyntax containingMethod, string rootToken) {
            //TODO: this only gets the type for variables with explicit defined type: we don't process "var"
            var parameter = containingMethod.ParameterList.Parameters.FirstOrDefault(x => x.Identifier.Text == rootToken);
            string typeName;

            if (parameter != null) {
                typeName = parameter.Type.ToString();
            } else {
                var localDeclaration = containingMethod
                    .DescendantNodes<LocalDeclarationStatementSyntax>()
                    .FirstOrDefault(x => x.Declaration.Variables[0].Identifier.Text == rootToken);

                if (localDeclaration != null) {
                    typeName = localDeclaration.Declaration.Type.ToString();
                } else {
                    throw new NotSupportedException("The type name was not found");
                }
            }

            return typeName;
        }

        private Type GetType(string typeName) {
            //todo: there can be multiple classes with the same name
            Type type = solutionTypes.FirstOrDefault(x => x.Name == typeName);
            return type ?? coreTypes[typeName];
        }
    }
}