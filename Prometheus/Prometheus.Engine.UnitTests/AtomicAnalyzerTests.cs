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
using Prometheus.Engine.Invariant;
using Prometheus.Engine.Thread;
using Prometheus.Extensions;
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
            atomicAnalyzer = new AtomicAnalyzer(solution, threadSchedule);
        }

        [TearDown]
        public void TearDown()
        {
            atomicAnalyzer = null;
        }

        [Test]
        public void AtomicAnalyzer_WithAtomicityAnalyzer()
        {
            var modelStateConfig = ModelStateConfiguration
                .Empty
                .ChangesState<LinkedList<object>>(x => x.RemoveLast())
                .ChangesState<LinkedList<object>>(x => x.AddFirst(Args.Any<object>()))
                .ChangesState<AtomicQueue<object>>(x => x.Enqueue(Args.Any<object>()))
                .ChangesState<AtomicQueue<object>>(x => x.Dequeue());
            var atomicInvariant = AtomicInvariant
                .Empty
                .WithExpression<AtomicQueue<object>>(x => x.IsModifiedAtomic("list"));
            atomicAnalyzer.AddConfiguration(modelStateConfig);
            atomicAnalyzer.Analyze(atomicInvariant);
        }
    }
}