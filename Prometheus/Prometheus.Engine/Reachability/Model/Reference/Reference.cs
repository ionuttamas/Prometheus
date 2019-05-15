using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Prometheus.Common;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    public class Reference
    {
        public DEQueue<ReferenceContext> ReferenceContexts { get; set; }
        public SyntaxNode Node { get; set; }
        public SyntaxToken Token { get; set; }
        public bool Is3rdParty { get; set; }
        public bool IsPure { get; set; }

        public Reference() {
            ReferenceContexts = new DEQueue<ReferenceContext>();
        }

        public Reference(SyntaxNode node) : this()
        {
            Node = node;
        }

        public Reference(SyntaxToken token) : this() {
            Token = token;
        }

        public void PrependContext(ReferenceContext context) {
            ReferenceContexts.Prepend(context);
        }

        public void AppendContext(ReferenceContext context)
        {
            ReferenceContexts.Append(context);
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