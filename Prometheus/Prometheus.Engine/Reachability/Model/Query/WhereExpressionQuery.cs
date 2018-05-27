using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Reachability.Model.Query
{
    /// <summary>
    /// Handles reference queries like "customers.Where(x => x.IsActive && x.Age > 30)" for IEnumerable references.
    /// </summary>
    public class WhereExpressionQuery : PredicateExpressionQuery {

        public WhereExpressionQuery(SimpleLambdaExpressionSyntax predicate):base(predicate) {
        }
    }
}