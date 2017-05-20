using System.Collections.Generic;

namespace Prometheus.Engine.Thread
{
    public class ThreadSchedule
    {
        public List<ThreadPath> Paths { get; private set; }

        public ThreadSchedule() {
            Paths = new List<ThreadPath>();
        }
    }
}