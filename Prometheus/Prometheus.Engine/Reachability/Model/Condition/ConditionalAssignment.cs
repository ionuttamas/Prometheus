using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    /// <summary>
    /// Holds the conditional assignment for a given reference "left = right" expression.
    /// </summary>
    public class ConditionalAssignment {
        public HashSet<Condition> Conditions { get; set; }
        public Reference LeftReference { get; set; }
        public Reference RightReference { get; set; }

        public ConditionalAssignment()
        {
            Conditions = new HashSet<Condition>();
            RightReference = new Reference();
        }

        public void AddCondition(IfStatementSyntax ifStatement, bool isNegated)
        {
            Conditions.Add(new Condition(ifStatement, isNegated));
        }

        public ConditionalAssignment Clone()
        {
            return new ConditionalAssignment
            {
                RightReference = {Token = RightReference.Token, Node = RightReference.Node},
                LeftReference = LeftReference,
                Conditions = new HashSet<Condition>(Conditions.Select(x=>x))
            };
        }

        public override string ToString()
        {
            return string.Join(" AND ", Conditions.Select(x=>x));
        }
    }
}