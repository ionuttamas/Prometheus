using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Prometheus.Engine.Analyzer;
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
            Dictionary<string, Type> parameters = lambdaExpression.Parameters.ToDictionary(x=>x.Name, x=>x.Type);

            if(lambdaExpression.Body is MethodCallExpression)
                return new List<IInvariant> { ParseMethodCallExpression(parameters, (MethodCallExpression)lambdaExpression.Body) };

            return ParseBinaryExpression(parameters, (BinaryExpression) lambdaExpression.Body);
        }

        private List<IInvariant> ParseBinaryExpression(Dictionary<string, Type> parameters, BinaryExpression expression)
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

        private IInvariant ParseMethodCallExpression(Dictionary<string, Type> parameters, MethodCallExpression expression)
        {
            if (expression.ToString().Contains(IS_MODIFIED_ATOMIC_MARKER))
                return null;

            //..TODO rest of invariants

            return null;
        }
    }

    public class TypeGraph
    {
    }
}
