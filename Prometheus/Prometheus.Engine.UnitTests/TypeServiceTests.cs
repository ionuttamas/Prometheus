using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.Types;
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
            typeService = new TypeService(solution);
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
            //var type = typeService.GetType(identifier);
            //Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForSplitAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.SplitAssignment)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForVarAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.VarAssignment)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForVarNestedAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.VarNestedAssignment)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForFieldAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.FieldAssignment)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForParameterInference_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ParameterInference)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForLocalStaticAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.LocalStaticAssignment)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForLocalInstanceAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.LocalInstanceAssignment)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForExternalVarStaticAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ExternalVarStaticAssignment)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForExternalVarMethodAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ExternalVarMethodAssignment)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForExternalVarFieldAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ExternalVarFieldAssignment)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }

        [Test]
        public void TypeServices_ForExternalVarPropertyAssignment_GetsTypeCorrectly() {
            var project = solution.Projects.First(x => x.Name == "TestProject.Services");
            var testTypeServiceClass = project.GetCompilation().GetClassDeclaration(typeof(TestTypeService));
            var identifier = testTypeServiceClass.GetMethodDescendant(nameof(TestTypeService.ExternalVarPropertyAssignment)).Body.DescendantTokens<IdentifierNameSyntax>(x => x.ToString() == "localVar").First();
            var type = typeService.GetType(identifier);
            Assert.AreEqual(typeof(Customer), type);
        }
    }
}