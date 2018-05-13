using System.Linq;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.ReachabilityProver;
using Prometheus.Engine.Thread;
using Prometheus.Engine.Types;
using TestProject.Services;

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
            var typeService = new TypeService(solution);
            ThreadSchedule threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            referenceTracker = new ReferenceTracker(solution, threadSchedule, typeService);
        }

        [TearDown]
        public void TearDown()
        {
            referenceTracker = null;
        }

        [Test]
        public void ReferenceTracker_ForMethodCallAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_IfTransfer)).DescendantTokens<SyntaxToken>(x => x.ToString() == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.True(assignments.Count == 2);

            Assert.AreEqual(0, assignments[0].Conditions.Count);
            Assert.AreEqual("_customerRepository", assignments[0].Reference.InstanceReference.ToString());
            Assert.AreEqual("customers[x]", assignments[0].Reference.ToString());

            Assert.AreEqual(0, assignments[1].Conditions.Count);
            Assert.AreEqual("_customerRepository", assignments[1].Reference.InstanceReference.ToString());
            Assert.AreEqual("customers[x + y]", assignments[1].Reference.ToString());
        }

        [Test]
        public void ReferenceTracker_ForMethodReferences_TracksCorrectly()
        {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var registrationServiceClass = project.GetCompilation().GetClassDeclaration(typeof(RegistrationService));
            var identifier = registrationServiceClass.GetMethodDescendant(nameof(RegistrationService.Register)).DescendantTokens<SyntaxToken>(x => x.ToString() == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(1, assignments.Count);
            Assert.AreEqual(0, assignments[0].Conditions.Count);
            Assert.AreEqual("sharedCustomer", assignments[0].Reference.ToString());
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.SimpleIfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(1, assignments.Count);
            Assert.True(assignments.First().Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium"));
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfSingleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.SimpleIfSingleElseTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(2, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium"));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfMultipleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.SimpleIfMultipleElseTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(3, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium"));

            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold"));

            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold" && x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_SimpleNestedWith_IfMultipleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.NestedIfElseTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(3, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium"));
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold"));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "amount > 0"));
        }

        [Test]
        public void ReferenceTracker_For_NestedIfElseWith_IfMultipleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.NestedIfElse_With_IfElseTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(6, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium"));
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold"));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "!from.IsActive && from.AccountBalance < 0"));
            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "amount < 0"));
            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "amount > 0" && x.IsNegated));

            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "!from.IsActive && from.AccountBalance < 0" && x.IsNegated));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold && from.AccountBalance < 0"));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "amount < 0"));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "amount > 0" && x.IsNegated));

            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "!from.IsActive && from.AccountBalance > 0"));
            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "amount < 0" && x.IsNegated));
            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "amount > 0" && x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_NestedCall_SimpleIfConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(6, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium"));
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold"));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "!from.IsActive && from.AccountBalance < 0"));
            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "amount < 0"));
            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "amount > 0" && x.IsNegated));

            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "!from.IsActive && from.AccountBalance < 0" && x.IsNegated));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold && from.AccountBalance < 0"));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "amount < 0"));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "amount > 0" && x.IsNegated));

            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "!from.IsActive && from.AccountBalance > 0"));
            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "amount < 0" && x.IsNegated));
            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "amount > 0" && x.IsNegated));

            identifier = transferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "from").First();
            assignments = referenceTracker.GetAssignments(identifier);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Age > 30"));
        }
    }
}