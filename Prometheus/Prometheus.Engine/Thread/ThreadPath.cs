using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Thread {
    public class ThreadPath {
        public List<Location> Invocations { get; set; }
        public MethodDeclarationSyntax ThreadMethod { get; set; }
    }
}
