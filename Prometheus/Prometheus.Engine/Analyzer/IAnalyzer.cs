using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Prometheus.Engine
{
    /// <summary>
    /// Marker interface for code analyzer.
    /// </summary>
    public interface IAnalyzer
    {
        IAnalysis Analyze(Expression expression);
    }
}
