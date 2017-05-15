using System.Collections.Generic;

namespace Prometheus.Engine.Thread
{
    public class ThreadHierarchy
    {
        public List<ThreadPath> Paths { get; private set; }

        public ThreadHierarchy() {
            Paths = new List<ThreadPath>();
        }
    }
}