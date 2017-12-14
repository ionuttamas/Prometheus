namespace Prometheus.Engine.Analyzer.Atomic
{
    public class AtomicAnalysis : IAnalysis
    {
        public LockContext FirstDeadlockLock { get; set; }
        public LockContext SecondDeadlockLock { get; set; }
        public LockContext UnmatchedLock { get; set; }
    }
}