using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Prometheus.Engine.Analyzer.Atomic
{
    public class AtomicInvariant : IInvariant
    {
        private Expression atomicInvariantExpression;
        private const string IS_MODIFIED_ATOMIC_MARKER = "IsModifiedAtomic";
        private static readonly Regex ConvertAtomicMemberRegex;

        static AtomicInvariant()
        {
            ConvertAtomicMemberRegex = new Regex(@"Convert\((.*)\)");
        }

        private AtomicInvariant()
        {
        }

        public MemberInfo Member { get; set; }

        public static AtomicInvariant Empty => new AtomicInvariant();

        public AtomicInvariant WithExpression<T>(Expression<Func<T, bool>> atomicExpression)
        {
            if(atomicInvariantExpression != null)
                throw new ArgumentException("Only one expression and only one member can be used per atomic invariant");

            if (!atomicExpression.ToString().Contains(IS_MODIFIED_ATOMIC_MARKER))
                throw new ArgumentException("IsModifiedAtomic marker is not used on any of the specified members");

            atomicInvariantExpression = atomicExpression;
            ParseExpression(atomicExpression);

            return this;
        }

        private void ParseExpression<T>(Expression<Func<T, bool>> atomicExpression)
        {
            var parameter = atomicExpression.Parameters[0];

            var bodyMethodExpression = (MethodCallExpression) atomicExpression.Body;

            if (bodyMethodExpression.Arguments.Count == 1) {
                ParsePublicMember(parameter.Name, parameter.Type, bodyMethodExpression);
            } else {
                ParsePrivateMember(parameter.Name, parameter.Type, bodyMethodExpression);
            }
        }

        private void ParsePublicMember(string parameterName, Type parameterType, MethodCallExpression expression)
        {
            string argument = expression.Arguments[0].ToString();
            string[] memberTokens = ConvertAtomicMemberRegex.IsMatch(argument)
                ? ConvertAtomicMemberRegex.Match(argument).Groups[1].Value.Split('.')
                : argument.Split('.');

            if (memberTokens.Length > 2)
                throw new ArgumentException(
                    "Specified invariant contains nested arguments; only one level member reference is allowed");

            //TODO: we could also specify multiple members for an instance type e.g. person.Invoice.Account.IsModifiedAtomic()
            if (memberTokens.Length != 2)
                throw new ArgumentException("A member must be specified for the given type");

            string parameter = memberTokens[0];
            string member = memberTokens[1];

            if (parameterName != parameter)
                throw new ArgumentException(
                    "Specified invariant is invalid; only one level member reference is allowed per type (either private or public member)");

            Member = parameterType.GetMember(member, BindingFlags.Public | BindingFlags.Instance).First();
        }

        private void ParsePrivateMember(string parameterName, Type parameterType, MethodCallExpression expression)
        {
            string parameter = expression.Arguments[0].ToString();
            string member = expression.Arguments[1].ToString().Trim('"');

            if (member.Contains("."))
                throw new ArgumentException(
                    "Specified invariant contains nested arguments; only one level member reference is allowed");

            if (parameterName!=parameter)
                throw new ArgumentException(
                    "Specified invariant is invalid; only one level member reference is allowed per type (either private or public member)");

            Member = parameterType.GetField(member, BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
}