using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.Model;
using TypeInfo = System.Reflection.TypeInfo;

namespace Prometheus.Engine.Types
{
    internal class TypeService : ITypeService
    {
        private readonly IPolymorphicResolver polymorphicService;
        private readonly ModelStateConfiguration modelConfig;
        private readonly List<TypeInfo> solutionTypes;
        private readonly List<TypeInfo> externalTypes;
        private readonly Dictionary<TypeInfo, List<TypeInfo>> interfaceImplementations;
        private readonly List<ClassDeclarationSyntax> classDeclarations;
        private readonly Dictionary<string, Type> primitiveTypes;
        private readonly Dictionary<string, Type> coreTypes;
        private readonly Dictionary<Type, EnumSort> enumSorts;
        private readonly Dictionary<Type, Sort> typeSorts;
        private readonly TypeCache typeCache;
        private readonly Context context;
        private const string VAR_TOKEN = "var";
        private readonly Regex genericRegex = new Regex(@"(.*)<(.*)>");

        public TypeService(Solution solution, Context context, IPolymorphicResolver polymorphicService, ModelStateConfiguration modelConfig, params string[] projects)
        {
            this.polymorphicService = polymorphicService;
            this.modelConfig = modelConfig;
            this.context = context;
            var solutionAssemblies = solution
                .Projects
                .Where(x => projects == null || projects.Contains(x.Name))
                .Select(x => Assembly.Load(x.AssemblyName))
                .ToList();
            solutionTypes = solutionAssemblies
                .SelectMany(x => x.DefinedTypes)
                .ToList();
            var externalAssemblies = solutionAssemblies.SelectMany(x => x.GetReferencedAssemblies())
                .Where(x => projects == null || !projects.Contains(x.Name))
                .DistinctBy(x=>x.FullName)
                .Where(x=> !x.Name.StartsWith("System") && !x.Name.Contains("mscorlib"))
                .Select(x => Assembly.Load(x.FullName))
                .ToList();
            externalTypes = externalAssemblies
                .SelectMany(x => x.DefinedTypes)
                .ToList();
            coreTypes = typeof(int).Assembly.DefinedTypes.DistinctBy(x=>x.Name).ToDictionary(x => x.Name, x => x.AsType());
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
                {"Byte", typeof(byte)},
                {"sbyte",typeof(sbyte)},
                {"SByte",typeof(sbyte)},
                {"short", typeof(short)},
                {"Int16", typeof(short)},
                {"ushort", typeof(ushort)},
                {"UInt16", typeof(ushort)},
                {"int", typeof(int)},
                {"Int32", typeof(int)},
                {"uint", typeof(uint)},
                {"UInt32", typeof(uint)},
                {"long", typeof(long)},
                {"Int64", typeof(long)},
                {"ulong", typeof(ulong)},
                {"UInt64", typeof(ulong)},
                {"float", typeof(float)},
                {"Single", typeof(float)},
                {"double", typeof(double)},
                {"Double", typeof(double)},
                {"decimal", typeof(decimal)},
                {"Decimal", typeof(decimal)},
                {"bool", typeof(bool)},
                {"Boolean", typeof(bool)},
                {"char", typeof(char)},
                {"Char", typeof(char)},
                {"byte?", typeof(byte?)},
                {"Byte?", typeof(byte?)},
                {"sbyte?",typeof(sbyte?)},
                {"SByte?",typeof(sbyte?)},
                {"short?", typeof(short?)},
                {"Int16?", typeof(short?)},
                {"ushort?", typeof(ushort?)},
                {"UInt16?", typeof(ushort?)},
                {"int?", typeof(int?)},
                {"Int32?", typeof(int?)},
                {"uint?", typeof(uint?)},
                {"UInt32?", typeof(uint?)},
                {"long?", typeof(long?)},
                {"Int64?", typeof(long?)},
                {"ulong?", typeof(ulong?)},
                {"UInt64?", typeof(ulong?)},
                {"float?", typeof(float?)},
                {"Single?", typeof(float?)},
                {"double?", typeof(double?)},
                {"Double?", typeof(double?)},
                {"decimal?", typeof(decimal?)},
                {"Decimal?", typeof(decimal?)},
                {"bool?", typeof(bool?)},
                {"Bool?", typeof(bool?)},
                {"char?", typeof(char?)},
                {"Char?", typeof(char?)},
                {"string", typeof(string)},
                {"String", typeof(string)}
            };
            //TODO: allow generics
            var types = solutionTypes.Where(x =>
                x.IsClass &&
                (!x.IsAbstract || !x.IsSealed) &&
                !x.IsGenericType &&
                !x.GetTypeInfo().IsDefined(typeof(CompilerGeneratedAttribute), true));
            ConstructSorts(types);
        }

        public bool TryGetType(string typeName, out Type type) {
            //todo: there can be multiple classes with the same name

            if (genericRegex.IsMatch(typeName))
                return TryGetGenericType(typeName, out type);

            type = solutionTypes.FirstOrDefault(x => x.Name == typeName);

            if (type == null)
            {
                type = (primitiveTypes.ContainsKey(typeName) ?
                           primitiveTypes[typeName] :
                           externalTypes.FirstOrDefault(x => x.Name == typeName)) ??
                           (coreTypes.ContainsKey(typeName) ? coreTypes[typeName] : null);
            }

            if (type == null)
                return false;

            return true;
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

        public bool IsExternal(Type type)
        {
            return externalTypes.Contains(type);
        }

        public bool IsPureMethod(SyntaxNode node, out Type returnType) {
            var invocation = node.As<InvocationExpressionSyntax>();
            var memberAccess = invocation.Expression.As<MemberAccessExpressionSyntax>();
            var expression = memberAccess.Expression.As<IdentifierNameSyntax>();
            var method = memberAccess.Name.Identifier.Text;

            if (!TryGetType(expression.Identifier.Text, out Type type)) {
                type = GetType(expression);
            }

            returnType = type.GetMethod(method).ReturnType;

            return modelConfig.IsMethodPure(type, method);
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
            var nullConstructor = context.MkConstructor("null", "is_null");
            var constructor = context.MkConstructor(type.FullName, $"is_{type.FullName}", memberSorts.Keys.ToArray(), memberSorts.Values.ToArray());
            var sort = context.MkDatatypeSort(type.Name, new[] { nullConstructor, constructor });
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
                    TryGetType(name, out type);
                    break;
                case string @string when @string.IsMatch("IEnumerable<(.*)>", out var name):
                    TryGetType(name, out type);
                    break;
                case string @string when @string.IsMatch(@"(.*)\[\]", out var name):
                    TryGetType(name, out type);
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

            var classType = solutionTypes.FirstOrDefault(x => x.Name == rootToken);
            classType = classType ?? externalTypes.FirstOrDefault(x => x.Name == rootToken);

            if (classType != null)
                return new List<Type> {classType};

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

            if (type == null && !TryGetType(typeName, out type))
            {
                throw new ArgumentException($"No type was found for {typeName}");
            }

            //TODO: handle abstract classes
            if (!type.IsInterface)
                return type;

            var typeInfo = type.GetTypeInfo();

            var implementationType = interfaceImplementations[typeInfo].Count==1 ?
                interfaceImplementations[typeInfo].First() :
                polymorphicService.GetImplementatedType(method, token);

            return implementationType;
        }

        private bool TryGetGenericType(string typeName, out Type type)
        {
            //TODO: currently handles only one parameter generic types
            type = null;
            var match = genericRegex.Match(typeName);
            string containerTypeName = $"{match.Groups[1].Value}`1";
            string parameterTypeName = match.Groups[2].Value;

            if(!TryGetType(containerTypeName, out var containerType))
                throw new ArgumentException($"{containerTypeName} was not found as a generic type");

            if (!TryGetType(parameterTypeName, out var parameterType))
                throw new ArgumentException($"{parameterTypeName} was not found as a generic type");

            type = containerType.MakeGenericType(parameterType);

            return true;
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

            if (!TryGetType(instanceTypeName, out var instanceType))
                return false;

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
            classType = classType ?? externalTypes.FirstOrDefault(x => x.Name == memberExpression.Expression.ToString());

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

        //TODO: this and generics
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
            var arraySort = context.MkArraySort(context.IntSort, elementSort);

            return arraySort;
        }
    }
}