using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReferenceProver
{
    /// <summary>
    /// Holds the conditional assignment for a given reference.
    /// </summary>
    public class ConditionalAssignment {
        //TODO: split to members
        public List<Condition> Conditions { get; set; }
        //TODO: more context here
        public SyntaxNode Reference { get; set; }
        //TODO: assigned identifier: more context here on location
        public Location AssignmentLocation { get; set; }

        public ConditionalAssignment()
        {
            Conditions = new List<Condition>();
        }

        public void AddCondition(IfStatementSyntax ifStatement, bool isNegated)
        {
            Conditions.Add(new Condition
            {
                IfStatement = ifStatement,
                IsNegated = isNegated
            });
        }

        public ConditionalAssignment Clone()
        {
            return new ConditionalAssignment
            {
                Reference = Reference,
                AssignmentLocation = AssignmentLocation,
                Conditions = Conditions.Select(x=>x).ToList()
            };
        }
    }

    public class Condition
    {
        public IfStatementSyntax IfStatement { get; set; }
        public bool IsNegated { get; set; }
    }
}