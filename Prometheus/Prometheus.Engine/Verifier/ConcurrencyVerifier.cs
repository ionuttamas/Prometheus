using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Prometheus.Engine.Analyzer;
using Prometheus.Engine.Atomic;
using Prometheus.Engine.Models;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine.Verifier
{
    public class ConcurrencyVerifier
    {
        private readonly Dictionary<Type, IAnalyzer> analyzers;
        private ThreadAnalyzer threadAnalyzer;
        private Solution solution;
        private ThreadSchedule threadSchedule;

        private ConcurrencyVerifier()
        {
            analyzers = new Dictionary<Type, IAnalyzer>
            {
                {typeof(AtomicInvariant), new AtomicAnalyzer()}
            };
        }

        public ConcurrencyVerifier WithSolution(string solutionPath)
        {
            var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            solution = workspace.OpenSolutionAsync(solutionPath).Result;
            threadAnalyzer = new ThreadAnalyzer(solution);

            foreach (var analyzer in analyzers.Values) {
                analyzer.Solution = solution;
            }

            return this;
        }

        public ConcurrencyVerifier WithEntryProject(string projectName)
        {
            threadSchedule = threadAnalyzer.GetThreadSchedule(solution.Projects.First(x => x.Name == projectName));

            foreach (var analyzer in analyzers.Values)
            {
                analyzer.ThreadSchedule = threadSchedule;
            }

            return this;
        }

        public ConcurrencyVerifier WithModelConfiguration(ModelStateConfiguration modelConfig) {
            foreach (var analyzer in analyzers.Values) {
                analyzer.ModelStateConfiguration = modelConfig;
            }

            return this;
        }

        public ThreadSchedule GetThreadSchedule()
        {
            return threadSchedule;
        }

        public IAnalysis Analyze(IInvariant invariant)
        {
            return analyzers[invariant.GetType()].Analyze(invariant);
        }
    }
}