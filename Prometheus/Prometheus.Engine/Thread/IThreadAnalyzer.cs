using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.Thread
{
    public interface IThreadAnalyzer
    {
        ThreadHierarchy GeThreadHierarchy(Project project);
    }
}