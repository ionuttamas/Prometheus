using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReachabilityProver
{
    /// <summary>
    /// Holds the conditional assignment for a given reference.
    /// </summary>
    public class ConditionalAssignment {
        //TODO: split to members
        public HashSet<Condition> Conditions { get; set; }
        public Reference Reference { get; set; }
        public Location AssignmentLocation { get; set; }

        public ConditionalAssignment()
        {
            Conditions = new HashSet<Condition>();
            Reference = new Reference();
        }

        public void AddCondition(IfStatementSyntax ifStatement, bool isNegated)
        {
            Conditions.Add(new Condition(ifStatement, isNegated));
        }

        public ConditionalAssignment Clone()
        {
            return new ConditionalAssignment
            {
                Reference = {Token = Reference.Token, Node = Reference.Node},
                AssignmentLocation = AssignmentLocation,
                Conditions = new HashSet<Condition>(Conditions.Select(x=>x))
            };
        }

        public override string ToString()
        {
            return string.Join(" AND ", Conditions.Select(x=>x));
        }
    }
}