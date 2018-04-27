using System;
using Prometheus.Engine.ReachabilityProver;

namespace Prometheus.Engine.ConditionProver {
    public interface IConditionProver: IDisposable {
        /// <summary>
        /// Checks whether two conditions are reachable or not from the entry point.
        /// </summary>
        bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second);
    }
}
