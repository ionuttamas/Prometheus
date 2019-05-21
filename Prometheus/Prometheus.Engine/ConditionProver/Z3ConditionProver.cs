using System.Collections.Generic;
using System.Linq;
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

            foreach (var assignmentCondition in assignment.Conditions) {
                var boolExpr = boolExpressionParser.ParseExpression(assignmentCondition.TestExpression, out var membersTable);
                processedMembers.Merge(membersTable);
                conditions.Add(assignmentCondition.IsNegated ? context.MkNot(boolExpr) : boolExpr);
            }

            BoolExpr expression = context.MkAnd(conditions.ToArray());

            return expression;
        }

        private List<BoolExpr> ParseConditionalAssignment(ConditionalAssignment assignment, Dictionary<string, NodeType> cachedMembers)
        {
            var contexts = assignment.RightReference.ReferenceContexts;
            var conditions = assignment
                .Conditions
                .Select(x =>
                        x.IsNegated
                        ? boolExpressionParser.ParseCachedExpression(x.TestExpression, contexts, cachedMembers).Select(expr=>context.MkNot(expr)).ToList()
                        : boolExpressionParser.ParseCachedExpression(x.TestExpression, contexts, cachedMembers))
                .ToList();
            var cartesianProduct = conditions.CartesianProduct();
            var result = cartesianProduct.Select(x => context.MkAnd(x)).ToList();

            return result;
        }
    }
}