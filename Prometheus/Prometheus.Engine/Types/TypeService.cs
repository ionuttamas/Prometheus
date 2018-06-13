using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using TypeInfo = System.Reflection.TypeInfo;

namespace Prometheus.Engine.Types
{
    internal class TypeService : ITypeService
    {
        private readonly List<TypeInfo> solutionTypes;
        private readonly Dictionary<TypeInfo, List<TypeInfo>> interfaceImplementations;
        private readonly List<ClassDeclarationSyntax> classDeclarations;
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
            //todo: support abstract classes, virtual methods as well
            interfaceImplementations = solutionTypes
                .Where(x => x.IsInterface)
                .ToDictionary(x => x, x => solutionTypes.Where(t => t.ImplementedInterfaces.Contains(x)).ToList());
            solutionTypes.AddRange(Assembly.GetAssembly(typeof(int)).DefinedTypes);
            classDeclarations = solution.Projects
                .SelectMany(x => x.GetCompilation().SyntaxTrees)
                .Select(x => x.GetRoot())
                .SelectMany(x => x.DescendantNodes<ClassDeclarationSyntax>())
                .ToList();
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

            Type type;
            var lambdaExpression = memberExpression.AncestorNodes<SimpleLambdaExpressionSyntax>().FirstOrDefault();

            if (lambdaExpression != null && (memberExpression.ToString() == lambdaExpression.Parameter.ToString() || memberExpression.ToString().StartsWith($"{lambdaExpression.Parameter}.")))
            {
                type = GetLambdaExpressionMemberType(memberExpression);
            }
            else
            {
                var expressionKind = memberExpression.Kind();
                type = expressionKind == SyntaxKind.SimpleMemberAccessExpression
                    ? GetExpressionTypes(memberExpression.As<MemberAccessExpressionSyntax>()).Last()
                    : GetNodeType(memberExpression.As<IdentifierNameSyntax>());
            }

            typeCache.AddToCache(memberExpression, type);

            return type;
        }

        public Type GetType(SyntaxToken syntaxToken)
        {
            if (typeCache.TryGetType(syntaxToken, out var cachedType))
                return cachedType;

            string typeName = GetTypeName(syntaxToken.GetLocation().GetContainingMethod(), syntaxToken.Text, out Type type);
            Type result = type ??  GetType(typeName);
            typeCache.AddToCache(syntaxToken, result);

            return result;
        }

        public List<TypeInfo> GetImplementations(Type @interface)
        {
            return interfaceImplementations[@interface.GetTypeInfo()];
        }

        public ClassDeclarationSyntax GetClassDeclaration(Type type)
        {
            //TODO: handle multiple same name classnames in different namespaces
            ClassDeclarationSyntax classDeclaration = classDeclarations.FirstOrDefault(x => x.Identifier.Text == type.Name);

            return classDeclaration;
        }

        public ClassDeclarationSyntax GetClassDeclaration(string className) {
            //TODO: handle multiple same name classnames in different namespaces
            ClassDeclarationSyntax classDeclaration = classDeclarations.FirstOrDefault(x => x.Identifier.Text == className);

            return classDeclaration;
        }

        public Sort GetSort(Context context, Type type)
        {
            if (type.IsNumeric())
                return context.RealSort; // there are issues comparing int to real

            if (type.IsString())
                return context.StringSort;

            if (type.IsBoolean())
                return context.BoolSort;

            throw new ArgumentException($"Type {type} is not supported");
        }

        private Type GetLambdaExpressionMemberType(ExpressionSyntax memberExpression) {
            var lambdaExpression = memberExpression.AncestorNodes<SimpleLambdaExpressionSyntax>().FirstOrDefault();
            var invocation = lambdaExpression.AncestorNodes<InvocationExpressionSyntax>().First();
            var instance = invocation
                .Expression.As<MemberAccessExpressionSyntax>()
                .Expression.As<IdentifierNameSyntax>();
            var itemType = GetCollectionItemType(instance);
            Type type = GetExpressionTypes(itemType, memberExpression.As<MemberAccessExpressionSyntax>()).Last();

            return type;
        }

        private Type GetCollectionItemType(IdentifierNameSyntax instance)
        {
            var collectionTypeName = GetTypeName(instance.GetContainingMethod(), instance.ToString(), out var collectionType);
            Type type;

            if (collectionType != null)
            {
                return collectionType.GenericTypeArguments.Length > 0 ?
                    collectionType.GenericTypeArguments[0] :
                    collectionType.GetElementType();
            }

            switch (collectionTypeName)
            {
                case string @string when @string.IsMatch("List<(.*)>", out var name):
                    type = GetType(name);
                    break;
                case string @string when @string.IsMatch("IEnumerable<(.*)>", out var name):
                    type = GetType(name);
                    break;
                case string @string when @string.IsMatch(@"(.*)\[\]", out var name):
                    type = GetType(name);
                    break;
                default:
                    throw new NotSupportedException("Only IEnumerable<T>, List<T> or T[] collection types are supported");
            }

            return type;
        }

        /// <summary>
        /// Gets all the types of a given member expression.
        /// E.g. for person.Address.Street returns {typeof(Person), typeof(Address), typeof(string)}
        /// </summary>
        private List<Type> GetExpressionTypes(MemberAccessExpressionSyntax memberExpression) {
            Queue<string> memberTokens = new Queue<string>(memberExpression.ToString().Split('.'));
            string rootToken = memberTokens.First();
            string typeName = GetTypeName(memberExpression.GetContainingMethod(), rootToken, out var type);
            Type rootType = type ?? GetType(typeName);

            return GetExpressionTypes(rootType, memberExpression);
        }

        private List<Type> GetExpressionTypes(Type rootType, MemberAccessExpressionSyntax memberExpression)
        {
            Queue<string> memberTokens = new Queue<string>(memberExpression.ToString().Split('.'));
            memberTokens.Dequeue();

            var types = new List<Type>();
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
        private Type GetNodeType(IdentifierNameSyntax identifierNameSyntax)
        {
            Type type;
            string typeName = GetTypeName(identifierNameSyntax.GetContainingMethod(), identifierNameSyntax.Identifier.Text, out type);

            return type ?? GetType(typeName);
        }

        #region Type name extraction

        private string GetTypeName(MethodDeclarationSyntax containingMethod, string token, out Type type) {
            string typeName;
            type = null;

            if (ProcessFieldAssignment(containingMethod, token, out typeName))
                return typeName;

            if (ProcessPropertyAssignment(containingMethod, token, out typeName))
                return typeName;

            if (ProcessParameter(containingMethod, token, out typeName))
                return typeName;

            if (ProcessAssignment(containingMethod, token, out typeName, out type))
                return typeName;

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

        private bool ProcessAssignment(MethodDeclarationSyntax containingMethod, string token, out string typeName, out Type type) {
            var localDeclaration = containingMethod
                .FirstDescendantNode<LocalDeclarationStatementSyntax>(x => x.Declaration.Variables[0].Identifier.Text == token);
            type = null;
            typeName = null;

            if (localDeclaration == null)
                return false;

            typeName = localDeclaration.Declaration.Type.ToString();

            if (typeName != VAR_TOKEN)
                return true;

            if (ProcessReferenceAssignment(localDeclaration, out typeName, out type))
                return true;

            if (ProcessClassMethodAssignment(localDeclaration, out typeName))
                return true;

            if (ProcessInstanceMethodAssignment(localDeclaration, out type))
            {
                typeName = type.Name;
                return true;
            }

            if (ProcessStaticMethodAssignment(localDeclaration, out typeName))
                return true;

            return false;
        }

        private bool ProcessReferenceAssignment(LocalDeclarationStatementSyntax declaration, out string typeName, out Type type)
        {
            var referenceExpression = declaration.Declaration.Variables[0].Initializer.Value;
            var referenceKind = referenceExpression.Kind();
            typeName = null;
            type = null;

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

            typeName = GetTypeName(containingMethod, identifier, out type);
            return true;
        }

        private bool ProcessFieldAssignment(MethodDeclarationSyntax methodDeclaration, string identifier, out string typeName)
        {
            var classDeclaration = methodDeclaration.GetContainingClass();
            var fieldDeclaration = classDeclaration
                .FirstDescendantNode<FieldDeclarationSyntax>(x => x.Declaration.Variables[0].Identifier.Text == identifier);

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
            typeName = null;

            if (referenceExpression.Kind() != SyntaxKind.InvocationExpression) {
                return false;
            }

            var invocationExpression = (InvocationExpressionSyntax) referenceExpression;

            if (invocationExpression.Expression.Kind() != SyntaxKind.IdentifierName) {
                return false;
            }

            var methodDeclaration = declaration
                .GetContainingClass()
                .DescendantNodes<MethodDeclarationSyntax>()
                .First(x => x.Identifier.Text == invocationExpression.Expression.ToString());

            typeName = methodDeclaration.ReturnType.ToString();

            return true;
        }

        private bool ProcessInstanceMethodAssignment(LocalDeclarationStatementSyntax declaration, out Type type) {
            var referenceExpression = declaration.Declaration.Variables[0].Initializer.Value;
            type = null;

            if (referenceExpression.Kind() != SyntaxKind.InvocationExpression) {
                return false;
            }

            var invocationExpression = (InvocationExpressionSyntax)referenceExpression;

            if (invocationExpression.Expression.Kind() != SyntaxKind.SimpleMemberAccessExpression)
            {
                return false;
            }

            //TODO we only process one level calls like "instance.Method()" not nested calls like "instance.[Field|Property|Method()]...Method()"
            var memberExpression = (MemberAccessExpressionSyntax)invocationExpression.Expression;
            var instanceTypeName = GetTypeName(memberExpression.GetContainingMethod(), memberExpression.Expression.ToString(), out type);

            if (instanceTypeName == null)
                return false;

            var instanceType = GetType(instanceTypeName);
            var method = instanceType.GetMethod(memberExpression.Name.ToString(),
                BindingFlags.Public |
                BindingFlags.Instance);
            type = method.ReturnType;

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