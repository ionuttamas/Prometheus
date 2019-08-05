using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.ConditionProver;
using Prometheus.Engine.ExpressionMatcher;
using Prometheus.Engine.ExpressionMatcher.Query;
using Prometheus.Engine.Reachability.Tracker;
using Prometheus.Engine.ReachabilityProver.Model;
using Prometheus.Engine.Types;

namespace Prometheus.Engine.Reachability.Prover
{
    internal class ReachabilityProver : IDisposable
    {
        private readonly ReferenceTracker referenceTracker;
        private readonly IConditionProver conditionProver;
        private readonly IQueryMatcher queryMatcher;
        private readonly ITypeService typeService;
        private readonly ReachabilityCache reachabilityCache;
        private const string NULL_MARKER = "null";

        public ReachabilityProver(ReferenceTracker referenceTracker, IConditionProver conditionProver, IQueryMatcher queryMatcher, ITypeService typeService)
        {
            this.referenceTracker = referenceTracker;
            this.conditionProver = conditionProver;
            this.queryMatcher = queryMatcher;
            this.typeService = typeService;
            reachabilityCache = new ReachabilityCache();
            referenceTracker.Configure(HaveCommonReference);
        }

        /// <summary>
        /// Checks whether two identifiers can possibly have the same value within a thread schedule.
        /// TODO: this does not consider the possible conditional cases for assigning a variable within loops ("for", "while" loops).
        /// TODO: this does not consider assignments like: "order = person.Orders.First()" and "order = instance" where "instance" and "person.Orders.First()" can be the same
        /// </summary>
        public bool HaveCommonReference(Reference first, Reference second, out Reference commonNode)
        {
            commonNode = null;

            if (first.Node == null && first.Token == default(SyntaxToken))
                return false;

            if (second.Node == null && second.Token == default(SyntaxToken))
                return false;

            if (!CheckTypes(first, second))
            {
                commonNode = null;
                reachabilityCache.AddToCache(first, second, null);
                return false;
            }

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
            if (reachabilityCache.TryGet(first.LeftReference, second.LeftReference, out commonReference))
            {
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

            var firstAssignments = first.LeftReference.ToString() == NULL_MARKER ?
                                    new List<ConditionalAssignment>() :
                                    referenceTracker.GetAssignments(first.RightReference);
            var secondAssignments = second.LeftReference.ToString() == NULL_MARKER ?
                                    new List<ConditionalAssignment>() :
                                    referenceTracker.GetAssignments(second.RightReference);

            if (!firstAssignments.Any() && !secondAssignments.Any())
            {
                reachabilityCache.AddToCache(first.LeftReference, second.LeftReference, null);
                reachabilityCache.AddToCache(first.RightReference, second.RightReference, null);
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

            reachabilityCache.AddToCache(first.LeftReference, second.LeftReference, null);
            reachabilityCache.AddToCache(first.RightReference, second.RightReference, null);
            return false;
        }

        private bool CheckTypes(Reference first, Reference second) {
            var firstContainer = first.Node != null ? typeService.GetTypeContainer(first.Node) : typeService.GetTypeContainer(first.Token);
            var secondContainer = second.Node != null ? typeService.GetTypeContainer(second.Node) : typeService.GetTypeContainer(second.Token);

            return typeService.AreParentChild(firstContainer, secondContainer);
        }

        /// <summary>
        /// This checks whether two nodes are the same reference (the shared memory of two threads).
        /// This can be a class field/property used by both thread functions or parameters passed to threads that are the same
        /// TODO: currently this checks only for field equivalence
        /// </summary>
        private bool AreEquivalent(Reference first, Reference second)
        {
            if (first.Node != null && second.Node != null &&
                first.Node.Kind() == second.Node.Kind() &&
                first.Node is LiteralExpressionSyntax)
            {
                return first.ToString() == second.ToString();
            }

            //TODO: in the case of customer[0] ≡ customers[x] from different locations => it fails to match 0≡x;
            if (first.ToString() != second.ToString() || first.GetLocation() != second.GetLocation())
                return false;

            /* Currently, we perform a strict equivalence testing for the reference query stack:
               e.g. for:
                    first.MethodCalls = { [a], Where(x => x.Member == capturedValue1), [b] }
                    second.MethodCalls = { [c], Where(x => capturedValue2 == x.Member), [d] }
               we enforce that a ≡ c, capturedValue1 ≡ capturedValue2, b ≡ d.
               In the future, we will support inclusion query support (one reference is included in the second).
             */
            return MatchCallContexts(first, second);
        }

        //TODO: redesign: incorrect as it doesn't capture many cases
        private bool MatchCallContexts(Reference first, Reference second)
        {
            var firstMethodContexts = first.ReferenceContexts.ToList();
            var secondMethodContexts = second.ReferenceContexts.ToList();

            if (firstMethodContexts.Count == 0 && secondMethodContexts.Count == 0)
                return true;

            //TODO: what if firstMethodContexts.Count != secondMethodContexts.Count?
            for (int i = 0; i < firstMethodContexts.Count; i++)
            {
                var lambdaEquivalence = AreLambdaContextsEquivalent(firstMethodContexts[i], secondMethodContexts[i]);

                if (lambdaEquivalence!=null)
                    return lambdaEquivalence.Value;

                var functionEquivalence = AreFunctionContextsEquivalent(firstMethodContexts[i], secondMethodContexts[i]);

                if (functionEquivalence != null)
                    return functionEquivalence.Value;
            }

            return true;
        }

        private bool? AreLambdaContextsEquivalent(ReferenceContext firstMethodContext, ReferenceContext secondMethodContext)
        {
            if ((firstMethodContext.Query == null && secondMethodContext.Query != null) ||
                (firstMethodContext.Query != null && secondMethodContext.Query == null) ||
                (firstMethodContext.Query == null && secondMethodContext.Query == null))
                return null;

            if (!queryMatcher.AreEquivalent(firstMethodContext.Query, secondMethodContext.Query, out var satisfiableTable))
                return null;

            foreach (var variableMapping in satisfiableTable) {
                var firstReference = new Reference(variableMapping.Key) { ReferenceContexts = new DEQueue<ReferenceContext>(new[] { firstMethodContext }) };
                var secondReference = new Reference(variableMapping.Value) { ReferenceContexts = new DEQueue<ReferenceContext>(new[] { secondMethodContext }) };

                if (!HaveCommonReference(firstReference, secondReference, out var _))
                    return false;
            }

            return null;
        }

        private bool? AreFunctionContextsEquivalent(ReferenceContext firstMethodContext, ReferenceContext secondMethodContext) {
            if (firstMethodContext.Query != null || secondMethodContext.Query != null)
                return null;

            //TODO: this only checks if for {a.Foo(x)} and {b.Foo(y)}, a≡b, Foo≡Foo, x≡y; this is incomplete: {a.Foo(m,n)} and {b.Bar(y)}
            var firstReference = new Reference(firstMethodContext.CallContext.InstanceNode);
            var secondReference = new Reference(secondMethodContext.CallContext.InstanceNode);

            if (!HaveCommonReference(firstReference, secondReference, out var _))
                return false;

            var firstMethod = firstMethodContext.CallContext.InvocationExpression.GetMethodName();
            var secondMethod = secondMethodContext.CallContext.InvocationExpression.GetMethodName();

            if (firstMethod != secondMethod)
                return false;

            var firstArguments = firstMethodContext.CallContext.ArgumentsTable;
            var secondArguments = secondMethodContext.CallContext.ArgumentsTable;

            if (firstArguments.Count != secondArguments.Count)
                return false;

            foreach (var entry  in firstArguments)
            {
                var firstArgumentReference = new Reference(entry.Value);
                var secondArgumentReference = new Reference(secondArguments.First(x => x.Key.ToString() == entry.Key.ToString()).Value);

                if (!HaveCommonReference(firstArgumentReference, secondArgumentReference, out var _))
                    return false;
            }

            return null;
        }
    }
}