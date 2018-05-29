using System.Linq;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.ExpressionMatcher;
using Prometheus.Engine.Reachability.Tracker;
using Prometheus.Engine.Thread;
using Prometheus.Engine.Types;
using TestProject.Services;

namespace Prometheus.Engine.UnitTests
{
    [TestFixture]
    public class QueryMatcherTests {
        private IQueryMatcher queryMatcher;
        private ReferenceTracker referenceTracker;
        private Solution solution;

        [SetUp]
        public void Init() {
            var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            solution = workspace.OpenSolutionAsync(@"C:\Users\tamas\Documents\Github\Prometheus\Prometheus\Prometheus.sln").Result;
            var typeService = new TypeService(solution);
            IReferenceParser referenceParser = new ReferenceParser();
            ThreadSchedule threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            referenceTracker = new ReferenceTracker(solution, threadSchedule, typeService, referenceParser);
            queryMatcher = new Z3QueryMatcher(typeService);
        }

        [TearDown]
        public void TearDown() {
            queryMatcher = null;
        }

        [Test]
        public void QueryMatcher_ForIndexQueryAssignments_MatchesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var firstIdentifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_WithIndexQuery_1)).DescendantTokens<SyntaxToken>(x => x.ToString() == "indexCustomer1").First();
            var secondIdentifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_WithIndexQuery_1)).DescendantTokens<SyntaxToken>(x => x.ToString() == "indexCustomer2").First();
            var firstAssignment = referenceTracker.GetAssignments(firstIdentifier).First();
            var secondAssignment = referenceTracker.GetAssignments(secondIdentifier).First();

            queryMatcher.AreEquivalent(firstAssignment.RightReference.ReferenceContexts.First().Query,
                                       secondAssignment.RightReference.ReferenceContexts.First().Query,
                                       out var satisfiableTable);

            Assert.True(satisfiableTable.Count == 2);
            Assert.True(satisfiableTable.Any(x=>x.Key.ToString()== "from1" && x.Value.ToString()== "from2"));
            Assert.True(satisfiableTable.Any(x=>x.Key.ToString()== "to1" && x.Value.ToString()== "to2"));
        }

        [Test]
        public void QueryMatcher_ForFirstQueryAssignments_MatchesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var firstIdentifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_WithFirstQuery_1)).DescendantTokens<SyntaxToken>(x => x.ToString() == "firstCustomer1").First();
            var secondIdentifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_WithFirstQuery_2)).DescendantTokens<SyntaxToken>(x => x.ToString() == "firstCustomer2").First();
            var firstAssignment = referenceTracker.GetAssignments(firstIdentifier).First();
            var secondAssignment = referenceTracker.GetAssignments(secondIdentifier).First();

            queryMatcher.AreEquivalent(firstAssignment.RightReference.ReferenceContexts.First().Query,
                secondAssignment.RightReference.ReferenceContexts.First().Query,
                out var satisfiableTable);

            Assert.True(satisfiableTable.Count == 2);
            Assert.True(satisfiableTable.Any(x => x.Key.ToString() == "from1" && x.Value.ToString() == "from2"));
            Assert.True(satisfiableTable.Any(x => x.Key.ToString() == "to1" && x.Value.ToString() == "to2"));
        }

        [Test]
        public void QueryMatcher_ForWhereQueryAssignments_MatchesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var firstIdentifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_WithFirstQuery_1)).DescendantTokens<SyntaxToken>(x => x.ToString() == "whereCustomers1").First();
            var secondIdentifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_WithFirstQuery_2)).DescendantTokens<SyntaxToken>(x => x.ToString() == "whereCustomers2").First();
            var firstAssignment = referenceTracker.GetAssignments(firstIdentifier).First();
            var secondAssignment = referenceTracker.GetAssignments(secondIdentifier).First();

            queryMatcher.AreEquivalent(firstAssignment.RightReference.ReferenceContexts.First().Query,
                secondAssignment.RightReference.ReferenceContexts.First().Query,
                out var satisfiableTable);

            Assert.True(satisfiableTable.Count == 2);
            Assert.True(satisfiableTable.Any(x => x.Key.ToString() == "from1" && x.Value.ToString() == "from2"));
            Assert.True(satisfiableTable.Any(x => x.Key.ToString() == "to1" && x.Value.ToString() == "to2"));
        }
    }
}