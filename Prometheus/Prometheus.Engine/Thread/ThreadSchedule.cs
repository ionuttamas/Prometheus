using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.Thread
{
    public class ThreadSchedule
    {
        public List<ThreadPath> Paths { get; set; }

        //public bool BelongToSameThreadPath(Solution solution, Location pivot, Location candidate)
        //{
        //    // We should check if the candidate appears before the usage of the pivot within the same thread path
        //    // However, due to recursive calls
        //    var threadPath = GetInvocationPath(solution, location);
        //    return threadPath != null && threadPath.Invocations.Any();
        //}

        public bool ContainsLocation(Solution solution, Location location)
        {
            var threadPath = GetInvocationPath(solution, location);
            return threadPath != null && threadPath.Invocations.Any();
        }

        public InvocationPath GetInvocationPath(Solution solution, Location location)
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