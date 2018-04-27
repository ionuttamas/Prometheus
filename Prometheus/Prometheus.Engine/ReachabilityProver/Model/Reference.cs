using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.ReachabilityProver
{
    public class Reference
    {
        public SyntaxNode Node { get; set; }
        public SyntaxToken Token { get; set; }

        public Location GetLocation()
        {
            return Node != null ? Node.GetLocation() : Token.GetLocation();
        }

        public override string ToString()
        {
            return Node != null ? Node.ToString() : Token.ToString();
        }
    }
}