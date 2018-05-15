using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.ReachabilityProver
{
    internal class ReachabilityCache
    {
        private readonly Dictionary<Location, Dictionary<Location, Reference>> reachabilityCache;

        public ReachabilityCache()
        {
            reachabilityCache = new Dictionary<Location, Dictionary<Location, Reference>>();
        }

        public void AddToCache(Location first, Location second, Reference commonReference)
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

            reachabilityCache[first] = new Dictionary<Location, Reference> { [second] = commonReference };
        }

        public bool Contains(Location first, Location second)
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

        public Reference GetFromCache(Location first, Location second)
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
