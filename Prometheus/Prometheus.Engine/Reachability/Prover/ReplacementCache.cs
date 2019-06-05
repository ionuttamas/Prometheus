using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Prover
{
    internal class ReplacementCache
    {
        private readonly Dictionary<Reference, Reference> replacementCache;

        public ReplacementCache() {
            replacementCache = new Dictionary<Reference, Reference>();
        }

        public void AddToCache(Reference reference, Reference uniqueReference)
        {
            if (reference.Node != null && reference.Node.Kind() == SyntaxKind.InvocationExpression)
                return;

            if (uniqueReference.Node != null && uniqueReference.Node.Kind() == SyntaxKind.InvocationExpression)
                return;

            replacementCache[reference] = uniqueReference;
        }

        public bool TryGet(Reference reference, out Reference uniqueReference)
        {
            return replacementCache.TryGetValue(reference, out uniqueReference);
        }
    }
}