using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Prover
{
    internal class ReachabilityCache
    {
        private readonly Dictionary<Reference, Dictionary<Reference, Reference>> reachabilityCache;

        public ReachabilityCache()
        {
            reachabilityCache = new Dictionary<Reference, Dictionary<Reference, Reference>>();
        }

        public void AddToCache(Reference first, Reference second, Reference commonReference)
        {
            if(first.Node!=null && first.Node.Kind()==SyntaxKind.InvocationExpression)
                return;

            if (second.Node != null && second.Node.Kind() == SyntaxKind.InvocationExpression)
                return;

            if (reachabilityCache.ContainsKey(first))
            {
                reachabilityCache[first][second] = commonReference;
                return;
            }

            if (reachabilityCache.ContainsKey(second))
            {
                reachabilityCache[second][first] = commonReference;
                return;
            }

            reachabilityCache[first] = new Dictionary<Reference, Reference> { [second] = commonReference };
        }

        public bool TryGet(Reference first, Reference second, out Reference commonReference) {
            if (reachabilityCache.ContainsKey(first) && reachabilityCache[first].ContainsKey(second)) {
                commonReference = reachabilityCache[first][second];
                return true;
            }

            if (reachabilityCache.ContainsKey(second) && reachabilityCache[second].ContainsKey(first)) {
                commonReference = reachabilityCache[second][first];
                return true;
            }

            commonReference = null;
            return false;
        }
    }
}
