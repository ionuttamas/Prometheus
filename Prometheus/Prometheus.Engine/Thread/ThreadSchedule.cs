using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.Thread
{
    public class ThreadSchedule
    {
        public List<ThreadPath> Paths { get; set; }

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

            return null;
        }
    }
}