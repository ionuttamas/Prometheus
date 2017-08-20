using System;

namespace Prometheus.Engine.Invariant
{
    public class AtomicInvariant : IInvariant
    {
        public Type Type { get; set; }
        public string Member { get; set; }
    }
}