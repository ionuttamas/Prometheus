using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.ReachabilityProver
{
    internal class ReachabilityCache
    {
        private readonly Dictionary<Location, Dictionary<Location, object>> reachabilityCache;

        public ReachabilityCache()
        {
            reachabilityCache = new Dictionary<Location, Dictionary<Location, object>>();
        }

        public void AddToCache(Location first, Location second, object commonValue)
        {
            if (reachabilityCache.ContainsKey(first))
            {
                reachabilityCache[first][second] = commonValue;
                return;
            }

            if (reachabilityCache.ContainsKey(second))
            {
                reachabilityCache[second][first] = commonValue;
                return;
            }

            reachabilityCache[first] = new Dictionary<Location, object> { [second] = commonValue };
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

        public object GetFromCache(Location first, Location second)
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
