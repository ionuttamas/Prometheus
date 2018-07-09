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
                .IsPure(typeof(PaymentProvider), nameof(PaymentProvider.ValidatePaymentPure))
                .IsPure(typeof(PaymentProvider), nameof(PaymentProvider.ProcessPaymentPure));

            var threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            var polymorphicService = new PolymorphicResolver();
            var context = new Context();
            var typeService = new TypeService(solution, context, polymorphicService, "TestProject.GUI", "TestProject.Services", "TestProject.Common");
            var conditionProver = new Z3ConditionProver(typeService, modelStateConfig, context);
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
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_PureStaticCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_StaticCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_StaticCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Unsat_PureStaticCallConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_StaticCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_StaticCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

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

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_PureStaticAssignmentConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Sat_PureStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Unsat_PureStaticAssignmentConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_StaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_ImpureStaticAssignmentConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_ImpureStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_ImpureStaticAssignmentConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Negated_ImpureStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_PureReferenceAssignmentConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodReferenceAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_PureMethodReferenceAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Unsat_PureReferenceAssignmentConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_PureMethodReferenceAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Unsat_PureMethodReferenceAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Sat_ImpureReferenceAssignmentConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureMethodReferenceAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_ImpureMethodReferenceAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_With3rdPartyCheck_Negated_Sat_ImpureReferenceAssignmentConditions_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.If_3rdPartyCheck_ImpureMethodReferenceAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.If_3rdPartyCheck_Negated_Sat_ImpureMethodReferenceAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }
    }
}