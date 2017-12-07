using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
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
            var solution = workspace.OpenSolutionAsync(@"C:\Users\tamas\Documents\Github\Prometheus\Prometheus\Prometheus.sln").Result;
            ThreadSchedule threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(workspace.CurrentSolution.Projects.First(x => x.Name == "TestProject.GUI"));
            atomicAnalyzer = new AtomicAnalyzer(workspace, threadSchedule);
        }

        [TearDown]
        public void TearDown()
        {
            atomicAnalyzer = null;
        }

        [Test]
        public void AtomicAnalyzer_WithAtomicityAnalyzer()
        {
            var atomicInvariant = AtomicInvariant
                .Empty
                .WithExpression<AtomicQueue<object>>(x => x.IsModifiedAtomic("list"));
            atomicAnalyzer.Analyze(atomicInvariant);
        }
    }
}