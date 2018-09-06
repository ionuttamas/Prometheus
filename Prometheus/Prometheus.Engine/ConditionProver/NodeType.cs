using System;
using Microsoft.CodeAnalysis;
using Microsoft.Z3;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.ConditionProver
{
    //TODO: see if this can't be reduced by Reference & Expression only
    internal class NodeType {
        public SyntaxNode Node { get; set; }
        public Expr Expression { get; set; }
        public Type Type { get; set; }
        public bool IsExternal { get; set; }
        public Reference Reference { get; set; }
    }
}