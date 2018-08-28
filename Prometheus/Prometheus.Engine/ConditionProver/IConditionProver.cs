using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.ConditionProver {
    public delegate bool HaveCommonReference(Reference first, Reference second, out Reference commonReference);
    public delegate List<ConditionalAssignment> GetConditionalAssignments(SyntaxToken identifier, Stack<ReferenceContext> referenceContexts = null);

    public interface IConditionProver: IDisposable
    {
        /// <summary>
        /// Checks whether two conditions are reachable or not from the entry point.
        /// </summary>
        bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second);
    }
}
