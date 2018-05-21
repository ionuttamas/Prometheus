using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    /// <summary>
    /// Handles reference queries like "customers[x]" for dictionaries, lists or other index-able references.
    /// </summary>
    public class IndexArgumentQuery : IReferenceQuery
    {
        public ArgumentSyntax Argument { get; set; }

        public IndexArgumentQuery(ArgumentSyntax argument)
        {
            Argument = argument;
        }

        public override string ToString() {
            return Argument.ToString();
        }
    }
}