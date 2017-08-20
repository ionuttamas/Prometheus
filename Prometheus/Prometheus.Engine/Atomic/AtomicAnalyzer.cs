using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Prometheus.Common;
using Prometheus.Engine.Analyzer;
using Prometheus.Engine.Invariant;

namespace Prometheus.Engine
{
    /// <summary>
    /// This checker verifies if a variable is updated atomically within a code base.
    /// </summary>
    public class AtomicAnalyzer : IAnalyzer
    {
        private readonly string markingMethod;
        private readonly Workspace workspace;

        public AtomicAnalyzer(Workspace workspace)
        {
            this.workspace = workspace;
            markingMethod = nameof(Extensions.ModelExtensions.IsModifiedAtomic);
        }

        public IAnalysis Analyze(IInvariant invariant)
        {
            AnalyzePrivateMember();

            return new AtomicAnalysis();
        }

        private void AnalyzePrivateMember(MemberInfo member)
        {
            Type type = member.DeclaringType;
            string assemblyName = type.Assembly.GetName().Name;
            Solution solution = workspace.CurrentSolution;
            Project project = solution.Projects.First(x => x.AssemblyName == assemblyName);
            Compilation compilation = project.GetCompilation();
            ClassDeclarationSyntax classDeclaration = compilation.GetClassDeclaration(type.FullName);
            MemberDeclarationSyntax memberDeclaration = classDeclaration.GetMemberDeclaration(member.Name);
            SemanticModel semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            ISymbol memberSymbol = semanticModel.GetSymbolInfo(memberDeclaration).Symbol;
            IEnumerable<ReferencedSymbol> references = SymbolFinder.FindReferencesAsync(memberSymbol, solution).Result;

        }
    }
}