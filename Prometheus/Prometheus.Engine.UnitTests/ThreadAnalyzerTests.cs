using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis.MSBuild;
using NUnit.Framework;
using Prometheus.Engine.Thread;
using Prometheus.Extensions;
using TestProject.Common;

namespace Prometheus.Engine.UnitTests {
    [TestFixture]
    public class ThreadAnalyzerTests
    {
        private ThreadAnalyzer threadAnalyzer;
        private MSBuildWorkspace workspace;

        [SetUp]
        public void Init()
        {
            workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            var solution = workspace.OpenSolutionAsync(@"C:\Work\Projects\Prometheus\Prometheus\Prometheus.sln").Result;
            threadAnalyzer = new ThreadAnalyzer();
        }

        [TearDown]
        public void TearDown()
        {
            threadAnalyzer = null;
        }

        [Test]
        public void AtomicAnalyzer_WithAtomicityAnalyzer() {
            var result = threadAnalyzer.GetThreadHierarchy(workspace.CurrentSolution.Projects.First(x=>x.Name=="TestProject.GUI"));
        }
    }
}