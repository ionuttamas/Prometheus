using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReferenceTrack
{
    /// <summary>
    /// Holds the conditional assignment for a given reference.
    /// </summary>
    public class ConditionalAssignment {
        //TODO: split to members
        public List<string> Conditions { get; set; }
        //TODO: more context here
        public SyntaxNode Reference { get; set; }
        //TODO: assigned identifier: more context here on location
        public Location ReferenceLocation { get; set; }

        public ConditionalAssignment()
        {
            Conditions = new List<string>();
        }
    }
}