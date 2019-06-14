using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.ReachabilityProver.Model;

namespace Prometheus.Engine.ConditionProver {
    internal class Z3ConditionProver : IConditionProver
    {
        private readonly Z3BooleanExpressionParser boolExpressionParser;
        private readonly Context context;

        public Z3ConditionProver(Z3BooleanExpressionParser boolExpressionParser, Context context)
        {
            this.boolExpressionParser = boolExpressionParser;
            this.context = context;
        }

        public bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second)
        {
            BoolExpr firstCondition = ParseConditionalAssignment(first, out var processedMembers);
            //TODO: use (reference, state) for reference context validation
            List<BoolExpr> secondConditions = ParseConditionalAssignment(second, processedMembers);
            Solver solver = context.MkSolver();

            foreach (BoolExpr secondCondition in secondConditions)
            {
                solver.Assert(firstCondition, secondCondition);
                Status status = solver.Check();

                if (status == Status.SATISFIABLE)
                    return true;

                solver.Reset();
            }

            return false;
        }

        public void Dispose() {
            context.Dispose();
        }

        private BoolExpr ParseConditionalAssignment(ConditionalAssignment assignment, out Dictionary<string, NodeType> processedMembers) {
            List<BoolExpr> conditions = new List<BoolExpr>();
            processedMembers = new Dictionary<string, NodeType>();
            var contexts = assignment.RightReference.ReferenceContexts;

            foreach (var assignmentCondition in assignment.Conditions) {
                var boolExpr = ParseCondition(assignmentCondition, contexts, out var membersTable);
                processedMembers.Merge(membersTable);
                conditions.Add(assignmentCondition.IsNegated ? context.MkNot(boolExpr) : boolExpr);
            }

            BoolExpr expr = context.MkAnd(conditions);
            expr = (BoolExpr)expr.Simplify();

            return expr;
        }

        private List<BoolExpr> ParseConditionalAssignment(ConditionalAssignment assignment, Dictionary<string, NodeType> cachedMembers)
        {
            var contexts = assignment.RightReference.ReferenceContexts;
            var conditions = assignment
                .Conditions
                .Select(x => ParseCachedCondition(x, contexts, cachedMembers))
                .ToList();
            var cartesianProduct = conditions.CartesianProduct();
            var result = cartesianProduct.Select(x => context.MkAnd(x)).ToList();

            return result;
        }

        private BoolExpr ParseCondition(Condition condition, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedMembers)
        {
            processedMembers = new Dictionary<string, NodeType>();
            List<BoolExpr> exprs = new List<BoolExpr>();

            BoolExpr boolExpr;

            if (condition.TestExpression != null)
            {
                boolExpr = boolExpressionParser.ParseExpression(condition.TestExpression, contexts, out var membersTable);
                processedMembers.Merge(membersTable);
                exprs.Add(condition.IsNegated ? context.MkNot(boolExpr) : boolExpr);
            }

            foreach (var subCondition in condition.Conditions)
            {
                boolExpr = ParseCondition(subCondition, contexts, out var membersTable);
                processedMembers.Merge(membersTable);
                exprs.Add(boolExpr);
            }

            BoolExpr expr = context.MkAnd(exprs);
            expr = (BoolExpr)expr.Simplify();

            return expr;
        }

        private List<BoolExpr> ParseCachedCondition(Condition condition, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedMembers) {
            var exprs = new List<BoolExpr>();

            if (condition.TestExpression != null) {
                var boolExprs = boolExpressionParser.ParseCachedExpression(condition.TestExpression, contexts, cachedMembers);

                if (condition.IsNegated)
                {
                    boolExprs = boolExprs.Select(x => context.MkNot(x)).ToList();
                }

                boolExprs.ForEach(x => exprs.Add(x));
            }

            foreach (var subCondition in condition.Conditions) {
                var boolExprs = ParseCachedCondition(subCondition, contexts, cachedMembers);
                boolExprs.ForEach(x=>exprs.Add(x));
            }

            return exprs;
        }
    }
}