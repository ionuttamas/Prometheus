using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
        private readonly string markingMethod;
        private readonly Workspace workspace;

        public AtomicAnalyzer(Workspace workspace)
        {
            this.workspace = workspace;
            markingMethod = nameof(Extensions.ModelExtensions.IsModifiedAtomic);
        }

        public IAnalysis Analyze(Expression expression)
        {
            Type type = expression.GetParameterType();
            string parameterName = expression.GetParameterName();
            string textExpression = expression.ToString();
            List<MemberInfo> publicMembers = GetPublicMembers(type, parameterName, textExpression);
            List<MemberInfo> privateMembers = GetPrivateMembers(type, textExpression);
            AnalyzePrivateMember(privateMembers.First());

            return new AtomicAnalysis();
        }

        private List<MemberInfo> GetPublicMembers(Type type, string parameter, string expression)
        {
            var pattern = $"{markingMethod}\\(\\)";
            var matches = Regex.Matches(expression, pattern).Cast<Match>().Select(x => x.Groups[0]).ToList();
            var result = new List<MemberInfo>();

            foreach (var match in matches) {
                expression = expression.Substring(0, match.Index);
                expression = expression.Substring(expression.LastIndexOf($"{parameter}.", StringComparison.InvariantCultureIgnoreCase));
                var memberName = expression.Trim('.');
                var member = type.GetMember(memberName, BindingFlags.Instance | BindingFlags.Public).First();
                result.Add(member);
            }

            return result;
        }

        private List<MemberInfo> GetPrivateMembers(Type type, string expression)
        {
            string pattern = $"{markingMethod}\\(\"[^\"]*\"\\)";
            var matches = Regex.Matches(expression, pattern).Cast<Match>().Select(x => x.Groups[0]).ToList();
            var result = new List<MemberInfo>();

            foreach (var match in matches)
            {
                string memberName = match.Value.TrimStart(markingMethod, StringComparison.InvariantCultureIgnoreCase);
                memberName = memberName.Substring(2, memberName.Length - 4);
                var member = type.GetMember(memberName, BindingFlags.Instance | BindingFlags.NonPublic).First();
                result.Add(member);
            }

            return result;
        }

        private void AnalyzePrivateMember(MemberInfo member)
        {
            string assemblyName = member.DeclaringType.Assembly.GetName().Name;
            Project project = workspace.CurrentSolution.Projects.First(x => x.AssemblyName == assemblyName);
            Compilation compilation = project.GetCompilation();
            INamedTypeSymbol classSymbol = compilation.GetTypeByMetadataName(member.DeclaringType.FullName);


        }
    }
}