using Prometheus.Engine.Analyzer;
using Prometheus.Engine.Model;

namespace Prometheus.Engine.Atomic
{
    public class AtomicAnalysis : IAnalysis
    {
        public LockContext FirstDeadlockLock { get; set; }
        public LockContext SecondDeadlockLock { get; set; }
        public LockContext UnmatchedLock { get; set; }
    }
}