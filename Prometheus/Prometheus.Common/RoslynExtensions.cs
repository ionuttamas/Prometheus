using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Prometheus.Common
{
    public static class RoslynExtensions
    {
        public static IEnumerable<T> DescendantNodes<T>(this SyntaxNode node) {
            return node.DescendantNodes().OfType<T>();
        }

        public static SyntaxNode GetSyntaxNode(this SyntaxTree tree, ReferenceLocation location) {
            return tree.GetRoot().FindNode(location.Location.SourceSpan);
        }

        public static MethodDeclarationSyntax GetMethod(this SyntaxNode node, string name)
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

        public static IEnumerable<T> AncestorNodes<T>(this SyntaxNode node) {
            return node.Ancestors(false).OfType<T>();
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
    }
}