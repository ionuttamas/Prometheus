using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.Thread
{
    public interface IThreadAnalyzer
    {
        ThreadHierarchy GetThreadHierarchy(Project project);
    }
}