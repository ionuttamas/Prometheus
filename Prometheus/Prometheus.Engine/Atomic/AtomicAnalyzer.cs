using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Prometheus.Common;
using Prometheus.Engine.Analyzer;
using Prometheus.Engine.Invariant;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine
{
    /// <summary>
    /// This checker verifies if a variable is updated atomically within a code base.
    /// </summary>
    public class AtomicAnalyzer : IAnalyzer
    {
        private readonly Solution solution;
        private readonly ThreadSchedule threadSchedule;
        private ModelStateConfiguration configuration;

        public AtomicAnalyzer(Solution solution, ThreadSchedule threadSchedule)
        {
            this.solution = solution;
            this.threadSchedule = threadSchedule;
        }

        public void AddConfiguration(ModelStateConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public IAnalysis Analyze(IInvariant invariant)
        {
            var atomicInvariant = (AtomicInvariant) invariant;
            AnalyzePrivateMember(atomicInvariant.Member);

            return new AtomicAnalysis();
        }

        private void AnalyzePrivateMember(MemberInfo member)
        {
            Type type = member.DeclaringType;
            string assemblyName = type.Assembly.GetName().Name;
            Project project = solution.Projects.First(x => x.AssemblyName == assemblyName);
            Compilation compilation = project.GetCompilation();
            ISymbol memberSymbol = compilation.GetTypeByMetadataName($"{type.Namespace}.{type.Name}").GetMembers(member.Name).First();
            List<ReferenceLocation> locations = SymbolFinder.FindReferencesAsync(memberSymbol, solution).Result.SelectMany(x=>x.Locations).ToList();

            foreach (ReferenceLocation location in locations)
            {
                var identifierNode = compilation
                    .SyntaxTrees
                    .First(x => x.FilePath == location.Document.FilePath)
                    .GetSyntaxNode(location) as IdentifierNameSyntax;
                var memberAccessNode = identifierNode.Parent as MemberAccessExpressionSyntax;

                if (memberAccessNode?.Expression is IdentifierNameSyntax)
                {
                    var methodName = ((IdentifierNameSyntax) memberAccessNode?.Expression).Identifier.Text; //TODO: check method signature, not only its name
                    var changesState = configuration.IsStateChanging(type, methodName);
                }

                Console.WriteLine(identifierNode.GetType());
            }
        }

        private void AnalyzeStateChanges()
        {

        }

        private void AnalyzeModelWrites() {

        }
    }
}