namespace Prometheus.Engine.Parser
{
    public class NumericCondition:ICondition
    {
        public NumericOperator Operator { get; set; }
        public int Comparand { get; set; }
    }
}