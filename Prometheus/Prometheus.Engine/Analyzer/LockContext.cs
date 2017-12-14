using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.Analyzer
{
    public class LockContext {
        public string LockInstance { get; set; }
        public LockStatementSyntax LockStatementSyntax { get; set; }
        public MethodDeclarationSyntax Method { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is LockContext))
                return false;

            var lockContext = (LockContext) obj;

            return LockInstance == lockContext.LockInstance && Method.GetLocation() == lockContext.Method.GetLocation();
        }

        public override int GetHashCode() {
            return LockInstance.GetHashCode();
        }
    }
}