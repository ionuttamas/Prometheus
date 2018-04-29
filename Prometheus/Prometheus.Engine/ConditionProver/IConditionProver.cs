using System;
using Prometheus.Engine.ReachabilityProver;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.ConditionProver {
    public delegate bool HaveCommonReference(Reference first, Reference second, out Reference commonReference);

    public interface IConditionProver: IDisposable
    {
        void Configure(HaveCommonReference reachabilityDelegate);

        /// <summary>
        /// Checks whether two conditions are reachable or not from the entry point.
        /// </summary>
        bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second);
    }
}
