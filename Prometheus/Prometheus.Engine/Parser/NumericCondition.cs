namespace Prometheus.Engine.Parser
{
    public class NumericCondition:ICondition
    {
        public Operator Operator { get; set; }
        public int Comparand { get; set; }
    }
}