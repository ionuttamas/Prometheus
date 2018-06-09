using System;
using System.Collections.Generic;
using System.Linq;
using Prometheus.Engine.ConditionProver;
using Prometheus.Engine.ExpressionMatcher;
using Prometheus.Engine.Reachability.Tracker;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Prover
{
    internal class ReachabilityProver : IDisposable
    {
        private readonly ReferenceTracker referenceTracker;
        private readonly IConditionProver conditionProver;
        private readonly IQueryMatcher queryMatcher;
        private readonly ReachabilityCache reachabilityCache;

        public ReachabilityProver(ReferenceTracker referenceTracker, IConditionProver conditionProver, IQueryMatcher queryMatcher)
        {
            this.referenceTracker = referenceTracker;
            this.conditionProver = conditionProver;
            this.queryMatcher = queryMatcher;
            reachabilityCache = new ReachabilityCache();
            conditionProver.Configure(HaveCommonReference);
            referenceTracker.Configure(HaveCommonReference);
        }

        /// <summary>
        /// Checks whether two identifiers can possibly have the same value within a thread schedule.
        /// TODO: this does not consider the possible conditional cases for assigning a variable within loops ("for", "while" loops).
        /// TODO: this does not consider assignments like: "order = person.Orders.First()" and "order = instance" where "instance" and "person.Orders.First()" can be the same
        /// </summary>
        public bool HaveCommonReference(Reference first, Reference second, out Reference commonNode)
        {
            var firstAssignment = new ConditionalAssignment
            {
                LeftReference = first,
                RightReference = first
            };

            var secondAssignment = new ConditionalAssignment
            {
                RightReference = second,
                LeftReference = second
            };

            return InternalHaveCommonReference(firstAssignment, secondAssignment, out commonNode);
        }

        public void Dispose()
        {
            conditionProver.Dispose();
        }

        private bool InternalHaveCommonReference(ConditionalAssignment first, ConditionalAssignment second, out Reference commonReference)
        {
            commonReference = null;

            if (reachabilityCache.Contains(first.LeftReference, second.LeftReference))
            {
                commonReference = reachabilityCache.GetFromCache(first.LeftReference, second.LeftReference);
                return commonReference != null && conditionProver.IsSatisfiable(first, second);
            }

            if (!conditionProver.IsSatisfiable(first, second))
            {
                reachabilityCache.AddToCache(first.LeftReference, second.LeftReference, null);
                return false;
            }

            if (AreEquivalent(first.RightReference, second.RightReference))
            {
                commonReference = first.RightReference;
                reachabilityCache.AddToCache(first.LeftReference, second.LeftReference, commonReference);
                return true;
            }

            var firstAssignments = referenceTracker.GetAssignments(first.RightReference.Node?.DescendantTokens().First() ?? first.RightReference.Token, first.RightReference.ReferenceContexts);
            var secondAssignments = referenceTracker.GetAssignments(second.RightReference.Node?.DescendantTokens().First() ?? second.RightReference.Token, second.RightReference.ReferenceContexts);

            if (!firstAssignments.Any() && !secondAssignments.Any())
            {
                reachabilityCache.AddToCache(first.LeftReference, second.LeftReference, null);
                return false;
            }

            foreach (ConditionalAssignment assignment in firstAssignments)
            {
                assignment.Conditions.UnionWith(first.Conditions);
            }

            foreach (ConditionalAssignment assignment in secondAssignments)
            {
                assignment.Conditions.UnionWith(second.Conditions);
            }

            foreach (var firstAssignment in firstAssignments)
            {
                if (InternalHaveCommonReference(firstAssignment, second, out commonReference))
                    return true;
            }

            foreach (var secondAssignment in secondAssignments)
            {
                if (InternalHaveCommonReference(first, secondAssignment, out commonReference))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// This checks whether two nodes are the same reference (the shared memory of two threads).
        /// This can be a class field/property used by both thread functions or parameters passed to threads that are the same
        /// TODO: currently this checks only for field equivalence
        /// </summary>
        private bool AreEquivalent(Reference first, Reference second)
        {
            if (first.ToString() == second.ToString() && first.GetLocation() == second.GetLocation())
                return true;

            /* Currently, we perform a strict equivalence testing for the reference query stack:
               e.g. for:
                    first.MethodCalls = { [a], Where(x => x.Member == capturedValue1), [b] }
                    second.MethodCalls = { [c], Where(x => capturedValue2 == x.Member), [d] }
               we enforce that a ≡ c, capturedValue1 ≡ capturedValue2, b ≡ d.
               In the future, we will support inclusion query support (one reference is included in the second).
             */
            if (first.ReferenceContexts.Count != second.ReferenceContexts.Count)
                return false;

            var firstMethodCalls = first.ReferenceContexts.ToList();
            var secondMethodCalls = second.ReferenceContexts.ToList();

            if (firstMethodCalls.Count == 0)
                return false;

            for (int i = 0; i < firstMethodCalls.Count; i++)
            {
                if (!queryMatcher.AreEquivalent(firstMethodCalls[i].Query, secondMethodCalls[i].Query, out var satisfiableTable))
                    continue;

                foreach (var variableMapping in satisfiableTable)
                {
                    var firstReference = new Reference(variableMapping.Key){ ReferenceContexts = new Stack<ReferenceContext>(new[] { firstMethodCalls[i] }) };
                    var secondReference = new Reference(variableMapping.Value){ ReferenceContexts = new Stack<ReferenceContext>(new[] { secondMethodCalls[i] }) };

                    if (!HaveCommonReference(firstReference, secondReference, out var _))
                        return false;
                }
            }

            return true;
        }
    }
}