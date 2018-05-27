using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.Reachability.Model.Query;

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

        public bool IsStructurallyEquivalentTo(IReferenceQuery query)
        {
            if (query.GetType() != typeof(IndexArgumentQuery))
                return false;

            var indexQuery = query.As<IndexArgumentQuery>();

            if (Argument.Expression.Kind() == SyntaxKind.IdentifierName &&
                indexQuery.Argument.Expression.Kind() == SyntaxKind.IdentifierName)
                return true;




            throw new System.NotImplementedException();
        }
    }
}