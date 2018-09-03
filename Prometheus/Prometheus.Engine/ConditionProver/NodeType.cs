using System;
using Microsoft.CodeAnalysis;
using Microsoft.Z3;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.ConditionProver
{
    internal class NodeType {
        public SyntaxNode Node { get; set; }
        public Expr Expression { get; set; }
        public Type Type { get; set; }
        public bool IsExternal { get; set; }
        public Reference ExternalReference { get; set; }

        public NodeType()
        {
            ExternalReference = new Reference();
        }
    }
}