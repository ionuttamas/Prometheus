using System;
using Microsoft.CodeAnalysis;
using Microsoft.Z3;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.ConditionProver
{
    //TODO: see if this can't be reduced by Reference & Expression only
    internal class NodeType {
        /// <summary>
        /// Specifies if the node is 3rd party type (3rd party code outside of the solution under test).
        /// </summary>
        public bool Is3rdParty { get; set; }
        public Reference Reference { get; set; }
        public SyntaxNode Node { get; set; }
        public Expr Expression { get; set; }
        public Type Type { get; set; }
    }
}