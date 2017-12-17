using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine.ReferenceTrack {
    internal class ReferenceTracker
    {
        private readonly Solution solution;
        private readonly ThreadSchedule threadSchedule;

        public ReferenceTracker(Solution solution, ThreadSchedule threadSchedule)
        {
            this.solution = solution;
        }

        /// <summary>
        /// Checks whether two identifiers can possibly have the same value.
        /// </summary>
        public bool HaveCommonValue(IdentifierNameSyntax first, IdentifierNameSyntax second)
        {
            return false;
        }
    }
}
