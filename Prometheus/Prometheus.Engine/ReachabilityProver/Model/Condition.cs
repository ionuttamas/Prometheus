using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReachabilityProver
{
    public class Condition
    {
        public IfStatementSyntax IfStatement { get; private set; }
        public bool IsNegated { get; private set; }

        public Condition(IfStatementSyntax ifStatement, bool isNegated)
        {
            IfStatement = ifStatement;
            IsNegated = isNegated;
        }

        public override bool Equals(object instance)
        {
            if (!(instance is Condition))
                return false;

            Condition condition = (Condition) instance;

            return IfStatement==condition.IfStatement && IsNegated==condition.IsNegated;
        }

        public override int GetHashCode()
        {
            return IfStatement.GetHashCode();
        }

        public override string ToString()
        {
            return IfStatement.Condition.ToString();
        }
    }
}