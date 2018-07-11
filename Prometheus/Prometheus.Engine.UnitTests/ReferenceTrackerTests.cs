using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Z3;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.Model;
using Prometheus.Engine.Reachability.Tracker;
using Prometheus.Engine.Thread;
using Prometheus.Engine.Types;
using Prometheus.Engine.Types.Polymorphy;
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
            var polymorphicService = new PolymorphicResolver();
            var context = new Context();
            var typeService = new TypeService(solution, context, polymorphicService, ModelStateConfiguration.Empty, "TestProject.GUI", "TestProject.Services", "TestProject.Common");
            var threadSchedule = new ThreadAnalyzer(solution).GetThreadSchedule(solution.Projects.First(x => x.Name == "TestProject.GUI"));
            var referenceParser = new ReferenceParser();
            referenceTracker = new ReferenceTracker(solution, threadSchedule, typeService, referenceParser);
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
            Assert.AreEqual("_customerRepository", assignments[0].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference.ToString());
            Assert.AreEqual(2, assignments[0].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customers", assignments[0].RightReference.ToString());
            Assert.AreEqual("x", assignments[0].RightReference.ReferenceContexts.Peek().Query.ToString());

            Assert.AreEqual(0, assignments[1].Conditions.Count);
            Assert.AreEqual("_customerRepository", assignments[1].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference.ToString());
            Assert.AreEqual(2, assignments[1].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customers", assignments[1].RightReference.ToString());
            Assert.AreEqual("x + y", assignments[1].RightReference.ReferenceContexts.Peek().Query.ToString());
        }

        [Test]
        public void ReferenceTracker_ForNestedMethodCallAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var simpleAssignmentIdentifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_IfTransfer)).DescendantTokens<SyntaxToken>(x => x.ToString() == "innerMethodCustomer").First();
            var simpleMethodAssignments = referenceTracker.GetAssignments(simpleAssignmentIdentifier);

            var linqAssignmentIdentifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_IfTransfer)).DescendantTokens<SyntaxToken>(x => x.ToString() == "innerFirstLinqMethodCustomer").First();
            var linqMethodAssignments = referenceTracker.GetAssignments(linqAssignmentIdentifier);

            Assert.AreEqual(1, simpleMethodAssignments.Count);
            Assert.AreEqual(2, linqMethodAssignments.Count);

            Assert.AreEqual(0, simpleMethodAssignments[0].Conditions.Count);
            Assert.AreEqual(3, simpleMethodAssignments[0].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customer", simpleMethodAssignments[0].RightReference.ToString());

            Assert.AreEqual(0, linqMethodAssignments[0].Conditions.Count);
            Assert.AreEqual(null, linqMethodAssignments[0].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference);
            Assert.AreEqual(3, linqMethodAssignments[0].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customers", linqMethodAssignments[0].RightReference.ToString());
            Assert.AreEqual("x => x.Name == innerFrom.Name", linqMethodAssignments[0].RightReference.ReferenceContexts.Peek().Query.ToString());

            Assert.AreEqual(0, linqMethodAssignments[1].Conditions.Count);
            Assert.AreEqual(null, linqMethodAssignments[0].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference);
            Assert.AreEqual(3, linqMethodAssignments[1].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customer", linqMethodAssignments[1].RightReference.ToString());
            Assert.AreEqual(null, linqMethodAssignments[1].RightReference.ReferenceContexts.Peek().Query);
        }

        [Test]
        public void ReferenceTracker_ForNestedStaticMethodCallAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_IfTransfer)).DescendantTokens<SyntaxToken>(x => x.ToString() == "staticFirstLinqMethodCustomer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(3, assignments.Count);

            Assert.AreEqual(0, assignments[0].Conditions.Count);
            Assert.AreEqual(null, assignments[0].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference);
            Assert.AreEqual(3, assignments[0].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customers", assignments[0].RightReference.ToString());
            Assert.AreEqual("x => x.Age > from.Age", assignments[0].RightReference.ReferenceContexts.Peek().Query.ToString());

            Assert.AreEqual(0, assignments[1].Conditions.Count);
            Assert.AreEqual(null, assignments[0].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference);
            Assert.AreEqual(3, assignments[1].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("from", assignments[1].RightReference.ToString());
            Assert.AreEqual(null, assignments[1].RightReference.ReferenceContexts.Peek().Query);

            Assert.AreEqual(0, assignments[2].Conditions.Count);
            Assert.AreEqual(null, assignments[2].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference);
            Assert.AreEqual(3, assignments[2].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customers", assignments[2].RightReference.ToString());
            Assert.AreEqual("x => x.DeliveryAddress == from.DeliveryAddress", assignments[2].RightReference.ReferenceContexts.Peek().Query.ToString());
        }

        [Test]
        public void ReferenceTracker_ForNestedReferenceMethodCallAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_IfTransfer)).DescendantTokens<SyntaxToken>(x => x.ToString() == "innerNestedReferenceMethodCustomer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(2, assignments.Count);

            Assert.AreEqual(0, assignments[0].Conditions.Count);
            Assert.AreEqual("_customerRepository", assignments[0].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference.ToString());
            Assert.AreEqual(2, assignments[0].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customers", assignments[0].RightReference.ToString());
            Assert.AreEqual("x", assignments[0].RightReference.ReferenceContexts.Peek().Query.ToString());

            Assert.AreEqual(0, assignments[1].Conditions.Count);
            Assert.AreEqual("_customerRepository", assignments[1].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference.ToString());
            Assert.AreEqual(2, assignments[1].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customers", assignments[1].RightReference.ToString());
            Assert.AreEqual("x => x.Age == innerFrom.Age", assignments[1].RightReference.ReferenceContexts.Peek().Query.ToString());
        }

        [Test]
        public void ReferenceTracker_ForMethodCallAssignments_ForFirstLinqReturns_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_IfTransfer)).DescendantTokens<SyntaxToken>(x => x.ToString() == "firstCustomer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.True(assignments.Count == 1);
            Assert.AreEqual(0, assignments[0].Conditions.Count);
            Assert.AreEqual("_customerRepository", assignments[0].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference.ToString());
            Assert.AreEqual(1, assignments[0].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customers", assignments[0].RightReference.ToString());
            Assert.AreEqual("x => x.AccountBalance == accountBalance", assignments[0].RightReference.ReferenceContexts.Peek().Query.ToString());
        }

        [Test]
        public void ReferenceTracker_ForMethodCallAssignments_ForWhereLinqReturns_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_IfTransfer)).DescendantTokens<SyntaxToken>(x => x.ToString() == "whereCustomers").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.True(assignments.Count == 1);
            Assert.AreEqual(0, assignments[0].Conditions.Count);
            Assert.AreEqual("_customerRepository", assignments[0].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference.ToString());
            Assert.AreEqual(1, assignments[0].RightReference.ReferenceContexts.Peek().CallContext.ArgumentsTable.Count);
            Assert.AreEqual("customers", assignments[0].RightReference.ToString());
            Assert.AreEqual("x => x.Age == age", assignments[0].RightReference.ReferenceContexts.Peek().Query.ToString());
        }

        [Test]
        public void ReferenceTracker_ForMethodCallAssignments_ForLiteralKeyIndexesReturns_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService1));
            var firstIndexedIdentifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_IfTransfer)).DescendantTokens<SyntaxToken>(x => x.ToString() == "firstIndexedCustomer").First();
            var keyIndexedIdentifier = transferServiceClass.GetMethodDescendant(nameof(TransferService1.MethodAssignment_IfTransfer)).DescendantTokens<SyntaxToken>(x => x.ToString() == "keyIndexedCustomer").First();
            var firstIndexedAssignments = referenceTracker.GetAssignments(firstIndexedIdentifier);
            var keyIndexedAssignments = referenceTracker.GetAssignments(keyIndexedIdentifier);

            Assert.True(firstIndexedAssignments.Count == 1);
            Assert.AreEqual(0, firstIndexedAssignments[0].Conditions.Count);
            Assert.AreEqual("_customerRepository", firstIndexedAssignments[0].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference.ToString());
            Assert.AreEqual("customers", firstIndexedAssignments[0].RightReference.ToString());
            Assert.AreEqual("0", firstIndexedAssignments[0].RightReference.ReferenceContexts.Peek().Query.ToString());

            Assert.True(keyIndexedAssignments.Count == 1);
            Assert.AreEqual(0, keyIndexedAssignments[0].Conditions.Count);
            Assert.AreEqual("_customerRepository", keyIndexedAssignments[0].RightReference.ReferenceContexts.Peek().CallContext.InstanceReference.ToString());
            Assert.AreEqual("customersTable", keyIndexedAssignments[0].RightReference.ToString());
            Assert.AreEqual("\"key\"", keyIndexedAssignments[0].RightReference.ReferenceContexts.Peek().Query.ToString());
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
            Assert.AreEqual("sharedCustomer", assignments[0].RightReference.ToString());
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.SimpleIfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(1, assignments.Count);
            Assert.True(assignments.First().Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && !x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfSingleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.SimpleIfSingleElseTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(2, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && !x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium )" && x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_SimpleIfMultipleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.SimpleIfMultipleElseTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(3, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && !x.IsNegated));

            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium )" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold" && !x.IsNegated));

            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium )" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Gold )" && x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_SimpleNestedWith_IfMultipleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.NestedIfElseTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(3, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && !x.IsNegated));
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "amount > 0" && !x.IsNegated));

            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium )" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold" && !x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium )" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Gold )" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "amount > 0" && !x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_NestedIfElseWith_IfMultipleElseConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.NestedIfElse_With_IfElseTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(6, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && !x.IsNegated));
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "amount > 0" && !x.IsNegated));

            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium )" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold" && !x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium )" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "amount > 0" && !x.IsNegated));

            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "!from.IsActive && from.AccountBalance < 0" && !x.IsNegated));
            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "amount < 0"));
            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "NOT ( amount > 0 )" && x.IsNegated));

            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "NOT ( !from.IsActive && from.AccountBalance < 0 )" && x.IsNegated));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold && from.AccountBalance < 0"));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "amount < 0" && !x.IsNegated));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "NOT ( amount > 0 )" && x.IsNegated));

            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "!from.IsActive && from.AccountBalance > 0"));
            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "NOT ( amount < 0 )" && x.IsNegated));
            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "NOT ( amount > 0 )" && x.IsNegated));
        }

        [Test]
        public void ReferenceTracker_For_NestedCall_SimpleIfConditionalAssignments_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "customer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(6, assignments.Count);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Premium" && !x.IsNegated));
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "amount > 0" && !x.IsNegated));

            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium )" && x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold" && !x.IsNegated));
            Assert.True(assignments[1].Conditions.Any(x => x.ToString() == "amount > 0" && !x.IsNegated));

            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium )" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Gold )" && x.IsNegated));
            Assert.True(assignments[2].Conditions.Any(x => x.ToString() == "amount > 0"));

            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "!from.IsActive && from.AccountBalance < 0" && !x.IsNegated));
            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "amount < 0" && !x.IsNegated));
            Assert.True(assignments[3].Conditions.Any(x => x.ToString() == "NOT ( amount > 0 )"));

            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "NOT ( !from.IsActive && from.AccountBalance < 0 )" && x.IsNegated));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold && from.AccountBalance < 0" && !x.IsNegated));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "amount < 0" && !x.IsNegated));
            Assert.True(assignments[4].Conditions.Any(x => x.ToString() == "NOT ( amount > 0 )"));

            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "!from.IsActive && from.AccountBalance > 0" && !x.IsNegated));
            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "NOT ( amount < 0 )"));
            Assert.True(assignments[5].Conditions.Any(x => x.ToString() == "NOT ( amount > 0 )"));

            identifier = transferServiceClass.GetMethodDescendant("TransferInternal").Body.DescendantTokens<SyntaxToken>(x => x.Text == "from").First();
            assignments = referenceTracker.GetAssignments(identifier);
            Assert.True(assignments[0].Conditions.Any(x => x.ToString() == "from.Age > 30"));
        }

        [Test]
        public void ReferenceTracker_For_MethodCallAssignments_WithSimpleImplicitConditionals_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.SimpleIfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "simpleImplicitCustomer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(1, assignments.Count);
            var rightReferenceAssignments = referenceTracker.GetAssignments(assignments[0].RightReference.Node.DescendantTokens().First());

            Assert.AreEqual(2, rightReferenceAssignments[0].Conditions.Count);
            Assert.True(rightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "NOT ( !from.IsActive && from.AccountBalance == 20 AND amount < -10 )"));
            Assert.True(rightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Gold && from.AccountBalance ==30 AND NOT ( !from.IsActive && from.AccountBalance == 20 ) AND amount < -10 )"));
        }

        [Test]
        public void ReferenceTracker_For_MethodCallAssignments_WithComplexImplicitConditionals_TracksCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var transferServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TransferService));
            var identifier = transferServiceClass.GetMethodDescendant(nameof(TransferService.SimpleIfTransfer)).Body.DescendantTokens<SyntaxToken>(x => x.Text == "complexImplicitCustomer").First();
            var assignments = referenceTracker.GetAssignments(identifier);

            Assert.AreEqual(2, assignments.Count);
            var fromRightReferenceAssignments = referenceTracker.GetAssignments(assignments[0].RightReference.Node.DescendantTokens().First());
            var toRightReferenceAssignments = referenceTracker.GetAssignments(assignments[1].RightReference.Node.DescendantTokens().First());

            Assert.AreEqual(4, fromRightReferenceAssignments[0].Conditions.Count);
            Assert.True(fromRightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "from.Type == CustomerType.Gold"));
            Assert.True(fromRightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium )"));
            Assert.True(fromRightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "amount > 0"));
            Assert.True(fromRightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium AND amount > 0 )"));

            Assert.AreEqual(5, toRightReferenceAssignments[0].Conditions.Count);
            Assert.True(toRightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Premium AND amount > 0 )"));
            Assert.True(toRightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Gold AND NOT ( from.Type == CustomerType.Premium ) AND amount > 0 )"));
            Assert.True(toRightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "NOT ( NOT ( from.Type == CustomerType.Gold ) AND NOT ( from.Type == CustomerType.Premium ) AND amount > 0 )"));
            Assert.True(toRightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "NOT ( !from.IsActive && from.AccountBalance == 20 AND amount < -10 )"));
            Assert.True(toRightReferenceAssignments[0].Conditions.Any(x => x.ToString() == "NOT ( from.Type == CustomerType.Gold && from.AccountBalance == 30 AND NOT ( !from.IsActive && from.AccountBalance == 20 ) AND amount < -10 )"));
        }
    }
}