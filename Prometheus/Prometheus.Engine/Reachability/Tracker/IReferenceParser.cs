using Microsoft.CodeAnalysis;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Tracker
{
    public interface IReferenceParser
    {
        (Reference, IReferenceQuery) Parse(SyntaxNode node);
    }
}