﻿using System;
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
        private readonly Dictionary<string, Type> primitiveTypes;
        private readonly TypeCache typeCache;
        private const string VAR_TOKEN = "var";

        public TypeService(Solution solution)
        {
            //todo: needs to get projects referenced assemblies
            solutionTypes = solution
                .Projects
                .Select(x => Assembly.Load(x.AssemblyName))
                .SelectMany(x => x.DefinedTypes)
                .ToList();
            solutionTypes.AddRange(Assembly.GetAssembly(typeof(int)).DefinedTypes);
            typeCache = new TypeCache();
            primitiveTypes = new Dictionary<string, Type>
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
            if (typeCache.TryGetType(memberExpression, out var cachedType))
                return cachedType;

            var expressionKind = memberExpression.Kind();
            var type = expressionKind == SyntaxKind.SimpleMemberAccessExpression
                ? GetExpressionTypes((MemberAccessExpressionSyntax)memberExpression).Last()
                : GetNodeType((IdentifierNameSyntax)memberExpression);

            typeCache.AddToCache(memberExpression, type);

            return type;
        }

        public Type GetType(SyntaxToken syntaxToken)
        {
            if (typeCache.TryGetType(syntaxToken, out var cachedType))
                return cachedType;

            string typeName = GetTypeName(syntaxToken.GetLocation().GetContainingMethod(), syntaxToken.Text);
            Type type = GetType(typeName);

            typeCache.AddToCache(syntaxToken, type);

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

        #region Type name extraction

        private string GetTypeName(MethodDeclarationSyntax containingMethod, string token) {
            string typeName;

            if (typeCache.TryGetTypeName(containingMethod, token, out var cachedTypeName))
                return cachedTypeName;

            if (ProcessFieldAssignment(containingMethod, token, out typeName))
            {
                typeCache.AddToCache(containingMethod, token, typeName);
                return typeName;
            }
            else if (ProcessPropertyAssignment(containingMethod, token, out typeName))
            {
                typeCache.AddToCache(containingMethod, token, typeName);
                return typeName;
            }
            else if (ProcessParameter(containingMethod, token, out typeName))
            {
                typeCache.AddToCache(containingMethod, token, typeName);
                return typeName;
            }
            else if (ProcessAssignment(containingMethod, token, out typeName))
            {
                typeCache.AddToCache(containingMethod, token, typeName);
                return typeName;
            }

            return null;
        }

        private bool ProcessParameter(MethodDeclarationSyntax containingMethod, string token, out string typeName)
        {
            var parameter = containingMethod
                .ParameterList
                .Parameters
                .FirstOrDefault(x => x.Identifier.Text == token);

            if (parameter != null)
            {
                typeName = parameter.Type.ToString();
                return true;
            }

            typeName = null;
            return false;
        }

        private bool ProcessAssignment(MethodDeclarationSyntax containingMethod, string token, out string typeName) {
            var localDeclaration = containingMethod
                .DescendantNodes<LocalDeclarationStatementSyntax>()
                .FirstOrDefault(x => x.Declaration.Variables[0].Identifier.Text == token);

            if (localDeclaration == null)
            {
                typeName = null;
                return false;
            }

            typeName = localDeclaration.Declaration.Type.ToString();

            if (typeName != VAR_TOKEN)
                return true;

            if (ProcessReferenceAssignment(localDeclaration, out typeName))
                return true;

            if (ProcessClassMethodAssignment(localDeclaration, out typeName))
                return true;

            if (ProcessInstanceMethodAssignment(localDeclaration, out typeName))
                return true;

            if (ProcessStaticMethodAssignment(localDeclaration, out typeName))
                return true;

            return false;
        }

        private bool ProcessReferenceAssignment(LocalDeclarationStatementSyntax declaration, out string typeName)
        {
            var referenceExpression = declaration.Declaration.Variables[0].Initializer.Value;
            var referenceKind = referenceExpression.Kind();
            typeName = null;

            if (referenceKind == SyntaxKind.SimpleMemberAccessExpression) {
                var types = GetExpressionTypes((MemberAccessExpressionSyntax)referenceExpression);
                typeName = types.Last().Name;
                return true;
            }

            if (referenceKind != SyntaxKind.IdentifierName)
                return false;

            var containingMethod = referenceExpression.GetContainingMethod();
            var identifier = referenceExpression.As<IdentifierNameSyntax>().Identifier.Text;

            if (ProcessFieldAssignment(containingMethod, identifier, out typeName))
                return true;

            if (ProcessPropertyAssignment(containingMethod, identifier, out typeName))
                return true;

            typeName = GetTypeName(containingMethod, identifier);
            return true;
        }

        private bool ProcessFieldAssignment(MethodDeclarationSyntax methodDeclaration, string identifier, out string typeName)
        {
            var classDeclaration = methodDeclaration.GetContainingClass();
            var fieldDeclaration = classDeclaration
                .DescendantNodes<FieldDeclarationSyntax>()
                .FirstOrDefault(x => x.Declaration.Variables[0].Identifier.Text == identifier);

            if (fieldDeclaration != null)
            {
                typeName = fieldDeclaration.Declaration.Type.ToString();
                return true;
            }

            typeName = null;
            return false;
        }

        private bool ProcessPropertyAssignment(MethodDeclarationSyntax methodDeclaration, string identifier, out string typeName) {
            var classDeclaration = methodDeclaration.GetContainingClass();
            var propertyDeclaration = classDeclaration
                .DescendantNodes<PropertyDeclarationSyntax>()
                .FirstOrDefault(x => x.Identifier.Text == identifier);

            if (propertyDeclaration != null) {
                typeName = propertyDeclaration.Type.ToString();
                return true;
            }

            typeName = null;
            return false;
        }

        private bool ProcessClassMethodAssignment(LocalDeclarationStatementSyntax declaration, out string typeName) {
            var referenceExpression = declaration.Declaration.Variables[0].Initializer.Value;

            if (referenceExpression.Kind() != SyntaxKind.InvocationExpression) {
                typeName = null;
                return false;
            }

            var invocationExpression = (InvocationExpressionSyntax) referenceExpression;

            if (invocationExpression.Expression.Kind() != SyntaxKind.IdentifierName) {
                typeName = null;
                return false;
            }

            var methodDeclaration = declaration
                .GetContainingClass()
                .DescendantNodes<MethodDeclarationSyntax>()
                .First(x => x.Identifier.Text == invocationExpression.Expression.ToString());

            typeName = methodDeclaration.ReturnType.ToString();

            return true;
        }

        private bool ProcessInstanceMethodAssignment(LocalDeclarationStatementSyntax declaration, out string typeName) {
            var referenceExpression = declaration.Declaration.Variables[0].Initializer.Value;

            if (referenceExpression.Kind() != SyntaxKind.InvocationExpression) {
                typeName = null;
                return false;
            }

            var invocationExpression = (InvocationExpressionSyntax)referenceExpression;

            if (invocationExpression.Expression.Kind() != SyntaxKind.SimpleMemberAccessExpression)
            {
                typeName = null;
                return false;
            }

            //TODO we only process one level calls like "instance.Method()" not nested calls like "instance.[Field|Property|Method()]...Method()"
            var memberExpression = (MemberAccessExpressionSyntax)invocationExpression.Expression;
            var instanceTypeName = GetTypeName(memberExpression.GetContainingMethod(), memberExpression.Expression.ToString());

            if (instanceTypeName == null)
            {
                typeName = null;
                return false;
            }

            var instanceType = GetType(instanceTypeName);
            var method = instanceType.GetMethod(memberExpression.Name.ToString(),
                BindingFlags.Public |
                BindingFlags.Instance);
            typeName = method.ReturnType.Name;

            return true;
        }

        private bool ProcessStaticMethodAssignment(LocalDeclarationStatementSyntax declaration, out string typeName) {
            var referenceExpression = declaration.Declaration.Variables[0].Initializer.Value;

            if (referenceExpression.Kind() != SyntaxKind.InvocationExpression) {
                typeName = null;
                return false;
            }

            var invocationExpression = (InvocationExpressionSyntax)referenceExpression;

            if (invocationExpression.Expression.Kind() != SyntaxKind.SimpleMemberAccessExpression) {
                typeName = null;
                return false;
            }

            //TODO we only process one level calls like "ClassName.StaticMethod()" not nested calls like "ClassName.[StaticMethod()]...Method()"
            var memberExpression = (MemberAccessExpressionSyntax)invocationExpression.Expression;
            var classType= solutionTypes.FirstOrDefault(x => x.Name == memberExpression.Expression.ToString());

            if (classType == null)
            {
                typeName = null;
                return false;
            }

            var method = classType.GetMethod(memberExpression.Name.ToString(),
                BindingFlags.Public |
                BindingFlags.Static);
            typeName = method.ReturnType.Name;

            return true;
        }

        #endregion

        private Type GetType(string typeName) {
            //todo: there can be multiple classes with the same name
            Type type = solutionTypes.FirstOrDefault(x => x.Name == typeName);
            return type ?? primitiveTypes[typeName];
        }
    }
}