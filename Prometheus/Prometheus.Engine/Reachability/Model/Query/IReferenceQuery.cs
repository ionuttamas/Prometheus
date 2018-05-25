using Microsoft.Z3;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    public interface IReferenceQuery
    {
        /// <summary>
        /// Checks whether it is structurally equivalent to another query.
        /// It handles commutativity and other variations for clauses such as: "x + y ≡ c + d" or "x.Age > 30 && x.Balance > 100 ≡ y.Balance > 100 && y.Age > 30"
        /// </summary>
        bool IsStructurallyEquivalentTo(IReferenceQuery query, Context context);
    }
}