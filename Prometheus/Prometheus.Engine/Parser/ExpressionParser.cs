using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Prometheus.Engine.Analyzer;
using Prometheus.Engine.Analyzer.Atomic;
using Prometheus.Engine.Model;

namespace Prometheus.Engine.Parser
{
    public class ExpressionParser
    {
        private const string CHANGES_STATE_MARKER = "ChangesState";
        private const string IS_MODIFIED_ATOMIC_MARKER = "IsModifiedAtomic";
        private readonly Regex convertAtomicMemberRegex;

        public ExpressionParser()
        {
            convertAtomicMemberRegex = new Regex(@"Convert\((.*)\)");
        }

        public List<IInvariant> Parse(Expression expression)
        {
            LambdaExpression lambdaExpression = (LambdaExpression) expression;
            List<ParameterExpression> parameters = lambdaExpression.Parameters.ToList();

            if(lambdaExpression.Body is MethodCallExpression)
                return new List<IInvariant> { ParseMethodCallExpression(parameters, (MethodCallExpression)lambdaExpression.Body) };

            return ParseBinaryExpression(parameters, (BinaryExpression) lambdaExpression.Body);
        }

        private List<IInvariant> ParseBinaryExpression(List<ParameterExpression> parameters, BinaryExpression expression)
        {
            var result = new List<IInvariant>();

            if (expression.Right is MethodCallExpression)
            {
                result.Add(ParseMethodCallExpression(parameters, (MethodCallExpression) expression.Right));
            }
            else
            {
                result.AddRange(ParseBinaryExpression(parameters, (BinaryExpression)expression.Right));
            }

            if (expression.Left is MethodCallExpression)
            {
                result.Add(ParseMethodCallExpression(parameters, (MethodCallExpression)expression.Left));
            }
            else
            {
                result.AddRange(ParseBinaryExpression(parameters, (BinaryExpression)expression.Left));
            }

            return result;
        }

        private IInvariant ParseMethodCallExpression(List<ParameterExpression> parameters, MethodCallExpression expression)
        {
            if (expression.ToString().Contains(IS_MODIFIED_ATOMIC_MARKER))
                return AtomicInvariant.Empty.WithExpression(ExtractParameter(parameters, expression.Arguments[0]), expression);

            //TODO: process rest of invariants

            return null;
        }

        private ParameterExpression ExtractParameter(List<ParameterExpression> parameters, Expression argument)
        {
            var argumentText = argument.ToString();
            string[] memberTokens = convertAtomicMemberRegex.IsMatch(argumentText)
                ? convertAtomicMemberRegex.Match(argumentText).Groups[1].Value.Split('.')
                : argumentText.Split('.');

            if (memberTokens.Length>2)
                throw new ArgumentException("Specified invariant contains nested references; only one level member reference is allowed");

            return parameters.First(x => x.ToString() == memberTokens[0]);
        }
    }
}
