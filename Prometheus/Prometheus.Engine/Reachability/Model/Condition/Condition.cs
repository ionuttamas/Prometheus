using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    public class Condition
    {
        public ExpressionSyntax TestExpression { get; }
        public bool IsNegated { get; set; }
        public HashSet<Condition> Conditions { get; }

        public Condition(ExpressionSyntax testExpression, bool isNegated)
        {
            Conditions = new HashSet<Condition>();
            TestExpression = testExpression;
            IsNegated = isNegated;
        }

        public Condition(IEnumerable<Condition> conditions, bool isNegated) {
            Conditions = new HashSet<Condition>(conditions);
            IsNegated = isNegated;
        }

        public override bool Equals(object instance)
        {
            if (!(instance is Condition))
                return false;

            Condition condition = (Condition) instance;

            return TestExpression==condition.TestExpression && IsNegated==condition.IsNegated;
        }

        public override int GetHashCode()
        {
            return TestExpression!=null? TestExpression.GetHashCode():Conditions.GetHashCode();
        }

        public override string ToString()
        {
            return $"{(IsNegated? "NOT ( " : string.Empty)}{TestExpression?.ToString() ?? string.Join(" AND ", Conditions)}{(IsNegated ? " )" : string.Empty)}";
        }
    }
}