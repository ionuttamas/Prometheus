using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Prometheus.Engine.Invariant;

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
                return GetAtomicInvariant(parameters, expression);

            //..TODO rest of invariants

            return null;
        }

        private AtomicInvariant GetAtomicInvariant(Dictionary<string, Type> parameters, MethodCallExpression expression)
        {
            if (expression.Arguments.Count == 1)
                return GetPublicAtomicInvariant(parameters, expression);

            return GetPrivateAtomicInvariant(parameters, expression);
        }

        private AtomicInvariant GetPublicAtomicInvariant(Dictionary<string, Type> parameters, MethodCallExpression expression)
        {
            string argument = expression.Arguments[0].ToString();
            string[] memberTokens = convertAtomicMemberRegex.IsMatch(argument) ?
                                    convertAtomicMemberRegex.Match(argument).Groups[1].Value.Split('.'):
                                    argument.Split('.');

            if(memberTokens.Length > 2)
                throw new ArgumentException("Specified invariant contains nested arguments; only one level member reference is allowed");

            if (memberTokens.Length != 2)
                throw new ArgumentException("A member must be specified for the given type");

            string parameter = memberTokens[0];
            string member = memberTokens[1];

            if (!parameters.ContainsKey(parameter))
                throw new ArgumentException("Specified invariant is invalid; only one level member reference is allowed per type (either private or public member)");

            var invariant = new AtomicInvariant
            {
                Type = parameters[parameter],
                Member = member
            };

            return invariant;
        }

        private AtomicInvariant GetPrivateAtomicInvariant(Dictionary<string, Type> parameters, MethodCallExpression expression)
        {
            string parameter = expression.Arguments[0].ToString();
            string member = expression.Arguments[1].ToString();

            if (member.Contains("."))
                throw new ArgumentException("Specified invariant contains nested arguments; only one level member reference is allowed");

            if (!parameters.ContainsKey(parameter))
                throw new ArgumentException("Specified invariant is invalid; only one level member reference is allowed per type (either private or public member)");

            var invariant = new AtomicInvariant
            {
                Type = parameters[parameter],
                Member = member
            };

            return invariant;
        }
    }

    public class TypeGraph
    {
    }
}
