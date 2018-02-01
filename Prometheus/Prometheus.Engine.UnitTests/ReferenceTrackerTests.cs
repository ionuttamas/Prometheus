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
        public void ReferenceTracker_InstanceSharedField_ForNonConditionalAssignments_TracksCorrectly()
        {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var registrationServiceClass = project.GetCompilation().GetClassDeclaration(typeof (TestProject.Services.RegistrationService));
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof (TestProject.Services.TransferService));
            var firstIdentifier = registrationServiceClass.GetMethodDescendant(nameof(TestProject.Services.RegistrationService.Register)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var secondIdentifier = transferServiceClass.GetMethodDescendant(nameof(TestProject.Services.TransferService.Transfer)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "from").First();

            SyntaxNode commonValue;
            var haveCommonValue = referenceTracker.HaveCommonValue(firstIdentifier, secondIdentifier, out commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReferenceTracker_InstanceSharedField_ForOnSided_SimpleIfConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var registrationServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.RegistrationService));
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService));
            var firstIdentifier = registrationServiceClass.GetMethodDescendant(nameof(TestProject.Services.RegistrationService.SimpleIfRegister)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var secondIdentifier = transferServiceClass.GetMethodDescendant(nameof(TestProject.Services.TransferService.Transfer)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "from").First();

            SyntaxNode commonValue;
            var haveCommonValue = referenceTracker.HaveCommonValue(firstIdentifier, secondIdentifier, out commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReferenceTracker_InstanceSharedField_ForTwoSided_SimpleIfConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var registrationServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.RegistrationService));
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService));
            var firstIdentifier = registrationServiceClass.GetMethodDescendant(nameof(TestProject.Services.RegistrationService.SimpleIfRegister)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "customer").First();
            var secondIdentifier = transferServiceClass.GetMethodDescendant(nameof(TestProject.Services.TransferService.SimpleIfTransfer)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "from").First();

            SyntaxNode commonValue;
            var haveCommonValue = referenceTracker.HaveCommonValue(firstIdentifier, secondIdentifier, out commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }
    }
}