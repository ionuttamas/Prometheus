using System;
using System.Collections.Generic;
using System.Linq;
using Prometheus.Engine.ConditionProver;
using Prometheus.Engine.Reachability.Tracker;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Prover
{
    internal class ReachabilityProver : IDisposable
    {
        private readonly ReferenceTracker referenceTracker;
        private readonly IConditionProver conditionProver;
        private readonly ReachabilityCache reachabilityCache;

        public ReachabilityProver(ReferenceTracker referenceTracker, IConditionProver conditionProver)
        {
            this.referenceTracker = referenceTracker;
            this.conditionProver = conditionProver;
            reachabilityCache = new ReachabilityCache();
            conditionProver.Configure(HaveCommonReference);
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

        public void Dispose() {
            conditionProver.Dispose();
        }

        private bool InternalHaveCommonReference(ConditionalAssignment first, ConditionalAssignment second, out Reference commonReference)
        {
            commonReference = null;

            if (reachabilityCache.Contains(first.LeftReference, second.LeftReference))
            {
                commonReference = reachabilityCache.GetFromCache(first.LeftReference, second.LeftReference);
                return commonReference!=null && conditionProver.IsSatisfiable(first, second);
            }

            if (!conditionProver.IsSatisfiable(first, second))
            {
                reachabilityCache.AddToCache(first.LeftReference, second.LeftReference, null);
                return false;
            }

            if (AreEquivalent(first, second))
            {
                commonReference = first.RightReference;
                reachabilityCache.AddToCache(first.LeftReference, second.LeftReference, commonReference);
                return true;
            }

            List<ConditionalAssignment> firstAssignments = referenceTracker.GetAssignments(first.RightReference.Node?.DescendantTokens().First() ?? first.RightReference.Token);
            List<ConditionalAssignment> secondAssignments = referenceTracker.GetAssignments(second.RightReference.Node?.DescendantTokens().First() ?? second.RightReference.Token);

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
        private static bool AreEquivalent(ConditionalAssignment first, ConditionalAssignment second)
        {
            var firstReferenceName = first.RightReference.ToString();
            var secondReferenceName = second.RightReference.ToString();
            var firstLocation = first.RightReference.GetLocation();
            var secondLocation = second.RightReference.GetLocation();

            if (firstReferenceName != secondReferenceName)
                return false;

            return firstLocation == secondLocation;
        }

    }
}