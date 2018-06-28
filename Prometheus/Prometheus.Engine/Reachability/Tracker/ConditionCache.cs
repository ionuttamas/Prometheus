using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Tracker
{
    internal class ConditionCache
    {
        private readonly Dictionary<SyntaxNode, HashSet<Condition>> conditionCache;

        public ConditionCache()
        {
            conditionCache = new Dictionary<SyntaxNode, HashSet<Condition>>();
        }

        public void AddToCache(SyntaxNode node, HashSet<Condition> conditions)
        {
            if (conditionCache.ContainsKey(node))
            {
                foreach (var condition in conditions)
                {
                    conditionCache[node].Add(condition);
                }
            }
            else
            {
                conditionCache[node] = conditions;
            }
         }

        public void AddToCache(SyntaxNode node, Condition condition) {
            if (!conditionCache.ContainsKey(node))
            {
                conditionCache[node] = new HashSet<Condition>();
            }

            conditionCache[node].Add(condition);
        }

        public bool TryGet(SyntaxNode node, out HashSet<Condition> conditions)
        {
            if (conditionCache.ContainsKey(node))
            {
                conditions = conditionCache[node];
                return true;
            }

            conditions = null;
            return false;
        }
    }
}
