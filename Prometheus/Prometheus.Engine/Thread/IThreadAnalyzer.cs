using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.Thread
{
    public interface IThreadAnalyzer
    {
        ThreadSchedule GetThreadSchedule(Project entryProject);
    }
}