using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.Analyzer;
using Prometheus.Engine.Analyzer.Atomic;
using Prometheus.Engine.Model;
using Prometheus.Engine.ReferenceTrack;
using Prometheus.Engine.Thread;
using Prometheus.Engine.Verifier;
using TestProject.Common;

namespace Prometheus.Engine.UnitTests
{
    [TestFixture]
    public class ReferenceTrackerTests
    {
        private ReferenceTracker referenceTracker;

        [SetUp]
        public void Init()
        {
            var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            var solution = workspace.OpenSolutionAsync(@"C:\Users\tamas\Documents\Github\Prometheus\Prometheus\Prometheus.sln").Result;
            ThreadSchedule threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            referenceTracker = new ReferenceTracker(solution, threadSchedule);
        }

        [TearDown]
        public void TearDown()
        {
            referenceTracker = null;
        }

        [Test]
        public void AtomicAnalyzer_ForAtomicQueue_AnalyzesCorrectly()
        {/*
            var modelStateConfig = ModelStateConfiguration
                .Empty
                .ChangesState<LinkedList<object>>(x => x.RemoveLast())
                .ChangesState<LinkedList<object>>(x => x.AddFirst(Args.Any<object>()));
            var atomicInvariant = AtomicInvariant
                .Empty
                .WithExpression<AtomicQueue<object>>(x => x.IsModifiedAtomic("list"));
            atomicAnalyzer.ModelStateConfiguration = modelStateConfig;
            var analysis = atomicAnalyzer.Analyze(atomicInvariant).As<AtomicAnalysis>();

            Assert.True(analysis.UnmatchedLock==null);
            Assert.True(analysis.FirstDeadlockLock==null);
            Assert.True(analysis.FirstDeadlockLock == null);*/
        }
    }
}