namespace Prometheus.Common
{
    public static class ObjectExtensions
    {
        public static bool IsNull<T>(this T instance)
        {
            return instance.Equals(default(T));
        }
    }
}