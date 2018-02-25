using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Prometheus.Common
{
    public static class RoslynExtensions
    {
        public static IEnumerable<T> DescendantNodes<T>(this SyntaxNode node) {
            return node.DescendantNodes().OfType<T>();
        }

        public static IEnumerable<T> DescendantNodes<T>(this SyntaxNode node, Predicate<T> filter) {
            return node.DescendantNodes().OfType<T>().Where(x=>filter(x));
        }

        public static IEnumerable<T> DescendantTokens<T>(this SyntaxNode node, Predicate<T> filter) {
            return node.DescendantTokens().OfType<T>().Where(x => filter(x));
        }

        public static SyntaxNode GetSyntaxNode(this SyntaxTree tree, ReferenceLocation location) {
            return tree.GetRoot().FindNode(location.Location.SourceSpan);
        }

        public static MethodDeclarationSyntax GetMethodDescendant(this SyntaxNode node, string name)
        {
            return node.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(x=>x.Identifier.Text==name);
        }

        public static MethodDeclarationSyntax GetContainingMethod(this Location location)
        {
            MethodDeclarationSyntax callingMethod = location
                .SourceTree
                .GetRoot()
                .FindToken(location.SourceSpan.Start)
                .Parent
                .AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            return callingMethod;
        }

        public static ConstructorDeclarationSyntax GetContainingConstructor(this Location location) {
            ConstructorDeclarationSyntax callingConstructor = location
                .SourceTree
                .GetRoot()
                .FindToken(location.SourceSpan.Start)
                .Parent
                .AncestorsAndSelf()
                .OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault();

            return callingConstructor;
        }

        public static Compilation GetCompilation(this Solution solution, ReferenceLocation location)
        {
            return solution.Projects.First(x => x.ContainsDocument(location.Document.Id)).GetCompilation();
        }

        public static Compilation GetCompilation(this Solution solution, SyntaxNode node) {
            return solution.Projects.First(x => x.Documents.Any(doc => doc.FilePath == node.SyntaxTree.FilePath)).GetCompilation();
        }

        public static IEnumerable<ReferencedSymbol> FindReferences(this Solution solution, SyntaxNode node)
        {
            var methodSymbol = node.GetSemanticModel(GetCompilation(solution, node)).GetDeclaredSymbol(node);
            return SymbolFinder.FindReferencesAsync(methodSymbol, solution).Result;
        }

        public static IEnumerable<ReferenceLocation> FindReferenceLocations(this Solution solution, SyntaxNode node) {
            var methodSymbol = node.GetSemanticModel(GetCompilation(solution, node)).GetDeclaredSymbol(node);
            return FindReferenceLocations(solution, methodSymbol);
        }

        public static T GetNode<T>(this ReferenceLocation referenceLocation)
            where T:SyntaxNode
        {
            SyntaxNode referencingRoot = referenceLocation
                    .Document
                    .GetSyntaxRootAsync()
                    .Result;
            T node = referencingRoot
                    .DescendantNodes<T>()
                    .FirstOrDefault(x => x.ContainsLocation(referenceLocation.Location));

            return node;
        }

        public static bool ContainsLocation(this SyntaxNode node, Location location)
        {
            if(node.GetLocation().SourceSpan.Contains(location.SourceSpan) &&
               node.SyntaxTree == location.SourceTree)
                return true;

            return false;
        }

        public static IEnumerable<ReferenceLocation> FindReferenceLocations(this Solution solution, ISymbol symbol)
        {
            return SymbolFinder.FindReferencesAsync(symbol, solution).Result.SelectMany(x => x.Locations);
        }

        public static IEnumerable<T> AncestorNodes<T>(this SyntaxNode node) {
            return node.Ancestors(false).OfType<T>();
        }

        public static IEnumerable<T> AncestorNodesUntil<T>(this SyntaxNode node, SyntaxNode stopNode) where T:SyntaxNode{
            foreach (var ancestorNode in node.Ancestors(false).OfType<T>())
            {
                if (stopNode != ancestorNode)
                    yield return ancestorNode;

                yield break;
            }
        }

        public static T FirstAncestor<T>(this SyntaxNode node) {
            return node.Ancestors(false).OfType<T>().FirstOrDefault();
        }

        public static List<Type> GetNodeTypes(this MemberAccessExpressionSyntax memberExpression)
        {
            //TODO: this only gets the type for variables with explicit defined type: we don't process "var"
            Queue<string> memberTokens = new Queue<string>(memberExpression.ToString().Split('.'));
            string rootToken = memberTokens.First();
            var typeName = GetTypeName(memberExpression, rootToken);
            memberTokens.Dequeue();
            var types = new List<Type>();

            //todo: obviously there can be multiple classes with the same name
            Type rootType = Assembly.GetCallingAssembly()
                .GetReferencedAssemblies()
                .SelectMany(x => Assembly.Load(x).GetTypes())
                .First(x => x.FullName.Contains(typeName));
            Type type = rootType;
            types.Add(type);

            while (memberTokens.Count > 0)
            {
                var member = type.GetMember(memberTokens.Dequeue(),
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.GetProperty |
                    BindingFlags.GetField)[0];

                switch (member.MemberType)
                {
                    case MemberTypes.Field:
                        type = member.As<FieldInfo>().FieldType;
                        break;
                    case MemberTypes.Property:
                        type = member.As<PropertyInfo>().PropertyType;
                        break;
                    default:
                        throw new NotSupportedException();
                }

                types.Add(type);
            }

            return types;
        }

        public static Type GetNodeType(this IdentifierNameSyntax identifierNameSyntax)
        {
            string typeName = GetTypeName(identifierNameSyntax, identifierNameSyntax.Identifier.Text);

            return GetType(typeName);
        }

        private static string GetTypeName(SyntaxNode node, string rootToken)
        {
            //TODO: this only gets the type for variables with explicit defined type: we don't process "var"
            MethodDeclarationSyntax method = node.GetLocation().GetContainingMethod();
            var parameter = method.ParameterList.Parameters.FirstOrDefault(x => x.Identifier.Text == rootToken);
            string typeName;

            if (parameter != null)
            {
                typeName = parameter.Type.ToString();
            }
            else
            {
                var localDeclaration = method
                    .DescendantNodes<LocalDeclarationStatementSyntax>()
                    .FirstOrDefault(x => x.Declaration.Variables[0].Identifier.Text == rootToken);

                if (localDeclaration != null)
                {
                    typeName = localDeclaration.Declaration.Type.ToString();
                }
                else
                {
                    throw new NotSupportedException("The type name was not found");
                }
            }

            return typeName;
        }

        /// <summary>
        /// Returns the type given the types of the member access expression.
        /// E.g. for "customer.ShippingAddress.City" => {type(Customer), type(Address), type(string)}
        /// </summary>
        private static Type GetType(string typeName)
        {
            //todo: obviously there can be multiple classes with the same name
            Type type = Assembly.GetCallingAssembly()
                .GetReferencedAssemblies()
                .SelectMany(x => Assembly.Load(x).GetTypes())
                .First(x => x.FullName.Contains(typeName));

            return type;
        }

        public static ClassDeclarationSyntax GetClassDeclaration(this Compilation compilation, Type type)
        {
            ClassDeclarationSyntax classDeclaration = compilation
                .SyntaxTrees
                .SelectMany(x => x.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Select(x=> new { Symbol = x.GetSemanticModel(compilation).GetDeclaredSymbol(x), Declaration = x})
                .First(x => x.Symbol.ContainingNamespace.ToString() == type.Namespace && //TODO: x.Symbol.ContainingAssembly.Identity.ToString() == type.Assembly.FullName &&
                            x.Symbol.MetadataName==type.Name)
                .Declaration;

            return classDeclaration;
        }

        public static MemberDeclarationSyntax GetMemberDeclaration(this ClassDeclarationSyntax classDeclaration, string field)
        {
            return classDeclaration.DescendantNodes().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        }

        public static SemanticModel GetSemanticModel(this SyntaxNode node, Compilation compilation)
        {
            SemanticModel model = compilation.GetSemanticModel(node.SyntaxTree.GetRoot().SyntaxTree, true);
            return model;
        }

        public static string GetFullName(this ClassDeclarationSyntax classDeclaration)
        {
            var className = classDeclaration.Identifier.ToString();

            if (classDeclaration.Parent is NamespaceDeclarationSyntax)
            {
                var namespaceDeclaration = classDeclaration.Parent.As<NamespaceDeclarationSyntax>();
                return $"{namespaceDeclaration.Name}.{className}";
            }

            return className;
        }

        public static string GetName(this FieldDeclarationSyntax fieldDeclaration)
        {
            var variable = fieldDeclaration.Declaration.Variables.First();
            return variable.Identifier.Text;
        }

        public static string GetName(this PropertyDeclarationSyntax propertyDeclaration)
        {
            return propertyDeclaration.Identifier.Text;
        }

        public static Compilation GetCompilation(this Project project)
        {
            return project.GetCompilationAsync().Result;
        }

        public static string GetTypeName(this ObjectCreationExpressionSyntax objectCreation)
        {
            var identifierSyntax = objectCreation.Type as IdentifierNameSyntax;
            if (identifierSyntax != null)
            {
                return identifierSyntax.Identifier.ToString();
            }

            throw new NotImplementedException("Currently only IdentiferNameSyntax are allowed");
        }
    }
}