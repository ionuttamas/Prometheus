using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using NUnit.Framework;
using Prometheus.Common;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine.UnitTests {
    [TestFixture]
    public class ThreadAnalyzerTests
    {
        private ThreadAnalyzer threadAnalyzer;
        private MSBuildWorkspace workspace;

        [SetUp]
        public void Init()
        {
            workspace = MSBuildWorkspace.Create();
            var solution = workspace.OpenSolutionAsync(@"C:\Users\Tamas Ionut\Documents\Prometheus\Prometheus\Prometheus.sln").Result;
            //var sol2 = GetSolution(workspace.CurrentSolution);

            threadAnalyzer = new ThreadAnalyzer(solution);
        }

        [TearDown]
        public void TearDown()
        {
            threadAnalyzer = null;
        }

        [Test]
        public void ThreadAnalyzer_WithAtomicityAnalyzer() {
            #region cool
            /*var source1 = @"
namespace NS
{
public class C
{
public void MethodThatWeAreTryingToFind()
{
}
public void AnotherMethod()
{
    MethodThatWeAreTryingToFind(); // First Reference.
}
}
}";
    var source2 = @"
using NS;
using Alias=NS.C;
class Program
{
static void Main()
{
var c1 = new C();
c1.MethodThatWeAreTryingToFind(); // Second Reference.
c1.AnotherMethod();
var c2 = new Alias();
c2.MethodThatWeAreTryingToFind(); // Third Reference.
}
}";
    var project1Id = ProjectId.CreateNewId();
    var project2Id = ProjectId.CreateNewId();
    var document1Id = DocumentId.CreateNewId(project1Id);
    var document2Id = DocumentId.CreateNewId(project2Id);

    var solution = new AdhocWorkspace().CurrentSolution
        .AddProject(project1Id, "Project1", "Project1", LanguageNames.CSharp)
        .AddMetadataReference(project1Id, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
        .AddDocument(document1Id, "File1.cs", source1)
        .WithProjectCompilationOptions(project1Id,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
        .AddProject(project2Id, "Project2", "Project2", LanguageNames.CSharp)
        .AddMetadataReference(project2Id, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
        .AddProjectReference(project2Id, new ProjectReference(project1Id))
        .AddDocument(document2Id, "File2.cs", source2);

    // If you wish to try against a real solution you could use code like
    // var solution = Solution.Load("<Path
    // OR var solution = Workspace.LoadSolution("<Path>").CurrentSolution;


    var project1 = solution.GetProject(project1Id);
    var document1 = project1.GetDocument(document1Id);

    // Get MethodDeclarationSyntax corresponding to the 'MethodThatWeAreTryingToFind'.
    MethodDeclarationSyntax methodDeclaration = document1.GetSyntaxRootAsync().Result
        .DescendantNodes().OfType<MethodDeclarationSyntax>()
        .Single(m => m.Identifier.ValueText == "MethodThatWeAreTryingToFind");

    // Get MethodSymbol corresponding to the 'MethodThatWeAreTryingToFind'.
    var method = (IMethodSymbol)ModelExtensions.GetDeclaredSymbol(document1.GetSemanticModelAsync().Result, methodDeclaration);

    // Find all references to the 'MethodThatWeAreTryingToFind' in the solution.
    IEnumerable<ReferencedSymbol> methodReferences = SymbolFinder.FindReferencesAsync(method, solution).Result;
    Assert.AreEqual(1, methodReferences.Count());
    ReferencedSymbol methodReference = methodReferences.Single();
    Assert.AreEqual(3, methodReference.Locations.Count());

    var methodDefinition = (IMethodSymbol)methodReference.Definition;
    Assert.AreEqual("MethodThatWeAreTryingToFind", methodDefinition.Name);
    Assert.IsTrue(methodReference.Definition.Locations.Single().IsInSource);
    Assert.AreEqual("File1.cs", methodReference.Definition.Locations.Single().SourceTree.FilePath);

    Assert.IsTrue(methodReference.Locations
        .All(referenceLocation => referenceLocation.Location.IsInSource));
    Assert.AreEqual(1, methodReference.Locations
        .Count(referenceLocation => referenceLocation.Document.Name == "File1.cs"));
    Assert.AreEqual(2, methodReference.Locations
        .Count(referenceLocation => referenceLocation.Document.Name == "File2.cs"));


    var proj = workspace.CurrentSolution.Projects.First(x => x.Name == "TestProject.Services");
    var doc = proj.Documents.ToList()[2];
    methodDeclaration = doc.GetSyntaxRootAsync().Result
        .DescendantNodes().OfType<MethodDeclarationSyntax>()
        .Single(m => m.Identifier.ValueText == "Start");
    var sym = methodDeclaration.GetSemanticModel(proj.GetCompilation()).GetDeclaredSymbol(methodDeclaration);
    method = (IMethodSymbol)ModelExtensions.GetDeclaredSymbol(doc.GetSemanticModelAsync().Result, methodDeclaration);
    methodReferences = SymbolFinder.FindReferencesAsync(sym, workspace.CurrentSolution).Result;
*/
            #endregion

            var result = threadAnalyzer.GetThreadSchedule(workspace.CurrentSolution.Projects.First(x=>x.Name== "TestProject.GUI"));
        }

        private static Solution GetSolution(Solution solution)
        {
            var adHocSolution = new AdhocWorkspace().CurrentSolution;


            foreach (var project in solution.Projects)
            {
                adHocSolution = adHocSolution
                    .AddProject(project.Id, project.Name, project.AssemblyName, LanguageNames.CSharp)
                    .AddMetadataReferences(project.Id, project.MetadataReferences)
                    .WithProjectCompilationOptions(project.Id, project.CompilationOptions);

                foreach (var document in project.Documents)
                {
                    adHocSolution = adHocSolution.AddDocument(document.Id, document.Name, document.GetTextAsync().Result);
                }
            }

            return solution;
            /*.AddMetadataReference(project1Id, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddDocument(document1Id, "File1.cs", source1)
                .WithProjectCompilationOptions(project1Id,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddProject(project2Id, "Project2", "Project2", LanguageNames.CSharp)
                .AddMetadataReference(project2Id, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddProjectReference(project2Id, new ProjectReference(project1Id))
                .AddDocument(document2Id, "File2.cs", source2);*/
        }
    }
}