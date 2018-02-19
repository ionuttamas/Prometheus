using System;
using System.IO;
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
    public class ReferenceProverTests {
        private ReferenceProver.ReferenceProver referenceProver;
        private Solution solution;

        [SetUp]
        public void Init() {
            var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            solution = workspace.OpenSolutionAsync(@"C:\Users\tamas\Documents\Github\Prometheus\Prometheus\Prometheus.sln").Result;
            ThreadSchedule threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            referenceProver = new ReferenceProver.ReferenceProver(new ReferenceTracker(solution, threadSchedule));
        }

        [TearDown]
        public void TearDown() {
            referenceProver = null;
        }

        [Test]
        public void ReferenceProver_For_SimpleIf_And_NegatedCounterpart_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.SimpleIfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.SimpleIf_NegatedTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();

            object commonValue;
            var haveCommonValue = referenceProver.HaveCommonValue(firstIdentifier, secondIdentifier, out commonValue);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReferenceProver_For_NestedIfElse_And_SatisfiableCounterpart_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.NestedCall_SimpleIf_SimpleIfTransfer_SatisfiableCounterpart)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();

            object commonValue;
            var haveCommonValue = referenceProver.HaveCommonValue(firstIdentifier, secondIdentifier, out commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }
    }
}