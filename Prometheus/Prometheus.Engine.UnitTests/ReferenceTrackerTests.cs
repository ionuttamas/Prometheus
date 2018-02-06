using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.ReferenceTrack;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine.UnitTests
{
    [TestFixture]
    public class ReferenceTrackerTests
    {
        private ReferenceTracker referenceTracker;
        private Solution solution;

        [SetUp]
        public void Init()
        {
            var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            solution = workspace.OpenSolutionAsync(@"C:\Users\tamas\Documents\Github\Prometheus\Prometheus\Prometheus.sln").Result;
            ThreadSchedule threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            referenceTracker = new ReferenceTracker(solution, threadSchedule);
        }

        [TearDown]
        public void TearDown()
        {
            referenceTracker = null;
        }

        [Test]
        public void ReferenceTracker_ForNonConditionalAssignments_TracksCorrectly()
        {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var registrationServiceClass = project.GetCompilation().GetClassDeclaration(typeof (TestProject.Services.RegistrationService));
            var identifier = registrationServiceClass.GetMethodDescendant(nameof(TestProject.Services.RegistrationService.Register)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.True(assignments.Any());
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var registrationServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.RegistrationService));
            var identifier = registrationServiceClass.GetMethodDescendant(nameof(TestProject.Services.RegistrationService.SimpleIfRegister)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.True(assignments.Any());
            Assert.True(assignments.First().Conditions.Any(x=>x.Expression=="customer.Type == CustomerType.Gold"));
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfSingleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TestProject.Services.TransferService.SimpleIfSingleElseTransfer)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "from").Last();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(2, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.Expression == "from.Type == CustomerType.Premium"));
            Assert.True(assignments[1].Conditions.Any(x => x.Expression == "!from.Type == CustomerType.Premium"));
        }

        [Test]
        public void ReferenceTracker_ForTwoSided_SimpleIfMultipleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TestProject.Services.TransferService.SimpleIfMultipleElseTransfer)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "from").ToList()[3];
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(3, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.Expression == "from.Type == CustomerType.Premium"));

            Assert.True(assignments[1].Conditions.Any(x => x.Expression == "!from.Type == CustomerType.Premium"));
            Assert.True(assignments[1].Conditions.Any(x => x.Expression == "from.Type == CustomerType.Gold"));

            Assert.True(assignments[2].Conditions.Any(x => x.Expression == "!from.Type == CustomerType.Premium"));
            Assert.True(assignments[2].Conditions.Any(x => x.Expression == "!from.Type == CustomerType.Gold"));
        }
    }
}