using Microsoft.CodeAnalysis;

namespace Prometheus.Engine.ReachabilityProver
{
    public class Reference
    {
        public SyntaxNode Node { get; set; }
        public SyntaxToken Token { get; set; }

        public Reference() {
        }

        public Reference(SyntaxNode node)
        {
            Node = node;
        }

        public Reference(SyntaxToken token) {
            Token = token;
        }

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