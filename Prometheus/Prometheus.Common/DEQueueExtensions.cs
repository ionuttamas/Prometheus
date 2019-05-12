namespace Prometheus.Common
{
    public static class DEQueueExtensions
    {
        public static bool IsNullOrEmpty<T>(this DEQueue<T> dequeue) {
            return dequeue == null || dequeue.IsEmpty;
        }
    }
}