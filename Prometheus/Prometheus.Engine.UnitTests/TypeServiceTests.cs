using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.Types;
using Prometheus.Engine.Types.Polymorphy;
using TestProject.Services;

namespace Prometheus.Engine.UnitTests
{
    [TestFixture]
    public class TypeServiceTests {
        private ITypeService typeService;
        private Solution solution;

        [SetUp]
        public void Init() {
            var workspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            solution = workspace.OpenSolutionAsync(@"C:\Users\tamas\Documents\Github\Prometheus\Prometheus\Prometheus.sln").Result;
            IPolymorphicResolver polymorphicService = new PolymorphicResolver();
            polymorphicService.Register(typeof(TestTypeService), "GetPolymorphicTypeDeclaration", "field", typeof(AskField));
            polymorphicService.Register(typeof(TestTypeService), "GetPolymorphicTypeParameter", "currentPriceField", typeof(CurrentPriceField));
            typeService = new TypeService(solution, polymorphicService);
        }

        [TearDown]
        public void TearDown() {
            typeService = null;
        }

        [Test]
        public void TypeServices_ForSimpleAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.SimpleAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForSplitAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.SplitAssignment)).Body.DescendantNodes<IdentifierNameSyntax>(x => x.Identifier.Text == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForVarAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.VarAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForVarNestedAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.VarNestedAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(string), type);
        }

        [Test]
        public void TypeServices_ForFieldAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.FieldAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForFieldNestedAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.FieldNestedAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(string), type);
        }

        [Test]
        public void TypeServices_ForPropertyAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.PropertyAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForPropertyNestedAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.PropertyNestedAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(string), type);
        }

        [Test]
        public void TypeServices_ForParameterInference_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ParameterInference)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForLocalStaticAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.LocalStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForLocalInstanceAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.LocalInstanceAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForExternalVarStaticAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ExternalVarStaticAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForExternalVarMethodAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ExternalVarMethodAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForExternalVarFieldAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ExternalVarFieldAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForExternalVarPropertyAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ExternalVarPropertyAssignment)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForGenericListLambdaExpressions_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.GenericListWhereLambdaExpression)).Body.DescendantNodes<MemberAccessExpressionSyntax>(x => x.ToString() == "x.DeliveryAddress.City").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(string), type);
        }

        [Test]
        public void TypeServices_ForGenericIEnumerableLambdaExpressions_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.GenericIEnumerableWhereLambdaExpression)).Body.DescendantNodes<MemberAccessExpressionSyntax>(x => x.ToString() == "x.DeliveryAddress.City").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(string), type);
        }

        [Test]
        public void TypeServices_ForArrayLambdaExpressions_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ArrayWhereLambdaExpression)).Body.DescendantNodes<MemberAccessExpressionSyntax>(x => x.ToString() == "x.DeliveryAddress.City").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(string), type);
        }

        [Test]
        public void TypeServices_ForPolymorphicDeclarationType_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.GetPolymorphicTypeDeclaration)).Body.DescendantTokens<SyntaxToken>(x => x.ToString() == "field").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(AskField), type);
        }

        [Test]
        public void TypeServices_ForPolymorphicParameterType_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.GetPolymorphicTypeParameter)).DescendantTokens<SyntaxToken>(x => x.ToString() == "currentPriceField").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(CurrentPriceField), type);
        }
    }
}