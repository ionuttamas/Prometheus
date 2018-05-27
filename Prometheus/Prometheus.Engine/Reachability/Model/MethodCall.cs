using Prometheus.Engine.Reachability.Model.Query;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    public class MethodCall
    {
        public MethodCall()
        {
            CallContext = new CallContext();
        }

        public MethodCall(CallContext callContext, IReferenceQuery query) {
            CallContext = callContext;
            Query = query;
        }

        /// <summary>
        /// The call context for a method call assignment.
        /// E.g. for "reference = instance.Get(a, b)" it holds all the call context information.
        /// </summary>
        public CallContext CallContext { get; set; }

        /// <summary>
        /// In the case of an assignment with a query applied on the reference like:
        ///  - "reference = customers[x]" or
        ///  - "reference = customers.First(x => predicate(x))" or
        ///  - "reference = customers.Where(x => predicate(x))"
        /// this contains the filters applied on the given instance ("customers" reference in this case).
        /// </summary>
        public IReferenceQuery Query { get; set; }
    }
}