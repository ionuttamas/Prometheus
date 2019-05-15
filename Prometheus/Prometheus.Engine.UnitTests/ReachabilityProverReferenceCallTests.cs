
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Z3;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.ConditionProver;
using Prometheus.Engine.ExpressionMatcher;
using Prometheus.Engine.ExpressionMatcher.Query;
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
    public class ReachabilityProverReferenceCallTests {
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
            var referenceParser = new ReferenceParser();
            var expressionParser = new Z3BooleanExpressionParser(typeService, referenceParser, context);
            var conditionProver = new Z3ConditionProver(expressionParser, context);
            var queryMatcher = new Z3QueryMatcher(typeService, context);
            var conditionExtractor = new ConditionExtractor();
            var referenceTracker = new ReferenceTracker(solution, threadSchedule, typeService, referenceParser, conditionExtractor);
            reachabilityProver = new Reachability.Prover.ReachabilityProver(referenceTracker, conditionProver, queryMatcher, typeService);
            var methodParser = new Z3BooleanMethodParser(expressionParser, conditionExtractor, context);

            expressionParser.Configure(reachabilityProver.HaveCommonReference);
            expressionParser.Configure(referenceTracker.GetAssignments);
            expressionParser.Configure((ParseBooleanMethod) methodParser.ParseBooleanMethod);
            expressionParser.Configure((ParseCachedBooleanMethod) methodParser.ParseCachedBooleanMethod);
        }

        [TearDown]
        public void TearDown() {
            reachabilityProver.Dispose();
        }

        #region Bool Method Parsing in Test Conditions
        [Test]
        public void ReachabilityProver_For_FieldReferenceCall_InTestCondition_Sat_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.IfCheck_FieldReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.IfCheck_Sat_FieldReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_FieldReferenceCall_InTestCondition_Unsat_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.IfCheck_FieldReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.IfCheck_Unsat_FieldReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_LocallyInitialized_FieldReferenceCall_InTestCondition_Sat_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.IfCheck_LocallyInitialized_FieldReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.IfCheck_Sat_LocallyInitialized_FieldReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_LocallyInitialized_FieldReferenceCall_InTestCondition_Unsat_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.IfCheck_LocallyInitialized_FieldReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.IfCheck_Unsat_LocallyInitialized_FieldReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }

        [Test]
        public void ReachabilityProver_For_ThisReferenceCall_InTestCondition_Sat_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.IfCheck_ThisReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.IfCheck_Sat_ThisReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var commonValue);

            Assert.True(haveCommonValue);
            Assert.AreEqual("sharedCustomer", commonValue.ToString());
        }

        [Test]
        public void ReachabilityProver_For_ThisReferenceCall_InTestCondition_Unsat_ProvesCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferService1Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService1));
            var transferService2Class = project.GetCompilation().GetClassDeclaration(typeof(TestProject.Services.TransferService2));

            var firstIdentifier = transferService1Class.GetMethodDescendant(nameof(TestProject.Services.TransferService1.IfCheck_ThisReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer1").First();
            var secondIdentifier = transferService2Class.GetMethodDescendant(nameof(TestProject.Services.TransferService2.IfCheck_Unsat_ThisReferenceCall)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer2").First();
            var haveCommonValue = reachabilityProver.HaveCommonReference(new Reference(firstIdentifier), new Reference(secondIdentifier), out var _);

            Assert.False(haveCommonValue);
        }
        #endregion
    }
}