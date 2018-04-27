using System;

namespace Prometheus.Engine.Types
{
    public interface ITypeService
    {
        Type GetType(string name);
    }
}
