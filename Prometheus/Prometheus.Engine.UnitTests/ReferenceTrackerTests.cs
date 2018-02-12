using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.ReferenceProver;
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
        public void ReferenceTracker_ForMethodCallAssignments_TracksCorrectly()
        {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var registrationServiceClass = project.GetCompilation().GetClassDeclaration(typeof (TestProject.Services.RegistrationService));
            var identifier = registrationServiceClass.GetMethodDescendant(nameof(TestProject.Services.RegistrationService.Register)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.True(assignments.Count==1);
            Assert.True(assignments[0].Conditions.Count==0);
            Assert.True(assignments[0].Reference.ToString()=="sharedCustomer");
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TestProject.Services.TransferService.SimpleIfTransfer)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.True(assignments.Any());
            Assert.True(assignments.First().Conditions.Any(x=>x.IfStatement.Condition.ToString()== "from.Type == CustomerType.Premium"));
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfSingleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TestProject.Services.TransferService.SimpleIfSingleElseTransfer)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(2, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium"));
            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfMultipleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TestProject.Services.TransferService.SimpleIfMultipleElseTransfer)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(3, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium"));

            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Gold"));

            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Gold" && x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_SimpleNestedWith_IfMultipleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TestProject.Services.TransferService.NestedIfElseTransfer)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(3, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium"));
            Assert.True(assignments[0].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0"));

            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Gold"));
            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0"));

            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Gold" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0"));
        }

        [Test]
        public void ReferenceTracker_For_NestedIfElseWith_IfMultipleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TestProject.Services.TransferService.NestedIfElse_With_IfElseTransfer)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(6, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium"));
            Assert.True(assignments[0].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0"));

            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Gold"));
            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0"));

            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Gold" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0"));

            Assert.True(assignments[3].Conditions.Any(x => x.IfStatement.Condition.ToString() == "!from.IsActive && from.AccountBalance < 0"));
            Assert.True(assignments[3].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount < 0"));
            Assert.True(assignments[3].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0" && x.IsNegated));

            Assert.True(assignments[4].Conditions.Any(x => x.IfStatement.Condition.ToString() == "!from.IsActive && from.AccountBalance < 0" && x.IsNegated));
            Assert.True(assignments[4].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Gold && from.AccountBalance < 0"));
            Assert.True(assignments[4].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount < 0"));
            Assert.True(assignments[4].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0" && x.IsNegated));

            Assert.True(assignments[5].Conditions.Any(x => x.IfStatement.Condition.ToString() == "!from.IsActive && from.AccountBalance > 0"));
            Assert.True(assignments[5].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount < 0" && x.IsNegated));
            Assert.True(assignments[5].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0" && x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_NestedCall_SimpleIfConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService));
            var identifier = transferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(6, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium"));
            Assert.True(assignments[0].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0"));

            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Gold"));
            Assert.True(assignments[1].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0"));

            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Gold" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0"));

            Assert.True(assignments[3].Conditions.Any(x => x.IfStatement.Condition.ToString() == "!from.IsActive && from.AccountBalance < 0"));
            Assert.True(assignments[3].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount < 0"));
            Assert.True(assignments[3].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0" && x.IsNegated));

            Assert.True(assignments[4].Conditions.Any(x => x.IfStatement.Condition.ToString() == "!from.IsActive && from.AccountBalance < 0" && x.IsNegated));
            Assert.True(assignments[4].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Type == CustomerType.Gold && from.AccountBalance < 0"));
            Assert.True(assignments[4].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount < 0"));
            Assert.True(assignments[4].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0" && x.IsNegated));

            Assert.True(assignments[5].Conditions.Any(x => x.IfStatement.Condition.ToString() == "!from.IsActive && from.AccountBalance > 0"));
            Assert.True(assignments[5].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount < 0" && x.IsNegated));
            Assert.True(assignments[5].Conditions.Any(x => x.IfStatement.Condition.ToString() == "amount > 0" && x.IsNegated));

            identifier = transferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "from").First();
            assignments = referenceTracker.GetAssignments(identifier);
            Assert.True(assignments[0].Conditions.Any(x => x.IfStatement.Condition.ToString() == "from.Age > 30"));
        }
    }
}