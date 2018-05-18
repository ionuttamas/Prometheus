using System;
using System.Collections.Generic;
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

        public bool Contains(Reference first, Reference second)
        {
            if (reachabilityCache.ContainsKey(first) && reachabilityCache[first].ContainsKey(second))
            {
                return true;
            }

            if (reachabilityCache.ContainsKey(second) && reachabilityCache[second].ContainsKey(first))
            {
                return true;
            }

            return false;
        }

        public Reference GetFromCache(Reference first, Reference second)
        {
            if (reachabilityCache.ContainsKey(first) && reachabilityCache[first].ContainsKey(second))
            {
                return reachabilityCache[first][second];
            }

            if (reachabilityCache.ContainsKey(second) && reachabilityCache[second].ContainsKey(first))
            {
                return reachabilityCache[second][first];
            }

            throw new InvalidOperationException($"No value was cached for [{first}] and [{second}]");
        }
    }
}
