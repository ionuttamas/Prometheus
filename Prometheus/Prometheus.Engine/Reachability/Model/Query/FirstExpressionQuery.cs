using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Reachability.Model.Query
{
    /// <summary>
    /// Handles reference queries like "customers.First(x => x.IsActive && x.Age > 30)" for IEnumerable references.
    /// </summary>
    public class FirstExpressionQuery : PredicateExpressionQuery
    {
        public FirstExpressionQuery(SimpleLambdaExpressionSyntax predicate) : base(predicate)
        {
        }
    }
}