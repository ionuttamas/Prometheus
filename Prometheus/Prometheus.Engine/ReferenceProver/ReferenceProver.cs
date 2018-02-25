using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;

namespace Prometheus.Engine.ReferenceProver
{
    internal class ReferenceProver : IDisposable
    {
        private readonly ReferenceTracker referenceTracker;
        private readonly Context context;

        public ReferenceProver(ReferenceTracker referenceTracker)
        {
            this.referenceTracker = referenceTracker;
            context = new Context();
        }

        public void Dispose()
        {
            context.Dispose();
        }

        /// <summary>
        /// Checks whether two identifiers can possibly have the same value within a thread schedule.
        /// TODO: this does not consider the possible conditional cases for assigning a variable within loops ("for", "while" loops).
        /// TODO: this does not consider assignments like: "order = person.Orders.First()" and "order = instance" where "instance" and "person.Orders.First()" can be the same
        /// </summary>
        public bool HaveCommonValue(SyntaxToken first, SyntaxToken second, out object commonNode)
        {
            var firstAssignment = new ConditionalAssignment
            {
                TokenReference = first,
                AssignmentLocation = first.GetLocation()
            };
            var secondAssignment = new ConditionalAssignment
            {
                TokenReference = second,
                AssignmentLocation = second.GetLocation()
            };
            return HaveCommonValueInternal(firstAssignment, secondAssignment, out commonNode);
        }

        private bool HaveCommonValueInternal(ConditionalAssignment first, ConditionalAssignment second,
            out object commonNode)
        {
            commonNode = null;

            //TODO: need to check scoping: if "first" is a local variable => it cannot match a variable from another function/thread
            var firstAssignments =
                referenceTracker.GetAssignments(first.NodeReference?.DescendantTokens().First() ?? first.TokenReference);
                //todo: this needs checking
            var secondAssignments =
                referenceTracker.GetAssignments(second.NodeReference?.DescendantTokens().First() ??
                                                second.TokenReference);

            foreach (ConditionalAssignment assignment in firstAssignments)
            {
                assignment.Conditions.UnionWith(first.Conditions);
            }

            foreach (ConditionalAssignment assignment in secondAssignments)
            {
                assignment.Conditions.UnionWith(second.Conditions);
            }

            if (!firstAssignments.Any())
            {
                firstAssignments = new List<ConditionalAssignment> {first};
            }

            if (!secondAssignments.Any())
            {
                secondAssignments = new List<ConditionalAssignment> {second};
            }

            foreach (ConditionalAssignment firstAssignment in firstAssignments)
            {
                foreach (ConditionalAssignment secondAssignment in secondAssignments)
                {
                    if (ValidateReachability(firstAssignment, secondAssignment, out commonNode))
                        return true;
                }
            }

            return false;
        }

        private bool ValidateReachability(ConditionalAssignment first, ConditionalAssignment second,
            out object commonNode)
        {
            commonNode = null;

            if (!IsSatisfiable(first, second))
                return false;

            if (AreEquivalent(first, second))
            {
                commonNode = first.NodeReference ?? (object) first.TokenReference;
                return true;
            }

            var firstReferenceAssignment = new ConditionalAssignment
            {
                NodeReference = first.NodeReference,
                TokenReference = first.TokenReference,
                AssignmentLocation = first.AssignmentLocation,
                Conditions = first.Conditions
            };
            var secondReferenceAssignment = new ConditionalAssignment
            {
                NodeReference = second.NodeReference,
                TokenReference = second.TokenReference,
                AssignmentLocation = second.AssignmentLocation,
                Conditions = second.Conditions
            };

            if (HaveCommonValueInternal(firstReferenceAssignment, second, out commonNode))
                return true;

            return HaveCommonValueInternal(first, secondReferenceAssignment, out commonNode);
        }

        /// <summary>
        /// This checks whether two nodes are the same reference (the shared memory of two threads).
        /// This can be a class field/property used by both thread functions or parameters passed to threads that are the same
        /// TODO: currently this checks only for field equivalence
        /// </summary>
        private static bool AreEquivalent(ConditionalAssignment first, ConditionalAssignment second)
        {
            var firstReferenceName = first.NodeReference?.ToString() ?? first.TokenReference.ToString();
            var secondReferenceName = second.NodeReference?.ToString() ?? second.TokenReference.ToString();
            var firstLocation = first.NodeReference?.GetLocation() ?? first.TokenReference.GetLocation();
            var secondLocation = second.NodeReference?.GetLocation() ?? second.TokenReference.GetLocation();

            if (firstReferenceName != secondReferenceName)
                return false;

            return firstLocation == secondLocation;
        }

        #region Conditional prover

        //TODO: move this to separate service
        //TODO: this is ugly state keeping
        private class NodeTypes
        {
            public SyntaxNode Node { get; set; }
            public Expr Expression { get; set; }
            public List<Type> TypeChain { get; set; }
        }

        private Dictionary<string, NodeTypes> conditionalNodeTable = new Dictionary<string, NodeTypes>();
        private Dictionary<string, NodeTypes> currentNodeTable = new Dictionary<string, NodeTypes>();
        private bool matchExpressions = false;

        private bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second)
        {
            conditionalNodeTable = GetNodeTypes(first);
            BoolExpr firstCondition = ParseConditionalAssignment(first);
            BoolExpr secondCondition = ParseConditionalAssignment(second);
            Solver solver = context.MkSolver();
            solver.Assert(firstCondition, secondCondition);
            Status status = solver.Check();

            return status == Status.SATISFIABLE;
        }

        private BoolExpr ParseConditionalAssignment(ConditionalAssignment assignment)
        {
            BoolExpr[] conditions =
                assignment.Conditions.Select(
                    x =>
                        x.IsNegated
                            ? context.MkNot(ParseExpression(x.IfStatement.Condition))
                            : ParseExpression(x.IfStatement.Condition)).ToArray();
            BoolExpr expression = context.MkAnd(conditions);

            return expression;
        }

        private BoolExpr ParseExpression(ExpressionSyntax expressionSyntax)
        {
            var expressionKind = expressionSyntax.Kind();

            switch (expressionKind)
            {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression((PrefixUnaryExpressionSyntax) expressionSyntax);
                case SyntaxKind.SimpleMemberAccessExpression:
                    return context.MkBoolConst(expressionSyntax.ToString());
            }

            var binaryExpression = (BinaryExpressionSyntax) expressionSyntax;

            switch (expressionKind)
            {
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                    return ParseBinaryExpression(binaryExpression);
                default:
                    throw new NotImplementedException();
            }

        }

        private BoolExpr ParseBinaryExpression(BinaryExpressionSyntax binaryExpression)
        {
            SyntaxKind expressionKind = binaryExpression.Kind();
            Expr left = ParseExpressionMember(binaryExpression.Left);
            Expr right = ParseExpressionMember(binaryExpression.Right);

            switch (expressionKind)
            {
                case SyntaxKind.LogicalAndExpression:
                    return context.MkAnd((BoolExpr) left, (BoolExpr) right);
                case SyntaxKind.LogicalOrExpression:
                    return context.MkOr((BoolExpr) left, (BoolExpr) right);
                case SyntaxKind.GreaterThanExpression:
                    return context.MkGt((ArithExpr) left, (ArithExpr) right);
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return context.MkGe((ArithExpr) left, (ArithExpr) right);
                case SyntaxKind.LessThanExpression:
                    return context.MkLt((ArithExpr) left, (ArithExpr) right);
                case SyntaxKind.LessThanOrEqualExpression:
                    return context.MkLe((ArithExpr) left, (ArithExpr) right);

                //TODO: Fix this: since this is only for numeric values
                case SyntaxKind.EqualsExpression:
                    return context.MkEq((ArithExpr) left, (ArithExpr) right);
                case SyntaxKind.NotEqualsExpression:
                    return context.MkNot(context.MkEq((ArithExpr) left, (ArithExpr) right));
                default:
                    throw new NotImplementedException();
            }
        }

        private BoolExpr ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression)
        {
            if (prefixUnaryExpression.Kind() == SyntaxKind.LogicalNotExpression)
            {
                var innerExpression = prefixUnaryExpression.Operand;
                var parsedExpression = ParseExpression(innerExpression);
                return context.MkNot(parsedExpression);
            }

            throw new NotImplementedException();
        }

        private Expr ParseExpressionMember(ExpressionSyntax memberExpression)
        {
            var expressionKind = memberExpression.Kind();

            //todo: fix sort type: real vs int
            if (expressionKind == SyntaxKind.NumericLiteralExpression)
            {
                return context.MkNumeral(memberExpression.ToString(), context.RealSort);
            }

            if (expressionKind == SyntaxKind.SimpleMemberAccessExpression ||
                expressionKind == SyntaxKind.IdentifierName)
            {
                //TODO: this needs to see if the members can be the same or not
                if (hackTable.ContainsKey(memberExpression.ToString()))
                    return hackTable[memberExpression.ToString()].Expression;

                var constExpr = context.MkConst(memberExpression.ToString(), context.RealSort);
                hackTable[memberExpression.ToString()] = new NodeTypes
                {
                    Expression = constExpr,
                    Node = memberExpression,
                    TypeChain = expressionKind == SyntaxKind.SimpleMemberAccessExpression
                        ? ((MemberAccessExpressionSyntax) memberExpression).GetTypes()
                        : new List<Type> {memberExpression.GetType()}
                };
                return constExpr;
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression)
            {
                var prefixUnaryExpression = (PrefixUnaryExpressionSyntax) memberExpression;
                var negatedExpression = ParseExpressionMember(prefixUnaryExpression.Operand);

                return context.MkUnaryMinus((ArithExpr) negatedExpression);
            }

            return ParseExpression(memberExpression);
        }

        #endregion

        #region Condition matching

        private bool IsMatch()
        {
            return true;
        }

        private static Dictionary<string, NodeTypes> GetNodeTypes(ConditionalAssignment conditionalAssignment)
        {
            var comparandNodes = conditionalAssignment
                .Conditions
                .SelectMany(x => GetNodeTypes(x.IfStatement))
                .ToDictionary(x => x.Key, x => x.Value);

            return comparandNodes;
        }

        private static Dictionary<string, NodeTypes> GetNodeTypes(IfStatementSyntax ifStatement)
        {
            //TODO: we need to check based on the type (from.Address==address), we need to check them separately
            //We only take simple binary expressions or unary expression, but exclude members that are within a function call in a method
            var memberAccessExpressions =
                ifStatement.DescendantNodes<MemberAccessExpressionSyntax>(
                    x =>
                        (x.Parent is BinaryExpressionSyntax || x.Parent is PrefixUnaryExpressionSyntax) &&
                        !x.AncestorNodesUntil<InvocationExpressionSyntax>(ifStatement).Any());
            var identifiers =
                ifStatement.DescendantNodes<IdentifierNameSyntax>(
                    x =>
                        (x.Parent is BinaryExpressionSyntax || x.Parent is PrefixUnaryExpressionSyntax) &&
                        !x.AncestorNodesUntil<InvocationExpressionSyntax>(ifStatement).Any());
            var nodeTable = new Dictionary<string, NodeTypes>();

            foreach (IdentifierNameSyntax identifier in identifiers)
            {
                var nodeTypes = new NodeTypes
                {
                    Node = identifier,
                    TypeChain = new List<Type> { identifier.GetNodeType() }
                };
                nodeTable[identifier.Identifier.Text] = nodeTypes;
            }

            foreach (MemberAccessExpressionSyntax memberAccessExpression in memberAccessExpressions)
            {
                var nodeTypes = new NodeTypes {
                    Node = memberAccessExpression,
                    TypeChain = memberAccessExpression.GetNodeTypes()
                };
                nodeTable[memberAccessExpression.ToString()] = nodeTypes;
            }

            return nodeTable;
        }

        #endregion
    }
}