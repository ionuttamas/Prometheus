namespace Prometheus.Engine.Verifier
{
    public static class Args
    {
        public static T Any<T>()
        {
            return default(T);
        }
    }
}