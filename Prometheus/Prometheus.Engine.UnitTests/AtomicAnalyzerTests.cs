using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Build.Tasks;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.Analyzer;
using Prometheus.Engine.Analyzer.Atomic;
using Prometheus.Engine.Model;
using Prometheus.Engine.Thread;
using Prometheus.Engine.Verifier;
using TestProject.Common;

namespace Prometheus.Engine.UnitTests
{
    [TestFixture]
    public class AtomicAnalyzerTests
    {
        private AtomicAnalyzer atomicAnalyzer;

        [SetUp]
        public void Init()
        {
            var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            var solution = workspace.OpenSolutionAsync(@"C:\Users\tamas\Documents\Github\Prometheus\Prometheus\Prometheus.sln").Result;
            ThreadSchedule threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            atomicAnalyzer = new AtomicAnalyzer
            {
                Solution = solution,
                ThreadSchedule = threadSchedule
            };
        }

        [TearDown]
        public void TearDown()
        {
            atomicAnalyzer = null;
        }

        [Test]
        public void AtomicAnalyzer_ForAtomicQueue_AnalyzesCorrectly()
        {
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
            Assert.True(analysis.FirstDeadlockLock == null);
        }

        [Test]
        public void AtomicAnalyzer_ForDeadlockedQueue_AnalyzesCorrectly() {
            var modelStateConfig = ModelStateConfiguration
                .Empty
                .ChangesState<LinkedList<object>>(x => x.RemoveLast())
                .ChangesState<LinkedList<object>>(x => x.AddFirst(Args.Any<object>()));
            var atomicInvariant = AtomicInvariant
                .Empty
                .WithExpression<DeadlockedQueue<object>>(x => x.IsModifiedAtomic("list"));
            atomicAnalyzer.ModelStateConfiguration = modelStateConfig;
            var analysis = atomicAnalyzer.Analyze(atomicInvariant).As<AtomicAnalysis>();

            Assert.True(analysis.UnmatchedLock == null);
            Assert.True(analysis.FirstDeadlockLock != null);
            Assert.True(analysis.SecondDeadlockLock != null);
        }

        [Test]
        public void AtomicAnalyzer_ForNonAtomicQueue_AnalyzesCorrectly() {
            var modelStateConfig = ModelStateConfiguration
                .Empty
                .ChangesState<LinkedList<object>>(x => x.RemoveLast())
                .ChangesState<LinkedList<object>>(x => x.AddFirst(Args.Any<object>()));
            var atomicInvariant = AtomicInvariant
                .Empty
                .WithExpression<NonAtomicQueue<object>>(x => x.IsModifiedAtomic("list"));
            atomicAnalyzer.ModelStateConfiguration = modelStateConfig;
            var analysis = atomicAnalyzer.Analyze(atomicInvariant).As<AtomicAnalysis>();

            Assert.True(analysis.UnmatchedLock != null);
            Assert.True(analysis.FirstDeadlockLock == null);
            Assert.True(analysis.SecondDeadlockLock == null);
        }

        [Test]
        public void AtomicAnalyzer_ForAtomicStack_WithPublicMember_AnalyzesCorrectly() {
            var modelStateConfig = ModelStateConfiguration
                .Empty
                .ChangesState<LinkedList<object>>(x => x.RemoveLast())
                .ChangesState<LinkedList<object>>(x => x.AddLast(Args.Any<object>()));
            var atomicInvariant = AtomicInvariant
                .Empty
                .WithExpression<AtomicStack<object>>(x => x.List.IsModifiedAtomic());
            atomicAnalyzer.ModelStateConfiguration = modelStateConfig;
            var analysis = atomicAnalyzer.Analyze(atomicInvariant).As<AtomicAnalysis>();

            Assert.True(analysis.UnmatchedLock == null);
            Assert.True(analysis.FirstDeadlockLock == null);
            Assert.True(analysis.SecondDeadlockLock == null);
        }

        [Test]
        public void AtomicAnalyzer_ForNonAtomicUsedStack_WithPublicMember_AnalyzesCorrectly() {
            var modelStateConfig = ModelStateConfiguration
                .Empty
                .ChangesState<LinkedList<object>>(x => x.RemoveLast())
                .ChangesState<LinkedList<object>>(x => x.AddLast(Args.Any<object>()));
            var atomicInvariant = AtomicInvariant
                .Empty
                .WithExpression<NonAtomicUsedStack<object>>(x => x.List.IsModifiedAtomic());
            atomicAnalyzer.ModelStateConfiguration = modelStateConfig;
            var analysis = atomicAnalyzer.Analyze(atomicInvariant).As<AtomicAnalysis>();

            Assert.True(analysis.UnmatchedLock != null);
            Assert.True(analysis.FirstDeadlockLock == null);
            Assert.True(analysis.SecondDeadlockLock == null);
        }
    }
}