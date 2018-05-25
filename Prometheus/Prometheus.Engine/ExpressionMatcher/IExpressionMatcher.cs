using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.ExpressionMatcher {
    public interface IQueryMatcher : IDisposable
    {
        /// <summary>
        /// Checks whether two expressions are equivalent and returns the table under which the equivalence is constrained.
        /// </summary>
        Dictionary<SyntaxNode, SyntaxNode> AreEquivalent(IReferenceQuery first, IReferenceQuery second);
    }
}
