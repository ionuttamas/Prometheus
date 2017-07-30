using System;

namespace Prometheus.Engine.Parser
{
    public class TypeConstraint
    {
        public Type Type { get; set; }
        public string PropertyChain { get; set; }
        public ICondition Condition { get; set; }
    }
}