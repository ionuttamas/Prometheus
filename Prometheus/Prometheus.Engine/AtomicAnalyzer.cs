using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Prometheus.Common;

namespace Prometheus.Engine
{
    /// <summary>
    /// This checker verifies if a variable is updated atomically within a code base.
    /// </summary>
    public class AtomicAnalyzer : IAnalyzer
    {
        public void Analyze(Expression expression, Workspace workspace)
        {
            string markName = nameof(Extensions.ModelExtensions.IsModifiedAtomic);
            Type type = expression.GetParameterType();
            string parameterName = expression.GetParameterName();
            string textExpression = expression.ToString();
            var publicFieldPattern = $"{markName}\\(\\)";
            var privateFieldPattern = $"{markName}\\(\"[^\"]*\"\\)";
            var publicFieldMatches = Regex.Matches(textExpression, publicFieldPattern).Cast<Match>().Select(x => x.Groups[0]).ToList();
            var privateFieldMatches = Regex.Matches(textExpression, privateFieldPattern).Cast<Match>().Select(x => x.Groups[0]).ToList();


        }
    }
}