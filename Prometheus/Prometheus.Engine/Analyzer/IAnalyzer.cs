using Prometheus.Engine.Invariant;

namespace Prometheus.Engine.Analyzer
{
    public interface IAnalyzer
    {
        IAnalysis Analyze(IInvariant invariant);
    }
}
