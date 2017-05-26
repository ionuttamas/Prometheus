using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Prometheus.Common;

namespace Prometheus.Engine.Thread
{
    public class ThreadAnalyzer : IThreadAnalyzer
    {
        private readonly Solution solution;
        private readonly List<Project> startProjects;

        public ThreadAnalyzer(Solution solution)
        {
            this.solution = solution;
            // Currently, we support only ConsoleApplication projects as start projects
            startProjects = solution.Projects.Where(x => x.CompilationOptions.OutputKind == OutputKind.ConsoleApplication).ToList();
        }

        public ThreadSchedule GetThreadHierarchy(Project project)
        {
            Compilation compilation = project.GetCompilationAsync(CancellationToken.None).Result;
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof(System.Threading.Thread).Assembly.Location));
            IMethodSymbol entryMethod = compilation.GetEntryPoint(CancellationToken.None);
            SymbolDisplayFormat typeDisplayFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
            var threadVariables = compilation
                .SyntaxTrees
                .Select(x => new {
                    Tree = x,
                    Variables = x
                    .GetRoot()
                    .DescendantNodes<VariableDeclarationSyntax>()
                    .Where(v => v.GetSemanticModel(compilation).GetTypeInfo(v.Type).Type.ToDisplayString(typeDisplayFormat) == typeof(System.Threading.Thread).FullName)
                    .Select(v => v.Variables[0].Identifier.Text)})
                .Where(x => x.Variables.Any())
                .ToList();
            List<InvocationExpressionSyntax> threadInvocations = threadVariables
                .SelectMany(x => x.Tree.GetRoot().DescendantNodes<InvocationExpressionSyntax>())
                .Where(x => x.Expression.As<MemberAccessExpressionSyntax>().Name.Identifier.Text=="Start" &&
                            threadVariables.First(tv=>tv.Tree==x.SyntaxTree).Variables.Contains(x.Expression.As<MemberAccessExpressionSyntax>().Expression.As<IdentifierNameSyntax>().Identifier.Text))
                .ToList();



            //var model = threadDeclarations[0].Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault().GetSemanticModel(compilation);
            //var localDeclaration = (LocalDeclarationStatementSyntax)threadDeclarations[0].Parent;
            //var res = SymbolFinder.FindReferencesAsync(model.GetSymbolInfo(localDeclaration).Symbol, project.Solution).Result;
            //model.GetSymbolInfo(threadDeclarations[0].Variables.FirstOrDefault().ArgumentList.)
            //IEnumerable<ReferencedSymbol> references = SymbolFinder.FindReferencesAsync(threadDeclarations[0].GetSemanticModel(compilation), project.Solution).Result;


            return null;
        }

        private List<ThreadPath> GetPaths(Compilation compilation, InvocationExpressionSyntax threadStart)
        {
            // Get the method that calls the thread start and track it to a executable project entry point
            MethodDeclarationSyntax methodDeclaration = threadStart.AncestorNodes<MethodDeclarationSyntax>().First();
            ISymbol methodSymbol = methodDeclaration.GetSemanticModel(compilation).GetSymbolInfo(methodDeclaration).Symbol;
            List<List<Location>> callChains = GetSymbolChains(compilation, methodSymbol.Locations[0]);


            //return GetPaths(methodSymbol);
        }

        private List<List<Location>> GetSymbolChains(Compilation compilation, Location location)
        {
            var result = new List<List<Location>>();
            MethodDeclarationSyntax callingMethod = location.SourceTree.GetRoot().FindToken(location.SourceSpan.Start).Parent.Ancestors().OfType<MethodDeclarationSyntax>().First();
            ISymbol methodSymbol = callingMethod.GetSemanticModel(compilation).GetSymbolInfo(callingMethod).Symbol;
            IEnumerable<ReferencedSymbol> references = SymbolFinder.FindReferencesAsync(methodSymbol, solution).Result;

            foreach (var referenceLocation in references.SelectMany(x=>x.Locations).Select(x=>x.Location))
            {
                List<List<Location>> chains = GetSymbolChains(compilation, referenceLocation);

                foreach (var chain in chains)
                {
                    chain.Add(location);
                    result.Add(chain);
                }
            }

            return result;
        }
    }
}