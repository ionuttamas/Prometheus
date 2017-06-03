using System.Linq;
using Microsoft.CodeAnalysis.MSBuild;
using NUnit.Framework;
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
            var solution = workspace.OpenSolutionAsync(@"C:\Users\Tamas Ionut\Documents\Prometheus\Prometheus\Prometheus.sln").Result;
            threadAnalyzer = new ThreadAnalyzer(solution);
        }

        [TearDown]
        public void TearDown()
        {
            threadAnalyzer = null;
        }

        [Test]
        public void ThreadAnalyzer_WithAtomicityAnalyzer() {
            //startProjects = solution.Projects.Where(x => x.CompilationOptions.OutputKind == OutputKind.ConsoleApplication).ToList();
            var result = threadAnalyzer.GetThreadSchedule(workspace.CurrentSolution.Projects.First(x=>x.Name=="TestProject.GUI"));
        }
    }
}