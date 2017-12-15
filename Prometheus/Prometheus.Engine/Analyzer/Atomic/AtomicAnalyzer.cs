using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Prometheus.Common;
using Prometheus.Engine.Model;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine.Analyzer.Atomic
{
    /// <summary>
    /// This checker verifies if a variable is updated atomically within a code base.
    /// </summary>
    internal class AtomicAnalyzer : IAnalyzer
    {
        public Solution Solution { get; set; }
        public ThreadSchedule ThreadSchedule { get; set; }
        public ModelStateConfiguration ModelStateConfiguration { get; set; }

        public IAnalysis Analyze(IInvariant invariant)
        {
            var atomicInvariant = (AtomicInvariant) invariant;

            if (atomicInvariant.Member is PropertyInfo)
            {
                var propertyInfo = (PropertyInfo) atomicInvariant.Member;

                return propertyInfo.GetMethod.IsPublic ?
                    AnalyzePublicMember(propertyInfo) :
                    AnalyzePrivateMember(propertyInfo);
            }

            if (atomicInvariant.Member is FieldInfo)
            {
                var fieldInfo = (FieldInfo)atomicInvariant.Member;

                return fieldInfo.IsPrivate ?
                    AnalyzePrivateMember(fieldInfo) :
                    AnalyzePublicMember(fieldInfo);
            }

            throw new ArgumentException("The specified member is not supported", nameof(invariant));
        }

        private IAnalysis AnalyzePrivateMember(MemberInfo member)
        {
            Type type = member.DeclaringType;
            string assemblyName = type.Assembly.GetName().Name;
            var typeName = $"{type.Namespace}.{type.Name}";
            Project project = Solution.Projects.First(x => x.AssemblyName == assemblyName);
            Compilation compilation = project.GetCompilation();
            ISymbol memberSymbol = compilation.GetTypeByMetadataName(typeName).GetMembers(member.Name).First();
            List<ReferenceLocation> locations = SymbolFinder.FindReferencesAsync(memberSymbol, Solution).Result.SelectMany(x=>x.Locations).ToList();
            var classDeclaration = compilation.GetTypeByMetadataName(typeName).DeclaringSyntaxReferences[0].GetSyntax().As<ClassDeclarationSyntax>();
            var lockChains = new List<List<LockContext>>();

            foreach (ReferenceLocation location in locations)
            {
                if(ThreadSchedule.GetThreadPath(Solution, location.Location)==null)
                    continue;

                var identifierNode = (IdentifierNameSyntax)compilation
                    .SyntaxTrees
                    .First(x => x.FilePath == location.Document.FilePath)
                    .GetSyntaxNode(location);
                var memberAccessNode = identifierNode.Parent as MemberAccessExpressionSyntax;

                //TODO: need to also check field referencing and changing: e.g. "var newList = list;" => track if newList is modified atomically
                if (memberAccessNode?.Expression is IdentifierNameSyntax)
                {
                    var methodName = ((IdentifierNameSyntax) memberAccessNode.Name).Identifier.Text; //TODO: check method signature, not only its name
                    var changesState = ModelStateConfiguration.ChangesState(member.GetUnderlyingType(), methodName);

                    if (!changesState)
                        continue;

                    var locks = ExtractClassLocks(memberAccessNode, compilation, classDeclaration);
                    lockChains.AddRange(locks);
                }
            }

            var result = ProcessAnalysis(lockChains);

            return result;
        }

        private IAnalysis AnalyzePublicMember(MemberInfo member) {
            Type type = member.DeclaringType;
            string assemblyName = type.Assembly.GetName().Name;
            var typeName = $"{type.Namespace}.{type.Name}";
            Project project = Solution.Projects.First(x => x.AssemblyName == assemblyName);
            Compilation compilation = project.GetCompilation();
            ISymbol memberSymbol = compilation.GetTypeByMetadataName(typeName).GetMembers(member.Name).First();
            List<ReferenceLocation> locations = SymbolFinder.FindReferencesAsync(memberSymbol, Solution).Result.SelectMany(x => x.Locations).ToList();
            var lockChains = new List<List<LockContext>>();

            foreach (ReferenceLocation location in locations) {
                if (ThreadSchedule.GetThreadPath(Solution, location.Location) == null)
                    continue;

                var identifierNode = (IdentifierNameSyntax)compilation
                    .SyntaxTrees
                    .First(x => x.FilePath == location.Document.FilePath)
                    .GetSyntaxNode(location);
                var memberAccessNode = identifierNode.Parent as MemberAccessExpressionSyntax;

                //TODO: need to also check field referencing and changing: e.g. "var newList = list;" => track if newList is modified atomically
                if (memberAccessNode?.Expression is IdentifierNameSyntax) {
                    var methodName = ((IdentifierNameSyntax)memberAccessNode.Name).Identifier.Text; //TODO: check method signature, not only its name
                    var changesState = ModelStateConfiguration.ChangesState(member.GetUnderlyingType(), methodName);

                    if (!changesState)
                        continue;

                    var locks = ExtractSolutionLocks(memberAccessNode, compilation);
                    lockChains.AddRange(locks);
                }
            }

            var result = ProcessAnalysis(lockChains);

            return result;
        }

        private List<List<LockContext>> ExtractClassLocks(SyntaxNode node, Compilation compilation, ClassDeclarationSyntax classNode)
        {
            var currentNode = node;
            var methodDeclaration = node.AncestorNodes<MethodDeclarationSyntax>().FirstOrDefault();
            var lockNode = currentNode.AncestorNodes<LockStatementSyntax>().FirstOrDefault();
            var lockContexts = new List<LockContext>();
            var result = new List<List<LockContext>>();

            while (lockNode!=null)
            {
                var lockContext = new LockContext
                {
                    LockInstance = lockNode.Expression.ToString(),
                    LockStatementSyntax = lockNode,
                    Method = methodDeclaration
                };
                lockContexts.Add(lockContext);
                currentNode = lockNode;
                lockNode = currentNode.AncestorNodes<LockStatementSyntax>().FirstOrDefault();
            }

            var methodSymbol = methodDeclaration.GetSemanticModel(compilation).GetSymbolInfo(methodDeclaration).Symbol;

            foreach (ReferenceLocation location in SymbolFinder.FindReferencesAsync(methodSymbol, Solution).Result.SelectMany(x => x.Locations))
            {
                if (!classNode.GetLocation().SourceSpan.Contains(location.Location.SourceSpan))
                    continue;

                var invocationNode = compilation
                    .SyntaxTrees
                    .First(x => x.FilePath == location.Document.FilePath)
                    .GetSyntaxNode(location) as InvocationExpressionSyntax;
                var invocationCallsLocks = ExtractClassLocks(invocationNode, compilation, classNode);

                foreach (List<LockContext> invocationLocks in invocationCallsLocks)
                {
                    invocationLocks.InsertRange(0, lockContexts);
                    result.Add(invocationLocks);
                }
            }

            if (!result.Any())
            {
                result.Add(lockContexts);
            }

            return result;
        }

        private List<List<LockContext>> ExtractSolutionLocks(SyntaxNode node, Compilation compilation) {
            var currentNode = node;
            var methodDeclaration = node.AncestorNodes<MethodDeclarationSyntax>().FirstOrDefault();
            var lockNode = currentNode.AncestorNodes<LockStatementSyntax>().FirstOrDefault();
            var lockContexts = new List<LockContext>();
            var result = new List<List<LockContext>>();

            while (lockNode != null) {
                var lockContext = new LockContext {
                    LockInstance = lockNode.Expression.ToString(),
                    LockStatementSyntax = lockNode,
                    Method = methodDeclaration
                };
                lockContexts.Add(lockContext);
                currentNode = lockNode;
                lockNode = currentNode.AncestorNodes<LockStatementSyntax>().FirstOrDefault();
            }

            var methodSymbol = methodDeclaration.GetSemanticModel(compilation).GetSymbolInfo(methodDeclaration).Symbol;

            foreach (ReferenceLocation location in SymbolFinder.FindReferencesAsync(methodSymbol, Solution).Result.SelectMany(x => x.Locations)) {
                var invocationNode = compilation
                    .SyntaxTrees
                    .First(x => x.FilePath == location.Document.FilePath)
                    .GetSyntaxNode(location) as InvocationExpressionSyntax;
                var invocationCallsLocks = ExtractSolutionLocks(invocationNode, compilation);

                foreach (List<LockContext> invocationLocks in invocationCallsLocks) {
                    invocationLocks.InsertRange(0, lockContexts);
                    result.Add(invocationLocks);
                }
            }

            if (!result.Any()) {
                result.Add(lockContexts);
            }

            return result;
        }

        private AtomicAnalysis ProcessAnalysis(List<List<LockContext>> locks)
        {
            var lockInterleavings = new List<LockInterleaving>();

            for (int i = 0; i < locks.Count-1; i++)
            {
                for (int j = i+1; j < locks.Count; j++)
                {
                    var lockInterleaving = ProcessLocksInterleaving(locks[i], locks[j]);

                    if (lockInterleaving.HasErrors)
                    {
                        return new AtomicAnalysis
                        {
                            FirstDeadlockLock = lockInterleaving.FirstDeadlockLock,
                            SecondDeadlockLock = lockInterleaving.SecondDeadlockLock,
                            UnmatchedLock = lockInterleaving.UnmatchedLock
                        };
                    }

                    lockInterleavings.Add(lockInterleaving);
                }
            }

            /* TODO: not necessary var matchingLocks = lockInterleavings.Select(x => x.MatchingLocks).ToList();
            var protectionLocks = matchingLocks
                .Skip(1)
                .Aggregate(new HashSet<LockContext>(matchingLocks.First()),
                        (agg, x) =>
                        {
                            agg.IntersectWith(x);
                            return agg;
                        });

            if(protectionLocks.Count==0)*/
            return new AtomicAnalysis();
        }

        private LockInterleaving ProcessLocksInterleaving(List<LockContext> firstLocks, List<LockContext> secondLocks)
        {
            var result = new LockInterleaving();

            if(firstLocks.Count==0 && secondLocks.Count==0)
                return result;

            if (firstLocks.Count == 0) {
                result.UnmatchedLock = secondLocks[0];
                return result;
            }

            if (secondLocks.Count == 0)
            {
                result.UnmatchedLock = firstLocks[0];
                return result;
            }

            for (int i = 0; i < firstLocks.Count; i++)
            {
                var matchSecondLockIndex = secondLocks.FindIndex(x => x.LockInstance==firstLocks[i].LockInstance);

                if (matchSecondLockIndex > i)
                {
                    for (int j = 0; j < matchSecondLockIndex; j++)
                    {
                        var matchFirstLockIndex = firstLocks.FindIndex(x => x.LockInstance == secondLocks[j].LockInstance);

                        if (matchFirstLockIndex > i)
                        {
                            result.FirstDeadlockLock = firstLocks[i];
                            result.SecondDeadlockLock = secondLocks[j];
                            result.MatchingLocks.Clear();

                            return result;
                        }
                    }
                }

                if (matchSecondLockIndex >= 0)
                {
                    result.MatchingLocks.Add(firstLocks[i]);
                }
                else
                {
                    result.UnmatchedLock = firstLocks[i];
                }
            }

            if (result.MatchingLocks.Any())
            {
                result.UnmatchedLock = null;
            }

            return result;
        }

        private class LockInterleaving
        {
            public List<LockContext> MatchingLocks { get; set; }
            public LockContext FirstDeadlockLock { get; set; }
            public LockContext SecondDeadlockLock { get; set; }
            public LockContext UnmatchedLock { get; set; }
            public bool HasErrors => MatchingLocks.Count == 0 || FirstDeadlockLock != null || UnmatchedLock != null;

            public LockInterleaving()
            {
                MatchingLocks = new List<LockContext>();
            }
        }
    }
}