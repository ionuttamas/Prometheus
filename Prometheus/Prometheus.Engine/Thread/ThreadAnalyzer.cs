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

            foreach (var project in solution.Projects)
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
                        .Select(v => new
                        {
                            Variable = v.Variables[0].Identifier.Text,
                            ThreadMethodName = ((IdentifierNameSyntax)((ObjectCreationExpressionSyntax)v.Variables[0].Initializer.Value).ArgumentList.Arguments[0].Expression).Identifier.Text
                        })})
                .Where(x => x.Variables.Any())
                .ToList();
            var threadInvocations = threadVariables
                .SelectMany(x => x.Tree.GetRoot().DescendantNodes<InvocationExpressionSyntax>())
                .Where(x => x.Expression is MemberAccessExpressionSyntax &&
                            x.Expression.As<MemberAccessExpressionSyntax>().Name.Identifier.Text == "Start" &&
                            threadVariables
                                .First(tv => tv.Tree == x.SyntaxTree)
                                .Variables
                                .Select(v=>v.Variable)
                                .Contains(x.Expression.As<MemberAccessExpressionSyntax>().Expression.As<IdentifierNameSyntax>().Identifier.Text))
                .ToDictionary(x =>  x,
                              x => x.SyntaxTree
                                    .GetRoot()
                                    .GetMethod(threadVariables
                                                .First(tv => tv.Tree == x.SyntaxTree)
                                                .Variables
                                                .First(v => v.Variable== x.Expression.As<MemberAccessExpressionSyntax>().Expression.As<IdentifierNameSyntax>().Identifier.Text)
                                                .ThreadMethodName));
            List<ThreadPath> threadPaths = threadInvocations
                .SelectMany(x => GetPaths(project, entryPoint, x.Key, x.Value))
                .ToList();

            return threadPaths;
        }

        private List<ThreadPath> GetPaths(Project project, IMethodSymbol entryPoint, InvocationExpressionSyntax threadStart, MethodDeclarationSyntax threadMethod)
        {
            Compilation compilation = project.GetCompilationAsync(CancellationToken.None).Result;
            // Get the method that calls the thread start and track it to a executable project entry point
            MethodDeclarationSyntax methodDeclaration = threadStart.AncestorNodes<MethodDeclarationSyntax>().First();
            ISymbol methodSymbol = methodDeclaration.GetSemanticModel(compilation).GetDeclaredSymbol(methodDeclaration);
            List<List<Location>> callChains = GetSymbolChains(entryPoint, methodSymbol);
            List<ThreadPath> threadPaths = callChains
                .Select(x => new ThreadPath { Invocations = x, ThreadMethod = threadMethod })
                .ToList();

            return threadPaths;
        }

        private List<List<Location>> GetSymbolChains(IMethodSymbol entryPoint, ISymbol referencedSymbol)
        {
            Location location = referencedSymbol.Locations.First();
            Project project = GetProject(referencedSymbol);

            if(entryPoint.Name==referencedSymbol.Name &&
               entryPoint.ContainingType.ToString()==referencedSymbol.ContainingType.ToString())
            {
                var chain = new List<Location> {location};
                return new List<List<Location>> {chain};
            }

            var result = new List<List<Location>>();
            MethodDeclarationSyntax callingMethod = location.GetContainingMethod();
            Document document = project.Documents.First(x => x.FilePath == callingMethod.SyntaxTree.FilePath);
            IMethodSymbol methodSymbol = (IMethodSymbol)ModelExtensions.GetDeclaredSymbol(document.GetSemanticModelAsync().Result, callingMethod);
            IEnumerable<ReferencedSymbol> references = SymbolFinder.FindReferencesAsync(methodSymbol, solution).Result;

            foreach (var referenceLocation in references.SelectMany(x=>x.Locations))
            {
                SyntaxNode referencingRoot = referenceLocation
                    .Document
                    .GetSyntaxRootAsync()
                    .Result;
                MethodDeclarationSyntax callingMethodDeclaration = referencingRoot
                    .DescendantNodes<InvocationExpressionSyntax>()
                    .First(x=>x.GetLocation().SourceSpan.Contains(referenceLocation.Location.SourceSpan))
                    .AncestorNodes<MethodDeclarationSyntax>()
                    .First();
                IMethodSymbol callingReferenceSymbol = (IMethodSymbol)ModelExtensions.GetDeclaredSymbol(referenceLocation.Document.GetSemanticModelAsync().Result, callingMethodDeclaration);

                if(callingReferenceSymbol.Equals(referencedSymbol))
                    continue;

                List<List<Location>> chains = GetSymbolChains(entryPoint, callingReferenceSymbol);

                foreach (var chain in chains)
                {
                    chain.Add(referenceLocation.Location);
                    chain.Add(location);
                    result.Add(chain);
                }
            }

            return result;
        }

        private Project GetProject(ISymbol symbol)
        {
            Project project = solution.Projects.First(x => x.AssemblyName == symbol.ContainingAssembly.Name);

            return project;
        }
    }
}