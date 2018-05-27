using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Reachability.Model.Query
{
    public abstract class PredicateExpressionQuery : IReferenceQuery {
        public SimpleLambdaExpressionSyntax Predicate { get; set; }

        protected PredicateExpressionQuery(SimpleLambdaExpressionSyntax predicate) {
            Predicate = predicate;
        }

        public override string ToString() {
            return Predicate.ToString();
        }
    }
}