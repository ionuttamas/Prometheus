using Microsoft.CodeAnalysis;
using Prometheus.Engine.Reachability.Model.Query;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Tracker
{
    public interface IReferenceParser
    {
        bool IsBuildInMethod(string methodName);
        (Reference, IReferenceQuery) Parse(SyntaxNode node);
    }
}