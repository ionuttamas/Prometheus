using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ConditionProver
{
    internal class InvocationType
    {
        public bool IsStatic => StaticType != null;
        public bool IsLocalReference => InstanceType == null;
        public Type StaticType { get; set; }
        public Type InstanceType { get; set; }
        public IdentifierNameSyntax Instance { get; set; }
        public Type Type => StaticType ?? InstanceType;
    }
}