using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Prometheus.Engine.Reachability.Model.Query;

namespace Prometheus.Engine.ExpressionMatcher {
    public interface IQueryMatcher : IDisposable
    {
        /// <summary>
        /// Checks whether two expressions are equivalent and returns the table under which the equivalence is constrained.
        /// It handles commutativity and other variations for clauses such as: "x + y ≡ c + d" or "x.Age > 30 && x.Balance > 100 ≡ y.Balance > 100 && y.Age > 30".
        /// </summary>
        bool AreEquivalent(IReferenceQuery first, IReferenceQuery second, out Dictionary<SyntaxNode, SyntaxNode> satisfiableTable);
    }
}
