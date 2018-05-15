using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    /// <summary>
    /// Handles reference queries like "customers.Where(x => x.IsActive && x.Age > 30)" for IEnumerable references.
    /// </summary>
    public class WhereExpressionQuery : IReferenceQuery
    {
        public SimpleLambdaExpressionSyntax Predicate { get; set; }

        public WhereExpressionQuery(SimpleLambdaExpressionSyntax predicate) {
            Predicate = predicate;
        }
    }
}