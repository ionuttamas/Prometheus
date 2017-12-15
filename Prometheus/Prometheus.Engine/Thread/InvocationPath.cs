using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Thread
{
    public class InvocationPath
    {
        public MethodDeclarationSyntax RootMethod { get; set; }
        public List<Location> Invocations { get; set; }
    }
}