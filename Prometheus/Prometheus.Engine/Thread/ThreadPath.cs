using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Prometheus.Common;

namespace Prometheus.Engine.Thread {
    public class ThreadPath {
        public MethodDeclarationSyntax ThreadMethod { get; set; }
        public List<Location> Invocations { get; set; }

        public List<List<Location>> GetInvocationChains(Solution solution, Location location)
        {
            return GetInvocationChains(solution, location, new List<MethodDeclarationSyntax>());
        }

        private List<List<Location>> GetInvocationChains(Solution solution, Location location, List<MethodDeclarationSyntax> visitedMethods) {
            Project project = solution.Projects.First(x => x.Documents.Any(doc => doc.FilePath == location.SourceTree.FilePath));

            if (ThreadMethod.GetLocation().SourceSpan.Contains(location.SourceSpan)) {
                var chain = new List<Location> { location };
                return new List<List<Location>> { chain };
            }

            var result = new List<List<Location>>();
            MethodDeclarationSyntax callingMethod = location.GetContainingMethod();

            if (callingMethod == null || visitedMethods.Any(x=>x.IsEquivalentTo(callingMethod))) {
                return new List<List<Location>>();
            }

            visitedMethods.Add(callingMethod);
            Document document = project.Documents.First(x => x.FilePath == callingMethod.SyntaxTree.FilePath);
            IMethodSymbol methodSymbol = (IMethodSymbol)document.GetSemanticModelAsync().Result.GetDeclaredSymbol(callingMethod);

            foreach (var referenceLocation in solution.FindReferences(methodSymbol)) {
                SyntaxNode referencingRoot = referenceLocation
                    .Document
                    .GetSyntaxRootAsync()
                    .Result;
                MethodDeclarationSyntax callingMethodDeclaration = referencingRoot
                    .DescendantNodes<InvocationExpressionSyntax>()
                    .First(x => x.GetLocation().SourceSpan.Contains(referenceLocation.Location.SourceSpan))
                    .AncestorNodes<MethodDeclarationSyntax>()
                    .First();

                if (callingMethodDeclaration.GetLocation().SourceSpan.Contains(referenceLocation.Location.SourceSpan))
                {
                    result.Add(new List<Location> { referenceLocation.Location });
                    continue;
                }

                List<List<Location>> chains = GetInvocationChains(solution, referenceLocation.Location, visitedMethods);

                foreach (var chain in chains) {
                    chain.Add(referenceLocation.Location);
                    chain.Add(location);
                    result.Add(chain);
                }
            }

            return result;
        }
    }
}
