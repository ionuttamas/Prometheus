using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.Thread
{
    public class ThreadAnalyzer : IThreadAnalyzer
    {
        public ThreadHierarchy GeThreadHierarchy(Project project)
        {
            // Currently, we support only ConsoleApplication projects
            Compilation compilation;
            if (!project.TryGetCompilation(out compilation))
                throw new NotSupportedException("Could not get compilation for this project");


            IMethodSymbol entryMethod = compilation.GetEntryPoint(CancellationToken.None);
            entryMethod.
        }

        private ThreadHierarchy
    }
}