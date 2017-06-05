using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Prometheus.Common;

namespace Prometheus.Engine.Thread
{
    public class ThreadAnalyzer : IThreadAnalyzer
    {
        private readonly Solution solution;

        public ThreadAnalyzer(Solution solution)
        {
            this.solution = solution;
        }

        public ThreadSchedule GetThreadSchedule(Project entryProject)
        {
            var threadSchedule = new ThreadSchedule
            {
                Paths = new List<ThreadPath>()
            };
            Compilation compilation = entryProject.GetCompilationAsync(CancellationToken.None).Result;
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof (System.Threading.Thread).Assembly.Location));

            foreach (var project in solution.Projects.Where(x => x.CompilationOptions.OutputKind == OutputKind.DynamicallyLinkedLibrary))
            {
                var threadPaths = AnalyzeProject(project, compilation.GetEntryPoint(CancellationToken.None));
                threadSchedule.Paths.AddRange(threadPaths);
            }

            return threadSchedule;
        }

        /// <summary>
        /// Analyzes the project and tracks the thread paths to the entry point of the entry project.
        /// </summary>
        private List<ThreadPath> AnalyzeProject(Project project, IMethodSymbol entryPoint)
        {
            Compilation compilation = project.GetCompilationAsync(CancellationToken.None).Result;
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof (System.Threading.Thread).Assembly.Location));
            SymbolDisplayFormat typeDisplayFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
            var threadVariables = compilation
                .SyntaxTrees
                .Select(x => new
                {
                    Tree = x,
                    Variables = x
                        .GetRoot()
                        .DescendantNodes<VariableDeclarationSyntax>()
                        .Where(v =>
                                v.GetSemanticModel(compilation).GetTypeInfo(v.Type).Type.ToDisplayString(typeDisplayFormat) ==
                                typeof (System.Threading.Thread).FullName)
                        .Select(v => v.Variables[0].Identifier.Text)})
                .Where(x => x.Variables.Any())
                .ToList();
            List<InvocationExpressionSyntax> threadInvocations = threadVariables
                .SelectMany(x => x.Tree.GetRoot().DescendantNodes<InvocationExpressionSyntax>())
                .Where(x => x.Expression.As<MemberAccessExpressionSyntax>().Name.Identifier.Text == "Start" &&
                            threadVariables.First(tv => tv.Tree == x.SyntaxTree)
                                .Variables.Contains(x.Expression.As<MemberAccessExpressionSyntax>().Expression.As<IdentifierNameSyntax>().Identifier.Text))
                .ToList();
            List<ThreadPath> threadPaths = threadInvocations
                .SelectMany(x => GetPaths(compilation, entryPoint, x))
                .ToList();

            return threadPaths;
        }

        private List<ThreadPath> GetPaths(Compilation compilation, IMethodSymbol entryPoint, InvocationExpressionSyntax threadStart)
        {
            // Get the method that calls the thread start and track it to a executable project entry point
            MethodDeclarationSyntax methodDeclaration = threadStart.AncestorNodes<MethodDeclarationSyntax>().First();
            ISymbol methodSymbol = methodDeclaration.GetSemanticModel(compilation).GetDeclaredSymbol(methodDeclaration);
            List<List<Location>> callChains = GetSymbolChains(compilation, entryPoint, methodSymbol);
            List<ThreadPath> threadPaths = callChains.Select(x => new ThreadPath
            {
                Invocations = x
            }).ToList();

            return threadPaths;
        }

        private List<List<Location>> GetSymbolChains(Compilation compilation, IMethodSymbol entryPoint, ISymbol referencedSymbol)
        {
            var entryMethodLocation = entryPoint.Locations.First();
            var entryMethodSpan = entryMethodLocation
                .SourceTree
                .GetRoot()
                .FindToken(entryMethodLocation.SourceSpan.Start)
                .Parent
                .As<MethodDeclarationSyntax>()
                .Body
                .Span;

            var location = referencedSymbol.Locations.First();

            if (referencedSymbol.ContainingType == entryPoint.ContainingType &&
                Equals(referencedSymbol.ContainingAssembly, entryPoint.ContainingAssembly) &&
                entryMethodSpan.Contains(location.SourceSpan))
            {
                var chain = new List<Location> {location};
                return new List<List<Location>> {chain};
            }

            var result = new List<List<Location>>();
            MethodDeclarationSyntax callingMethod = location
                .SourceTree
                .GetRoot()
                .FindToken(location.SourceSpan.Start)
                .Parent
                .AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .First();
            ISymbol methodSymbol = callingMethod.GetSemanticModel(compilation).GetDeclaredSymbol(callingMethod);
            IEnumerable<ReferencedSymbol> references = SymbolFinder.FindReferencesAsync(methodSymbol, solution).Result;

            foreach (var reference in references)
            {
                List<List<Location>> chains = GetSymbolChains(compilation, entryPoint, reference.Definition);

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