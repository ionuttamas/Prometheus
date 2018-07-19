using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Z3;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.ConditionProver;
using Prometheus.Engine.ExpressionMatcher;
using Prometheus.Engine.Model;
using Prometheus.Engine.Reachability.Tracker;
using Prometheus.Engine.ReachabilityProver.Model;
using Prometheus.Engine.Thread;
using Prometheus.Engine.Types;
using Prometheus.Engine.Types.Polymorphy;
using Prometheus.Engine.Verifier;
using TestProject._3rdParty;

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
            var modelStateConfig = ModelStateConfiguration
                .Empty
                .IsPure(typeof(BackgroundCheckHelper), nameof(BackgroundCheckHelper.ValidateSsnPure))
                .IsPure(typeof(BackgroundCheckHelper), nameof(BackgroundCheckHelper.StaticProcessPaymentPure))
                .IsPure(typeof(PaymentProvider), nameof(PaymentProvider.ValidatePaymentPure))
                .IsPure(typeof(PaymentProvider), nameof(PaymentProvider.ProcessPaymentPure));

            var threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            var polymorphicService = new PolymorphicResolver();
            var context = new Context();
            var typeService = new TypeService(solution, context, polymorphicService, modelStateConfig, "TestProject.GUI", "TestProject.Services", "TestProject.Common");
            var conditionProver = new Z3ConditionProver(typeService, context);
            var queryMatcher = new Z3QueryMatcher(typeService, context);
            var referenceParser = new ReferenceParser();
            var referenceTracker = new ReferenceTracker(solution, threadSchedule, typeService, referenceParser);
            reachabilityProver = new Reachability.Prover.ReachabilityProver(referenceTracker, conditionProver, queryMatcher);
        }

        [TearDown]
        public void TearDown() {
            reachabilityProver.Dispose();
        }

        [Test]
        public void ReachabilityProver_For_SimpleIf_And_NegatedCounterpart_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.SimpleIfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.SimpleIf_NegatedTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "referenceCustomer").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_NestedIfElse_And_SatisfiableCounterpart_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.NestedCall_SimpleIfTransfer_SatisfiableCounterpart)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").Skip(1).First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_StringConditions_And_SatisfiableCounterpart_ProvesCorrectly()
        {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.StringCondition_SimpleIfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.StringCondition_SimpleIf_NegatedTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "referenceCustomer").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_NestedIfElse_And_NonSatisfiableCounterpart_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var proverTransferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.ProverTransferService));

            var firstIdentifier = proverTransferServiceClass.GetMethodDescendant(nameof(TestProject.Services.ProverTransferService.NestedCall_SimpleIfTransfer_SatisfiableCounterpart)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var secondIdentifier = proverTransferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "exclusiveCustomer").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_WithSatisfiable_MethodCallAssignments_ProvesCorrectly() {
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
        public void ReachabilityProver_For_WithComplexSatisfiable_MethodCallAssignments_ProvesCorrectly() {
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
        public void ReachabilityProver_For_WithEnumSatisfiableConditions_ProvesCorrectly() {
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
        public void ReachabilityProver_For_WithEnumUnsatisfiableConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "unsatEnumCustomer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "unsatEnumCustomer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_WithSelfReferentialConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "selfReferentialCustomer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.MethodAssignment_IfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "selfReferentialCustomer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_WithSatisfiableNullCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_NullCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_NullCheck_Satisfiable)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_WithUnsat_NullCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_NullCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_NullCheck_Satisfiable)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyReferences_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "paymentProvider").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_PureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "paymentProvider").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("paymentProvider", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_WithValueVariables_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "amount").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_PureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "amount").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("100", commonValue.ToString());
        }

        #region Pure Static Call
        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_PureStaticCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_StaticPureCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_StaticPureCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Unsat_PureStaticCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_StaticPureCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_StaticPureCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_PureStaticCallConditions_DifferentArgs_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_StaticPureCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Negated_Sat_StaticPureCall_DifferentArgs)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        #endregion

        #region Impure Static Call

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_ImpureStaticCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_StaticImpureCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_StaticImpureCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_ImpureStaticCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_StaticImpureCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Negated_Sat_StaticImpureCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        #endregion

        #region Pure Reference Call

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_PureReferenceCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_PureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Unsat_PureReferenceCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_PureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        #endregion

        #region Impure Reference Call

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_ImpureReferenceCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_ImpureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_ImpureReferenceCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Negated_ImpureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        #endregion

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_Pure_And_Impure_ReferenceCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Negated_ImpureReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        #region Pure Static Assignment

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_PureStaticAssignment_DirectCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodStaticAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Sat_PureStaticAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Unsat_PureStaticAssignment_DirectCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodStaticAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_StaticAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_PureStaticAssignment_MemberCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodStaticAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Sat_PureMethodStaticAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Unsat_PureStaticAssignment_MemberCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodStaticAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_PureMethodStaticAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        #endregion

        #region Impure Static Assignment

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_ImpureStaticAssignment_DirectCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_ImpureStaticAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_ImpureStaticAssignment_DirectCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Negated_ImpureStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_ImpureStaticAssignment_MemberCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureMethodStaticAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_ImpureMethodStaticAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_ImpureStaticAssignment_MemberCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureMethodStaticAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Negated_Sat_ImpureMethodStaticAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        #endregion

        #region Pure Reference Assignment
        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_PureReferenceAssignment_MemberCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodReferenceAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_PureMethodReferenceAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_PureReferenceAssignment_DifferentArgs_MemberCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodReferenceAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Sat_Negated_PureMethodReferenceAssignment_DifferentArgs_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_PureReferenceAssignment_DirectCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodReferenceAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Sat_PureMethodReferenceAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Unsat_PureReferenceAssignment_MemberCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodReferenceAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_PureMethodReferenceAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Unsat_PureReferenceAssignment_DirectCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodReferenceAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_PureMethodReferenceAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        #endregion

        #region Impure Reference Assignment
        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_ImpureReferenceAssignment_MemberCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureMethodReferenceAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_ImpureMethodReferenceAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_ImpureReferenceAssignment_DirectCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureMethodReferenceAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_ImpureMethodReferenceAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_ImpureReferenceAssignment_DirectCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureMethodReferenceAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Negated_Sat_ImpureMethodReferenceAssignment_DirectCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_ImpureReferenceAssignment_MemberCheckConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureMethodReferenceAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Negated_Sat_ImpureMethodReferenceAssignment_MemberCheck)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }
        #endregion
    }
}