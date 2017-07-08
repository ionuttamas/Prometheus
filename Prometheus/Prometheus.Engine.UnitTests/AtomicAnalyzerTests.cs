using System;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
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
            var solution = workspace.OpenSolutionAsync(@"C:\Users\Tamas Ionut\Documents\Prometheus\Prometheus\Prometheus.sln").Result;
            atomicAnalyzer = new AtomicAnalyzer(workspace);
        }

        [TearDown]
        public void TearDown()
        {
            atomicAnalyzer = null;
        }

        [Test]
        public void AtomicAnalyzer_WithAtomicityAnalyzer() {
            atomicAnalyzer.Analyze((Expression<Func<AtomicQueue<object>, bool>>)(x=>x.IsModifiedAtomic("list")));
        }
    }
}