using Prometheus.Engine.Invariant;

namespace Prometheus.Engine.Analyzer
{
    public interface IAnalyzer
    {
        void AddConfiguration(ModelStateConfiguration configuration);
        IAnalysis Analyze(IInvariant invariant);
    }
}
