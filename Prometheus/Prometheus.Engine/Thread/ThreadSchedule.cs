using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.Thread
{
    public class ThreadSchedule
    {
        public List<ThreadPath> Paths { get; set; }

        public bool ContainsLocation(Solution solution, Location location)
        {
            var threadPath = GetThreadPath(solution, location);
            return threadPath != null && threadPath.Invocations.Any();
        }

        public InvocationPath GetThreadPath(Solution solution, Location location)
        {
            foreach (ThreadPath threadPath in Paths)
            {
                var invocations =  threadPath.GetInvocationChains(solution, location);

                if (!invocations.Any())
                    continue;

                return new InvocationPath
                {
                    RootMethod = threadPath.ThreadMethod,
                    Invocations = invocations.First()
                };
            }

            return new InvocationPath();
        }
    }
}