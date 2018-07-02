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
        private readonly IPolymorphicResolver polymorphicService;
        private readonly List<TypeInfo> solutionTypes;
        private readonly Dictionary<TypeInfo, List<TypeInfo>> interfaceImplementations;
        private readonly List<ClassDeclarationSyntax> classDeclarations;
        private readonly Dictionary<string, Type> primitiveTypes;
        private readonly Dictionary<Type, EnumSort> enumSorts;
        private readonly Dictionary<Type, Sort> typeSorts;
        private readonly TypeCache typeCache;
        private readonly Context context;
        private const string VAR_TOKEN = "var";

        public TypeService(Solution solution, Context context, IPolymorphicResolver polymorphicService, params string[] projects)
        {
            this.polymorphicService = polymorphicService;
            this.context = context;
            //todo: needs to get projects referenced assemblies
            solutionTypes = solution
                .Projects
                .Where(x => projects==null || projects.Contains(x.Name))
                .Select(x => Assembly.Load(x.AssemblyName))
                .SelectMany(x => x.DefinedTypes)
                .ToList();
            typeSorts = new Dictionary<Type, Sort>();
            enumSorts = solutionTypes
                .Where(x => x.IsEnum)
                .ToDictionary(x=>x.AsType(), x => default(EnumSort));
            interfaceImplementations = solutionTypes
                .Where(x => x.IsInterface)
                .ToDictionary(x => x, x => solutionTypes.Where(t => t.ImplementedInterfaces.Contains(x)).ToList());
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
            ConstructSorts(solutionTypes.Where(x => x.IsClass && (!x.IsAbstract || !x.IsSealed)));
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
                    : GetImplementedType(memberExpression.GetContainingMethod(), memberExpression.As<IdentifierNameSyntax>().Identifier.Text);
            }

            typeCache.AddToCache(memberExpression, type);

            return type;
        }

        public Type GetType(SyntaxToken syntaxToken)
        {
            if (typeCache.TryGetType(syntaxToken, out var cachedType))
                return cachedType;

            MethodDeclarationSyntax containingMethod = syntaxToken.GetLocation().GetContainingMethod();
            Type result = GetImplementedType(containingMethod, syntaxToken.Text);
            typeCache.AddToCache(syntaxToken, result);

            return result;
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

        public Sort GetSort(Type type)
        {
            return typeSorts[type];
        }

        #region Sort construction
        private void ConstructSorts(IEnumerable<Type> types) {
            foreach (var primitiveType in primitiveTypes.Values)
            {
                ConstructSort(primitiveType);
            }

            foreach (var referenceType in types) {
                ConstructSort(referenceType);
            }
        }

        private Sort ConstructSort(Type type) {
            Sort sort = null;

            if (typeSorts.ContainsKey(type))
                return typeSorts[type];

            if (type.IsSimple() || type.IsEnum) {
                sort = ConstructSimpleSort(type);
            }
            else if (type.IsClass) {
                sort = ConstructReferenceSort(type);
            }

            if (sort != null) {
                typeSorts[type] = sort;
                return sort;
            }

            throw new ArgumentException($"Type {type} is not supported");
        }

        private Sort ConstructSimpleSort(Type type) {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingSort = ConstructSimpleSort(Nullable.GetUnderlyingType(type));
                var constructor = context.MkConstructor(type.FullName, $"is_{type.FullName}", new []{"HasValue", "Value" }, new[] { context.BoolSort, underlyingSort });
                var sort = context.MkDatatypeSort(type.FullName, new[] { constructor });

                return sort;
            }

            if (type.IsNumeric() && !type.IsEnum)
                return context.RealSort; // TODO: there are issues comparing int to real

            if (type.IsString())
                return context.StringSort;

            if (type.IsBoolean())
                return context.BoolSort;

            if (type == typeof(char))
                return context.IntSort;

            if (type.IsEnum) {
                if (enumSorts[type] != null)
                    return enumSorts[type];

                var enumValues = type
                    .GetMembers(BindingFlags.Public | BindingFlags.Static)
                    .Select(x => x.Name)
                    .ToArray();
                var enumSort = context.MkEnumSort(type.Name, enumValues);
                enumSorts[type] = enumSort;

                return enumSort;
            }

            throw new ArgumentException($"Primitive type {type} is not supported");
        }

        private Sort ConstructReferenceSort(Type type) {
            var memberSorts = type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.MemberType == MemberTypes.Field || x.MemberType == MemberTypes.Property)
                .ToDictionary(x => x.Name, x => ConstructSort(x.GetMemberType()));
            var constructor = context.MkConstructor(type.FullName, $"is_{type.FullName}", memberSorts.Keys.ToArray(), memberSorts.Values.ToArray());
            var sort = context.MkDatatypeSort(type.Name, new[] { constructor });

            return sort;
        }
        #endregion

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
            var rootType = GetImplementedType(memberExpression.GetContainingMethod(), rootToken);
            var expressionTypes = GetExpressionTypes(rootType, memberExpression);

            return expressionTypes;
        }

        private List<Type> GetExpressionTypes(Type rootType, MemberAccessExpressionSyntax memberExpression)
        {
            Queue<string> memberTokens = new Queue<string>(memberExpression.ToString().Split('.'));
            memberTokens.Dequeue();

            var types = new List<Type>();
            Type currentType = rootType;
            types.Add(currentType);

            if (currentType.IsEnum)
                return types;

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

        private Type GetImplementedType(MethodDeclarationSyntax method, string token)
        {
            var enumType = enumSorts.FirstOrDefault(x => x.Key.Name == token);

            if (!enumType.IsNull())
                return enumType.Key;

            string typeName = GetTypeName(method, token, out var type);
            type = type ?? GetType(typeName);

            //TODO: handle abstract classes
            if (!type.IsInterface)
                return type;

            var typeInfo = type.GetTypeInfo();

            var implementationType = interfaceImplementations[typeInfo].Count==1 ?
                interfaceImplementations[typeInfo].First() :
                polymorphicService.GetImplementatedType(method, token);

            return implementationType;
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

        private Sort GetListSort(Sort elementSort)
        {
            var listSort = context.MkListSort($"{elementSort.Name}_list", elementSort);

            return listSort;
        }

        private Sort GetSetSort(Sort elementSort) {
            var setSort = context.MkSetSort(elementSort);

            return setSort;
        }

        private Sort GetArraySort(Sort elementSort) {
            var setSort = context.MkArraySort(elementSort, context.IntSort);

            return setSort;
        }
    }
}