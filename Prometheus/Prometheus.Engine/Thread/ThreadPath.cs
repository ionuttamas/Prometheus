using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Thread {
    public class ThreadPath {
        public List<InvocationExpressionSyntax> Invocations { get; private set; }
        public MethodDeclarationSyntax ThreadMethod { get; set; }

        public ThreadPath()
        {
            Invocations = new List<InvocationExpressionSyntax>();
        }
    }
}
