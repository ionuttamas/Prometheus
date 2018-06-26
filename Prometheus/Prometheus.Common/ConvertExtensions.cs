namespace Prometheus.Common
{
    public static class ConvertExtensions
    {
        public static T As<T>(this object instance)
        {
            return (T) instance;
        }
    }
}