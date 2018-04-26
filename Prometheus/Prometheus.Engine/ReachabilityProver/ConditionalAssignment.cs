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
        public SyntaxNode NodeReference { get; set; }
        public SyntaxToken TokenReference { get; set; }
        public Location AssignmentLocation { get; set; }

        public ConditionalAssignment()
        {
            Conditions = new HashSet<Condition>();
        }

        public void AddCondition(IfStatementSyntax ifStatement, bool isNegated)
        {
            Conditions.Add(new Condition(ifStatement, isNegated));
        }

        public ConditionalAssignment Clone()
        {
            return new ConditionalAssignment
            {
                TokenReference = TokenReference,
                AssignmentLocation = AssignmentLocation,
                Conditions = new HashSet<Condition>(Conditions.Select(x=>x))
            };
        }

        public override string ToString()
        {
            return string.Join(" AND ", Conditions.Select(x=>x));
        }
    }

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