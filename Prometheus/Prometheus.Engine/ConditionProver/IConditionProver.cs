﻿using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.ConditionProver {
    internal delegate bool HaveCommonReference(Reference first, Reference second, out Reference commonReference);
    internal delegate List<ConditionalAssignment> GetConditionalAssignments(Reference reference);
    internal delegate Expr ParseBooleanMethod(MethodDeclarationSyntax methodDeclaration, out Dictionary<string, NodeType> processedNodes);

    public interface IConditionProver: IDisposable
    {
        /// <summary>
        /// Checks whether two conditions are reachable or not from the entry point.
        /// </summary>
        bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second);
    }
}
