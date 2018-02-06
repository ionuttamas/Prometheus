using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.ReferenceTrack
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

        public void AddCondition(string expression, Location location)
        {
            Conditions.Add(new Condition
            {
                Location = location,
                Expression = expression
            });
        }

        public ConditionalAssignment Clone()
        {
            return new ConditionalAssignment
            {
                Reference = Reference,
                AssignmentLocation = AssignmentLocation,
                Conditions = Conditions.Select(x=>x.Clone()).ToList()
            };
        }
    }

    public class Condition
    {
        public string Expression { get; set; }
        public Location Location { get; set; }

        public Condition Clone()
        {
            return new Condition
            {
                Location = Location,
                Expression = Expression
            };
        }
    }
}