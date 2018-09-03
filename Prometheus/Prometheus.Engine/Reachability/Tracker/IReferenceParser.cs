using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Engine.Reachability.Model.Query;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Tracker
{
    public interface IReferenceParser
    {
        bool IsBuiltInMethod(string methodName);
        (Reference, IReferenceQuery) Parse(SyntaxNode node);
        MethodDeclarationSyntax GetMethodBindings(InvocationExpressionSyntax invocationExpression,
                                                  ClassDeclarationSyntax classDeclaration, string methodName,
                                                  out Dictionary<ParameterSyntax, ArgumentSyntax> argumentsTable);
    }
}