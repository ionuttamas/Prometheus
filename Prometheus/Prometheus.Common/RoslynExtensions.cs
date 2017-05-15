using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace Prometheus.Common
{
    public static class RoslynExtensions
    {
        public static ClassDeclarationSyntax GetClassDeclaration(this Compilation compilation, string fullClassName)
        {
            ClassDeclarationSyntax classDeclaration = compilation
                .SyntaxTrees
                .SelectMany(x => x.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .First(x => x.GetFullName() == fullClassName);

            return classDeclaration;
        }

        public static MemberDeclarationSyntax GetMemberDeclaration(this ClassDeclarationSyntax classDeclaration, string field)
        {
            return classDeclaration.DescendantNodes().OfType<MemberDeclarationSyntax>().FirstOrDefault();
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