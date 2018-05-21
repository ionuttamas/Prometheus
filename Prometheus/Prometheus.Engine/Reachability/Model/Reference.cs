using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    public class Reference
    {
        public Stack<MethodCall> MethodCalls { get; set; }
        public SyntaxNode Node { get; set; }
        public SyntaxToken Token { get; set; }

        public Reference() {
            MethodCalls = new Stack<MethodCall>();
        }

        public Reference(SyntaxNode node) : this()
        {
            Node = node;
        }

        public Reference(SyntaxToken token) : this() {
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

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            Reference instance = (Reference)obj;

            return instance.GetLocation() == GetLocation();
        }

        public override int GetHashCode() {
            return GetLocation().GetHashCode();
        }
    }
}