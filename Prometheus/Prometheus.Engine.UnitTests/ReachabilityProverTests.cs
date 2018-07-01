using System.Linq;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.ConditionProver;
using Prometheus.Engine.ExpressionMatcher;
using Prometheus.Engine.Reachability.Tracker;
using Prometheus.Engine.ReachabilityProver.Model;
using Prometheus.Engine.Thread;
using Prometheus.Engine.Types;
using Prometheus.Engine.Types.Polymorphy;

namespace Prometheus.Engine.UnitTests
{
    [TestFixture]
    public class ReachabilityProverTests {
        private Reachability.Prover.ReachabilityProver reachabilityProver;
        private Solution solution;

        [SetUp]
        public void Init() {
            var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            solution = workspace.OpenSolutionAsync(@"C:\Users\tamas\Documents\Github\Prometheus\Prometheus\Prometheus.sln").Result;
            ThreadSchedule threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            IPolymorphicResolver polymorphicService = new PolymorphicResolver();
            ITypeService typeService = new TypeService(solution, polymorphicService, "TestProject.GUI", "TestProject.Services", "TestProject.Common");
            IConditionProver conditionProver = new Z3ConditionProver(typeService);
            IQueryMatcher queryMatcher = new Z3QueryMatcher(typeService);
            IReferenceParser referenceParser = new ReferenceParser();
            ReferenceTracker referenceTracker = new ReferenceTracker(solution, threadSchedule, typeService, referenceParser);
            reachabilityProver = new Reachability.Prover.ReachabilityProver(referenceTracker, conditionProver, queryMatcher);
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

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.NestedCall_SimpleIfTransfer_SatisfiableCounterpart)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").Skip(1).First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_StringConditions_And_SatisfiableCounterpart_TracksCorrectly()
        {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.StringCondition_SimpleIfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.StringCondition_SimpleIf_NegatedTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "referenceCustomer").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_NestedIfElse_And_NonSatisfiableCounterpart_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.NestedCall_SimpleIfTransfer_SatisfiableCounterpart)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "exclusiveCustomer").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_WithSatisfiable_MethodCallAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.MethodAssignment_SimpleAssign)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("customers", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_WithComplexSatisfiable_MethodCallAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "refCustomer").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "refCustomer").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("customers", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_WithEnumSatisfiableConditions_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "enumCustomer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "enumCustomer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_WithEnumUnsatisfiableConditions_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "unsatEnumCustomer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "unsatEnumCustomer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_WithSelfReferentialConditions_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "selfReferentialCustomer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "selfReferentialCustomer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }
    }
}