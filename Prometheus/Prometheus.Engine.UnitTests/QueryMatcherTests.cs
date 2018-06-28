using System.Linq;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.ExpressionMatcher;
using Prometheus.Engine.Reachability.Tracker;
using Prometheus.Engine.Thread;
using Prometheus.Engine.Types;
using Prometheus.Engine.Types.Polymorphy;
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
            IPolymorphicResolver polymorphicService = new PolymorphicResolver();
            var typeService = new TypeService(solution, polymorphicService, "TestProject.GUI", "TestProject.Services", "TestProject.Common");
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
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TransferService2));
            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TransferService1.MethodAssignment_WithIndexQuery_1)).DescendantTokens<SyntaxToken>(x => x.ToString() == "indexCustomer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TransferService2.MethodAssignment_WithIndexQuery_2)).DescendantTokens<SyntaxToken>(x => x.ToString() == "indexCustomer2").First();
            var firstAssignment = referenceTracker.GetAssignments(firstIdentifier).First();
            var secondAssignment = referenceTracker.GetAssignments(secondIdentifier).First();

            var areEquivalent = queryMatcher.AreEquivalent(firstAssignment.RightReference.ReferenceContexts.First().Query,
                                       secondAssignment.RightReference.ReferenceContexts.First().Query,
                                       out var satisfiableTable);

            Assert.True(areEquivalent);
            Assert.True(satisfiableTable.Count == 2);
            Assert.True(satisfiableTable.Any(x=>x.Key.ToString()== "from2" && x.Value.ToString()== "from1"));
            Assert.True(satisfiableTable.Any(x=>x.Key.ToString()== "to2" && x.Value.ToString()== "to1"));
        }

        [Test]
        public void QueryMatcher_ForFirstQueryAssignments_MatchesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TransferService2));
            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TransferService1.MethodAssignment_WithFirstQuery_1)).DescendantTokens<SyntaxToken>(x => x.ToString() == "firstCustomer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TransferService2.MethodAssignment_WithFirstQuery_2)).DescendantTokens<SyntaxToken>(x => x.ToString() == "firstCustomer2").First();
            var firstAssignment = referenceTracker.GetAssignments(firstIdentifier).First();
            var secondAssignment = referenceTracker.GetAssignments(secondIdentifier).First();

            var areEquivalent = queryMatcher.AreEquivalent(firstAssignment.RightReference.ReferenceContexts.First().Query,
                secondAssignment.RightReference.ReferenceContexts.First().Query,
                out var satisfiableTable);

            Assert.True(areEquivalent);
            Assert.AreEqual(2, satisfiableTable.Count);
            Assert.True(satisfiableTable.Any(x => x.Key.ToString() == "from2" && x.Value.ToString() == "from1"));
            Assert.True(satisfiableTable.Any(x => x.Key.ToString() == "to2" && x.Value.ToString() == "to1"));
        }

        [Test]
        public void QueryMatcher_ForWhereQueryAssignments_MatchesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TransferService2));
            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TransferService1.MethodAssignment_WithWhereQuery_1)).DescendantTokens<SyntaxToken>(x => x.ToString() == "whereCustomers1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TransferService2.MethodAssignment_WithWhereQuery_2)).DescendantTokens<SyntaxToken>(x => x.ToString() == "whereCustomers2").First();
            var firstAssignment = referenceTracker.GetAssignments(firstIdentifier).First();
            var secondAssignment = referenceTracker.GetAssignments(secondIdentifier).First();

            var areEquivalent = queryMatcher.AreEquivalent(firstAssignment.RightReference.ReferenceContexts.First().Query,
                secondAssignment.RightReference.ReferenceContexts.First().Query,
                out var satisfiableTable);

            Assert.True(areEquivalent);
            Assert.AreEqual(2, satisfiableTable.Count);
            Assert.True(satisfiableTable.Any(x => x.Key.ToString() == "from2" && x.Value.ToString() == "from1"));
            Assert.True(satisfiableTable.Any(x => x.Key.ToString() == "to2" && x.Value.ToString() == "to1"));
        }
    }
}