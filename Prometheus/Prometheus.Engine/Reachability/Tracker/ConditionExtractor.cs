using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.Reachability.Tracker
{
    internal class ConditionExtractor
    {
        private readonly ConditionCache conditionCache;

        public ConditionExtractor()
        {
            conditionCache = new ConditionCache();
        }

        public HashSet<Condition> ExtractConditions(SyntaxNode node) {
            if (conditionCache.TryGet(node, out var conditions))
                return conditions;

            var exitConditions = ExtractNegatedExitConditions(node);
            conditions = ExtractIfElseConditions(node);
            conditions.UnionWith(exitConditions);
            conditionCache.AddToCache(node, conditions);

            return conditions;
        }

        /// <summary>
        /// In the case of return types, for instance for "return person" statement to be reached,
        /// the previous 2 conditions need to be negated: NOT (person.Age == 20) AND NOT (person.Age == 30).
        /// </summary>
        /// <code>
        ///  public Person DecrementAge(Person person)
        ///  {
        ///      if(person.Age == 20)
        ///      {
        ///          return new Person(20);
        ///      }
        ///      else if(person.Age == 30)
        ///      {
        ///          return new Person(30);
        ///      }
        ///
        ///      return person;
        ///  }
        /// </code>
        private HashSet<Condition> ExtractNegatedExitConditions(SyntaxNode node) {
            var nodeSpan = node.GetLocation().SourceSpan;
            var methodDeclaration = node.GetContainingMethod();
            var previousReturnConditions = methodDeclaration
                .DescendantNodes<ReturnStatementSyntax>()
                .Where(x => x.GetLocation().SourceSpan.End < nodeSpan.Start)
                .Select(ExtractIfElseConditions)
                .Where(x => x.Any())
                .Select(x => new Condition(x, true))
                .ToList();
            var exitConditions = previousReturnConditions.Any() ? new HashSet<Condition>(previousReturnConditions) : new HashSet<Condition>();

            return exitConditions;
        }

        /// <summary>
        /// In the case of return types, for instance for "return new Person(30)" statement to be reached,
        /// the following conditions need to be satisfied: (person.AccountBalance == 100) AND NOT `(person.Age == 20) AND (person.Age == 30).
        /// </summary>
        /// <code>
        ///  public Person DecrementAge(Person person)
        ///  {
        ///      if(person.AccountBalance == 100)
        ///      {
        ///         if(person.Age == 20)
        ///         {
        ///             return new Person(20);
        ///         }
        ///         else if(person.Age == 30)
        ///         {
        ///             return new Person(30);
        ///         }
        ///      }
        ///
        ///      return person;
        ///  }
        /// </code>
        private HashSet<Condition> ExtractIfElseConditions(SyntaxNode node) {
            HashSet<Condition> conditions = new HashSet<Condition>();
            var elseClause = node.FirstAncestor<ElseClauseSyntax>();
            var ifClause = node.FirstAncestor<IfStatementSyntax>();

            //If the node is within the condition of an IfStatement,
            //we exclude the condition and continue with the ancestors
            if (ifClause != null && ifClause.Condition.Contains(node)) {
                node = ifClause;
                ifClause = ifClause.FirstAncestor<IfStatementSyntax>();
            }

            while (node != null) {
                if (ifClause != null && !ifClause.Contains(elseClause)) {
                    conditions.UnionWith(ProcessIfStatement(node, out node));
                } else if (elseClause != null) {
                    conditions.UnionWith(ProcessElseStatement(node, out node));
                } else {
                    return conditions;
                }

                elseClause = node.FirstAncestor<ElseClauseSyntax>();
                ifClause = node.FirstAncestor<IfStatementSyntax>();
            }

            return conditions;
        }

        private HashSet<Condition> ProcessIfStatement(SyntaxNode node, out SyntaxNode lastNode) {
            var ifClause = node.FirstAncestor<IfStatementSyntax>();
            var conditions = new HashSet<Condition>();

            lastNode = ifClause;

            if (ifClause == null)
                return conditions;

            conditions.Add(new Condition(ifClause.Condition, false));
            var elseClause = ifClause.Parent as ElseClauseSyntax;

            while (elseClause != null) {
                var ifStatement = elseClause.Parent as IfStatementSyntax;

                if (ifStatement == null)
                    break;

                conditions.Add(new Condition(ifStatement.Condition, true));
                lastNode = ifStatement;
                elseClause = ifStatement.Parent as ElseClauseSyntax;
            }

            return conditions;
        }

        private HashSet<Condition> ProcessElseStatement(SyntaxNode node, out SyntaxNode lastNode) {
            var elseClause = node.FirstAncestor<ElseClauseSyntax>();
            var conditions = new HashSet<Condition>();
            lastNode = elseClause;

            while (elseClause != null) {
                var ifStatement = elseClause.Parent as IfStatementSyntax;

                if (ifStatement == null)
                    break;

                conditions.Add(new Condition(ifStatement.Condition, true));
                lastNode = ifStatement;
                elseClause = ifStatement.Parent as ElseClauseSyntax;
            }

            return conditions;
        }

    }
}