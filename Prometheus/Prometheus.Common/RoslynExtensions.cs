using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.FindSymbols;
using SyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Prometheus.Common
{
    public static class RoslynExtensions
    {
        private static readonly Dictionary<Project, Compilation> CompilationCache = new Dictionary<Project, Compilation>();
        private static readonly Dictionary<string, IEnumerable<ReferenceLocation>> LocationCache = new Dictionary<string, IEnumerable<ReferenceLocation>>();

        public static IEnumerable<T> DescendantNodes<T>(this SyntaxNode node) {
            return node.DescendantNodes().OfType<T>();
        }

        public static T FirstDescendantNode<T>(this SyntaxNode node, Predicate<T> filter)
        {
            return DescendantNodes<T>(node).FirstOrDefault(x=>filter(x));
        }

        public static IEnumerable<T> DescendantNodesAndTokens<T>(this SyntaxNode node, Predicate<T> filter) {
            return node.DescendantNodesAndTokens().OfType<T>().Where(x => filter(x));
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

        public static MethodDeclarationSyntax GetMethodDescendant(this SyntaxNode node, string methodName)
        {
            return node.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(x=>x.Identifier.Text==methodName);
        }

        public static MethodDeclarationSyntax GetContainingMethod(this SyntaxNode node, Solution solution = null)
        {
            MethodDeclarationSyntax callingMethod = node
                .GetLocation()
                .GetContainingMethod();

            return callingMethod;
        }

        /// <summary>
        /// For a member access expression such as "person.Address.Street" returns "person" as root identifier.
        /// </summary>
        public static IdentifierNameSyntax GetRootIdentifier(this ExpressionSyntax expression) {
            if(!(expression is MemberAccessExpressionSyntax) && !(expression is IdentifierNameSyntax))
                throw new ArgumentException($"Expression {expression} must be MemberAccessExpression or IdentifierNameSyntax");

            if (expression is IdentifierNameSyntax)
                return expression.As<IdentifierNameSyntax>();

            string rootToken = expression.ToString().Split('.').First();
            var identifier = expression.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == rootToken).First();

            return identifier;
        }

        public static ClassDeclarationSyntax GetContainingClass(this SyntaxNode node) {
            ClassDeclarationSyntax classDeclaration = node.GetLocation().GetContainingClass();

            return classDeclaration;
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

        public static ClassDeclarationSyntax GetContainingClass(this Location location) {
            ClassDeclarationSyntax classDeclaration = location
                .SourceTree
                .GetRoot()
                .FindToken(location.SourceSpan.Start)
                .Parent
                .AncestorsAndSelf()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            return classDeclaration;
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
            var compilation =  solution.Projects.First(x => x.Documents.Any(doc => doc.FilePath == node.SyntaxTree.FilePath)).GetCompilation();
            var diagnostics = compilation.GetDiagnostics();
            return compilation;
        }

        public static IEnumerable<ReferenceLocation> FindReferenceLocations(this Solution solution, SyntaxNode node) {
            var methodSymbol = node.GetSemanticModel(GetCompilation(solution, node)).GetDeclaredSymbol(node);
            return FindReferenceLocations(solution, methodSymbol);
        }

        public static IEnumerable<ReferenceLocation> FindReferenceLocations(this Solution solution, ISymbol symbol)
        {
            var key = symbol.ToString();

            if (LocationCache.ContainsKey(key))
                return LocationCache[key];

            var locations = SymbolFinder.FindReferencesAsync(symbol, solution).Result;
            LocationCache[key] = locations.SelectMany(x => x.Locations);

            return LocationCache[key];
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

        public static SemanticModel GetSemanticModel(this SyntaxNode node, Solution solution)
        {
            var filePath = node.SyntaxTree.FilePath;
            var project = solution.Projects.FirstOrDefault(x => x.Documents.Any(d => d.FilePath == filePath));
            var compilation = project.GetCompilation();
            var model = GetSemanticModel(node, compilation);

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
            if (CompilationCache.ContainsKey(project))
                return CompilationCache[project];

            var compilation = project.GetCompilationAsync().Result;

            foreach (var assembly in Assembly.Load(project.AssemblyName).GetReferencedAssemblies().Select(Assembly.Load))
            {
                //var assemblyName = assembly.GetName().Name;

                //if (compilation.ExternalReferences.Any(x=>x.Display == assemblyName))
                    //continue;

                var reference = MetadataReference.CreateFromFile(assembly.Location);
                compilation = compilation.AddReferences(reference);
            }

            CompilationCache[project] = compilation;
            var diag = compilation.GetDiagnostics();

            return compilation;
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

        public static bool IsAlgebraic(this ExpressionSyntax expressionSyntax)
        {
            switch (expressionSyntax.Kind())
            {
                case SyntaxKind.AddExpression:
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.SubtractExpression:
                case SyntaxKind.DivideExpression:
                    return true;
            }

            return false;
        }
    }
}