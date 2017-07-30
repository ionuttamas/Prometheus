using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine.UnitTests {
    [TestFixture]
    public class ThreadAnalyzerTests
    {
        private ThreadAnalyzer threadAnalyzer;
        private MSBuildWorkspace workspace;

        [SetUp]
        public void Init()
        {
            workspace = MSBuildWorkspace.Create();
            var solution = workspace.OpenSolutionAsync(@"C:\Users\tamas\Documents\Prometheus\Prometheus\Prometheus.sln").Result;
            //var sol2 = GetSolution(workspace.CurrentSolution);

            threadAnalyzer = new ThreadAnalyzer(solution);
        }

        [TearDown]
        public void TearDown()
        {
            threadAnalyzer = null;
        }

        [Test]
        public void ThreadAnalyzer_WithAtomicityAnalyzer() {
            var result = threadAnalyzer.GetThreadSchedule(workspace.CurrentSolution.Projects.First(x=>x.Name== "TestProject.GUI"));
        }
    }
}