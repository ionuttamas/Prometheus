using Microsoft.CodeAnalysis;
using Prometheus.Engine.Models;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine.Analyzer
{
    public interface IAnalyzer
    {
        Solution Solution { get; set; }
        ThreadSchedule ThreadSchedule { get; set; }
        ModelStateConfiguration ModelStateConfiguration { get; set; }
        IAnalysis Analyze(IInvariant invariant);
    }
}
