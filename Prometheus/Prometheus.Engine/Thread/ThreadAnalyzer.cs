using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;

namespace Prometheus.Engine.Thread
{
    public class ThreadAnalyzer : IThreadAnalyzer
    {
        public ThreadHierarchy GetThreadHierarchy(Project project)
        {
            // Currently, we support only ConsoleApplication projects
            Compilation compilation = project.GetCompilationAsync(CancellationToken.None).Result;
            IMethodSymbol entryMethod = compilation.GetEntryPoint(CancellationToken.None);
            //compilation.GetSemanticModel(entryMethod.)

            var declarations = compilation
                .SyntaxTrees
                .SelectMany(x => x.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>())
                .Select(x=> compilation.GetSemanticModel(x.SyntaxTree).GetSymbolInfo(x.Type).Symbol)
                .Select(x=>x)
                .ToList();

            /*SemanticModel semanticModel = compilation.GetSemanticModel(dec.SyntaxTree);

            var symbolInfo = semanticModel.GetSymbolInfo(dec.Type);
            var typeSymbol = symbolInfo.Symbol; // the type symbol for the variable..
*/
            return null;
        }

        //private ThreadHierarchy
    }
}