using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    /// <summary>
    /// Handles reference queries like "customers.First(x => x.IsActive && x.Age > 30)" for IEnumerable references.
    /// </summary>
    public class FirstExpressionQuery : IReferenceQuery
    {
        public SimpleLambdaExpressionSyntax Predicate { get; set; }

        public FirstExpressionQuery(SimpleLambdaExpressionSyntax predicate)
        {
            Predicate = predicate;
        }
    }
}