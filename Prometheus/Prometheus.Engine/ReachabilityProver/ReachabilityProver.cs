using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.ConditionProver;
using Prometheus.Engine.Types;
using TypeInfo = System.Reflection.TypeInfo;

namespace Prometheus.Engine.ReachabilityProver
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
        }

        /// <summary>
        /// Checks whether two identifiers can possibly have the same value within a thread schedule.
        /// TODO: this does not consider the possible conditional cases for assigning a variable within loops ("for", "while" loops).
        /// TODO: this does not consider assignments like: "order = person.Orders.First()" and "order = instance" where "instance" and "person.Orders.First()" can be the same
        /// </summary>
        public bool HaveCommonValue(Reference first, Reference second, out Reference commonNode)
        {
            var firstAssignment = new ConditionalAssignment
            {
                AssignmentLocation = first.GetLocation(),
                Reference = first
            };

            var secondAssignment = new ConditionalAssignment
            {
                Reference = second,
                AssignmentLocation = second.GetLocation()
            };
            return HaveCommonValueInternal(firstAssignment, secondAssignment, out commonNode);
        }

        public void Dispose() {
            conditionProver.Dispose();
        }

        private bool HaveCommonValueInternal(ConditionalAssignment first, ConditionalAssignment second, out Reference commonReference)
        {
            commonReference = null;

            if (reachabilityCache.Contains(first.AssignmentLocation, second.AssignmentLocation))
            {
                commonReference = reachabilityCache.GetFromCache(first.AssignmentLocation, second.AssignmentLocation);
                return commonReference!=null && conditionProver.IsSatisfiable(first, second);
            }

            if (!conditionProver.IsSatisfiable(first, second))
            {
                reachabilityCache.AddToCache(first.AssignmentLocation, second.AssignmentLocation, null);
                return false;
            }

            if (AreEquivalent(first, second))
            {
                commonReference = first.Reference;
                reachabilityCache.AddToCache(first.AssignmentLocation, second.AssignmentLocation, commonReference);
                return true;
            }

            List<ConditionalAssignment> firstAssignments = referenceTracker.GetAssignments(first.Reference.Node?.DescendantTokens().First() ?? first.Reference.Token);
            List<ConditionalAssignment> secondAssignments = referenceTracker.GetAssignments(second.Reference.Node?.DescendantTokens().First() ?? second.Reference.Token);

            if (!firstAssignments.Any() && !secondAssignments.Any())
            {
                reachabilityCache.AddToCache(first.AssignmentLocation, second.AssignmentLocation, null);
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
                if (HaveCommonValueInternal(firstAssignment, second, out commonReference))
                    return true;
            }

            foreach (var secondAssignment in secondAssignments)
            {
                if (HaveCommonValueInternal(first, secondAssignment, out commonReference))
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
            var firstReferenceName = first.Reference.Node?.ToString() ?? first.Reference.Token.ToString();
            var secondReferenceName = second.Reference.Node?.ToString() ?? second.Reference.Token.ToString();
            var firstLocation = first.Reference.Node?.GetLocation() ?? first.Reference.Token.GetLocation();
            var secondLocation = second.Reference.Node?.GetLocation() ?? second.Reference.Token.GetLocation();

            if (firstReferenceName != secondReferenceName)
                return false;

            return firstLocation == secondLocation;
        }

    }
}