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
        public ThreadSchedule GetThreadHierarchy(Project project)
        {
            // Currently, we support only ConsoleApplication projects
            Compilation compilation = project.GetCompilationAsync(CancellationToken.None).Result;
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof(System.Threading.Thread).Assembly.Location));
            IMethodSymbol entryMethod = compilation.GetEntryPoint(CancellationToken.None);
            SymbolDisplayFormat typeDisplayFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
            List<string> threadVariables = compilation
                .SyntaxTrees
                .SelectMany(x => x.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>())
                .Where(x => compilation.GetSemanticModel(x.SyntaxTree).GetTypeInfo(x.Type).Type.ToDisplayString(typeDisplayFormat)==typeof(System.Threading.Thread).FullName)
                .Select(x=>x.Variables[0].Identifier.Text)
                .ToList();
            List<InvocationExpressionSyntax> threadInvocations = compilation
                .SyntaxTrees
                .SelectMany(x => x.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                .Where(x => x.Expression.As<MemberAccessExpressionSyntax>().Name.Identifier.Text=="Start" &&
                            threadVariables.Contains(x.Expression.As<MemberAccessExpressionSyntax>().Expression.As<IdentifierNameSyntax>().Identifier.Text))
                .ToList();

            //var model = threadDeclarations[0].Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault().GetSemanticModel(compilation);
            //var localDeclaration = (LocalDeclarationStatementSyntax)threadDeclarations[0].Parent;
            //var res = SymbolFinder.FindReferencesAsync(model.GetSymbolInfo(localDeclaration).Symbol, project.Solution).Result;
            //model.GetSymbolInfo(threadDeclarations[0].Variables.FirstOrDefault().ArgumentList.)
            //IEnumerable<ReferencedSymbol> references = SymbolFinder.FindReferencesAsync(threadDeclarations[0].GetSemanticModel(compilation), project.Solution).Result;


            return null;
        }
    }
}