using System.Linq;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.ConditionProver;
using Prometheus.Engine.ReachabilityProver;
using Prometheus.Engine.Thread;
using Prometheus.Engine.Types;

namespace Prometheus.Engine.UnitTests
{
    [TestFixture]
    public class ReachabilityProverTests {
        private ReachabilityProver.ReachabilityProver reachabilityProver;
        private Solution solution;

        [SetUp]
        public void Init() {
            var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            solution = workspace.OpenSolutionAsync(@"C:\Users\tamas\Documents\Github\Prometheus\Prometheus\Prometheus.sln").Result;
            ThreadSchedule threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            ITypeService typeService = new TypeService(solution);
            IConditionProver conditionProver = new Z3ConditionProver(typeService);
            reachabilityProver = new ReachabilityProver.ReachabilityProver(new ReferenceTracker(solution, threadSchedule), conditionProver);
        }

        [TearDown]
        public void TearDown() {
            reachabilityProver.Dispose();
        }

        [Test]
        public void ReachabilityProver_For_SimpleIf_And_NegatedCounterpart_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.SimpleIfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.SimpleIf_NegatedTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "referenceCustomer").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_NestedIfElse_And_SatisfiableCounterpart_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.NestedCall_SimpleIf_SimpleIfTransfer_SatisfiableCounterpart)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out Reference commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_NestedIfElse_And_NonSatisfiableCounterpart_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.NestedCall_SimpleIf_SimpleIfTransfer_SatisfiableCounterpart)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "exclusiveCustomer").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }
    }
}