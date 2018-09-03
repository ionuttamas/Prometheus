using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.Reachability.Tracker;
using Prometheus.Engine.ReachabilityProver.Model;
using Prometheus.Engine.Types;

namespace Prometheus.Engine.ConditionProver
{
    internal class Z3BooleanExpressionParser
    {
        private readonly ITypeService typeService;
        private readonly IReferenceParser referenceParser;
        private HaveCommonReference reachabilityDelegate;
        private GetConditionalAssignments getAssignmentsDelegate;
        private ParseBooleanMethod parseBooleanMethodDelegate;

        private readonly Context context;

        public Z3BooleanExpressionParser(ITypeService typeService, IReferenceParser referenceParser, Context context) {
            this.typeService = typeService;
            this.referenceParser = referenceParser;
            this.context = context;
        }

        public void Configure(HaveCommonReference @delegate) {
            reachabilityDelegate = @delegate;
        }

        public void Configure(GetConditionalAssignments @delegate) {
            getAssignmentsDelegate = @delegate;
        }

        public void Configure(ParseBooleanMethod @delegate) {
            parseBooleanMethodDelegate = @delegate;
        }

        public BoolExpr ParseExpression(ExpressionSyntax expressionSyntax, out Dictionary<string, NodeType> processedMembers) {
            var expressionKind = expressionSyntax.Kind();

            switch (expressionKind) {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression(expressionSyntax.As<PrefixUnaryExpressionSyntax>(), out processedMembers);
                case SyntaxKind.SimpleMemberAccessExpression:
                    processedMembers = new Dictionary<string, NodeType>();
                    return context.MkBoolConst(expressionSyntax.ToString());
            }

            var binaryExpression = expressionSyntax.As<BinaryExpressionSyntax>();

            switch (expressionKind) {
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                    return ParseBinaryExpression(binaryExpression, out processedMembers);
                default:
                    throw new NotImplementedException();
            }

        }

        public List<BoolExpr> ParseExpression(ExpressionSyntax expressionSyntax, Dictionary<string, NodeType> cachedMembers) {
            var expressionKind = expressionSyntax.Kind();

            switch (expressionKind) {
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression((PrefixUnaryExpressionSyntax)expressionSyntax, cachedMembers);
                case SyntaxKind.SimpleMemberAccessExpression:
                    return new List<BoolExpr> { context.MkBoolConst(expressionSyntax.ToString()) };

                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                    return ParseBinaryExpression(expressionSyntax.As<BinaryExpressionSyntax>(), cachedMembers);
                default:
                    throw new NotImplementedException();
            }
        }

        #region Non-cached processing

        private BoolExpr ParseBinaryExpression(BinaryExpressionSyntax binaryExpression, out Dictionary<string, NodeType> processedMembers) {
            SyntaxKind expressionKind = binaryExpression.Kind();
            var leftProcessedMembers = new Dictionary<string, NodeType>();
            var rightProcessedMembers = new Dictionary<string, NodeType>();
            Expr left;
            Expr right;

            if (binaryExpression.Left.Kind() == SyntaxKind.NullLiteralExpression) {
                left = GetNullExpression(typeService.GetTypeContainer(binaryExpression.Right).Type);
                right = ParseExpressionMember(binaryExpression.Right, out rightProcessedMembers);
            } else if (binaryExpression.Right.Kind() == SyntaxKind.NullLiteralExpression) {
                right = GetNullExpression(typeService.GetTypeContainer(binaryExpression.Left).Type);
                left = ParseExpressionMember(binaryExpression.Left, out leftProcessedMembers);
            } else {
                left = ParseExpressionMember(binaryExpression.Left, out leftProcessedMembers);
                right = ParseExpressionMember(binaryExpression.Right, out rightProcessedMembers);
            }

            processedMembers = new Dictionary<string, NodeType>();
            processedMembers.Merge(leftProcessedMembers);
            processedMembers.Merge(rightProcessedMembers);

            switch (expressionKind) {
                case SyntaxKind.LogicalAndExpression:
                    return context.MkAnd((BoolExpr)left, (BoolExpr)right);
                case SyntaxKind.LogicalOrExpression:
                    return context.MkOr((BoolExpr)left, (BoolExpr)right);

                case SyntaxKind.GreaterThanExpression:
                    //todo: fix comparison expression for string expressions
                    return context.MkGt((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return context.MkGe((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.LessThanExpression:
                    return context.MkLt((ArithExpr)left, (ArithExpr)right);
                case SyntaxKind.LessThanOrEqualExpression:
                    return context.MkLe((ArithExpr)left, (ArithExpr)right);

                //TODO: Fix this: since this is only for numeric values
                case SyntaxKind.EqualsExpression:
                    return context.MkEq(left, right);
                case SyntaxKind.NotEqualsExpression:
                    return context.MkNot(context.MkEq(left, right));
                default:
                    throw new NotImplementedException();
            }
        }

        private BoolExpr ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, out Dictionary<string, NodeType> processedMembers) {
            if (prefixUnaryExpression.Kind() != SyntaxKind.LogicalNotExpression)
                throw new NotImplementedException();

            var innerExpression = prefixUnaryExpression.Operand;
            var innerExpressionKind = innerExpression.Kind();

            if (innerExpressionKind == SyntaxKind.SimpleMemberAccessExpression) {
                var parsedExpression = ParseExpression(innerExpression, out processedMembers);
                return context.MkNot(parsedExpression);
            }

            if (innerExpressionKind == SyntaxKind.InvocationExpression) {
                typeService.IsPureMethod(innerExpression.As<InvocationExpressionSyntax>(), out var returnType);

                if (returnType == typeof(bool)) {
                    var parsedExpression = (BoolExpr)ParseInvocationExpression(innerExpression, out processedMembers);
                    return context.MkNot(parsedExpression);
                }
            }

            throw new InvalidOperationException($"PrefixUnaryExpression {prefixUnaryExpression} could not be processed");
        }

        private Expr ParseExpressionMember(ExpressionSyntax memberExpression, out Dictionary<string, NodeType> processedMembers) {
            var expressionKind = memberExpression.Kind();
            processedMembers = new Dictionary<string, NodeType>();

            if (memberExpression is BinaryExpressionSyntax)
                return ParseBinaryExpression((BinaryExpressionSyntax)memberExpression, out processedMembers);

            if (expressionKind == SyntaxKind.NumericLiteralExpression)
                return ParseNumericLiteral(memberExpression.ToString());

            if (expressionKind == SyntaxKind.StringLiteralExpression)
                return ParseStringLiteral(memberExpression.As<LiteralExpressionSyntax>().Token.ValueText);

            if (expressionKind == SyntaxKind.InvocationExpression)
                return ParseInvocationExpression(memberExpression, out processedMembers);

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName) {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseUnaryExpression(memberExpression, out processedMembers)
                    : ParseExpression(memberExpression, out processedMembers);
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression)
                return ParseUnaryExpression(memberExpression, out processedMembers);

            var memberType = typeService.GetTypeContainer(memberExpression).Type;

            //TODO: check nested reference chains from different chains: "customer.Address.ShipInfo" & "order.ShipInfo" to be the same
            //TODO: check agains same reference chains: "from.Address.ShipInfo" & "to.Address.ShipInfo"
            //Check against the nodes from already parsed the first conditional assignment
            return ParseVariableExpression(memberExpression, memberType, processedMembers);
        }

        private Expr ParseInvocationExpression(ExpressionSyntax expression, out Dictionary<string, NodeType> processedMembers) {
            var invocationExpression = (InvocationExpressionSyntax)expression;
            var invocationType = GetInvocationType(invocationExpression);

            return typeService.IsExternal(invocationType.Type) ?
                ParseExternalCodeInvocationExpression(invocationExpression, out processedMembers) :
                ParseInternalCodeInvocationExpression(invocationExpression, invocationType, out processedMembers);
        }

        private Expr ParseExternalCodeInvocationExpression(InvocationExpressionSyntax invocationExpression, out Dictionary<string, NodeType> processedMembers) {
            typeService.IsPureMethod(invocationExpression, out var returnType);
            Sort sort = typeService.GetSort(returnType);
            Expr constExpr = context.MkConst(invocationExpression.ToString(), sort);
            processedMembers = new Dictionary<string, NodeType> {
                [invocationExpression.Expression.ToString()] = new NodeType {
                    Expression = constExpr,
                    Node = invocationExpression,
                    Type = returnType
                }
            };

            return constExpr;
        }

        private Expr ParseInternalCodeInvocationExpression(InvocationExpressionSyntax invocationExpression, InvocationType invocationType, out Dictionary<string, NodeType> processedMembers) {
            var expr = parseBooleanMethodDelegate(invocationType.MethodDeclaration, out processedMembers);
            referenceParser.GetMethodBindings(invocationExpression, invocationType.MethodDeclaration.GetContainingClass(), invocationType.MethodDeclaration.Identifier.Text, out var argumentsTable);
            var callContext = new CallContext {
                InstanceReference = invocationType.Instance,
                ArgumentsTable = argumentsTable,
                InvocationExpression = invocationExpression
            };
            var referenceContext = new ReferenceContext(callContext, null);

            foreach (var processedMember in processedMembers) {
                processedMember.Value.ExternalReference.AddContext(referenceContext);
            }

            return expr;
        }

        private Expr ParseUnaryExpression(ExpressionSyntax unaryExpression, out Dictionary<string, NodeType> processedMembers) {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)unaryExpression;
            var negatedExpression = ParseExpressionMember(prefixUnaryExpression.Operand, out processedMembers);

            return context.MkUnaryMinus((ArithExpr)negatedExpression);
        }

        private Expr ParseVariableExpression(ExpressionSyntax memberExpression, Type type, Dictionary<string, NodeType> processedMembers) {
            string memberName = memberExpression.ToString();
            string uniqueMemberName = $"{memberName}{memberExpression.GetLocation().SourceSpan}";
            Expr expr;
            bool isExternal = false;
            Reference externalReference = null;

            if (!TryParseFixedExpression(memberExpression, type, out expr)) {
                Sort sort = typeService.GetSort(type);
                expr = context.MkConst(uniqueMemberName, sort);
                var rootIdentifier = memberExpression.GetRootIdentifier();
                var rootType = typeService.GetTypeContainer(rootIdentifier).Type;
                //TODO: https://github.com/ionuttamas/Prometheus/issues/25
                var firstAssignment = getAssignmentsDelegate(rootIdentifier.Identifier).FirstOrDefault();

                if (firstAssignment != null && firstAssignment.RightReference.IsExternal || typeService.IsExternal(rootType)) {
                    isExternal = true;
                    //todo: currently we only take the first assignments of the rootIdentifier to see if is pure or not, regardless of any conditions
                    externalReference = firstAssignment.RightReference;
                }
            }

            processedMembers[memberName] = new NodeType {
                Expression = expr,
                Node = memberExpression,
                Type = type,
                IsExternal = isExternal,
                ExternalReference = externalReference
            };

            return expr;
        }

        private Expr ParseNumericLiteral(string numericLiteral) {
            Sort sort = int.TryParse(numericLiteral, out int _) ? context.IntSort : (Sort)context.RealSort;
            return context.MkNumeral(numericLiteral, context.RealSort); //TODO: issue on real>int expression
        }

        private Expr ParseStringLiteral(string stringLiteral) {
            return context.MkString(stringLiteral);
        }

        #endregion

        #region Cached processing

        private List<BoolExpr> ParseBinaryExpression(BinaryExpressionSyntax binaryExpression, Dictionary<string, NodeType> cachedMembers) {
            SyntaxKind expressionKind = binaryExpression.Kind();
            List<Expr> left;
            List<Expr> right;

            if (binaryExpression.Left.Kind() == SyntaxKind.NullLiteralExpression) {
                left = new List<Expr> { GetNullExpression(typeService.GetTypeContainer(binaryExpression.Right).Type) };
                right = ParseExpressionMember(binaryExpression.Right, cachedMembers);
            } else if (binaryExpression.Right.Kind() == SyntaxKind.NullLiteralExpression) {
                right = new List<Expr> { GetNullExpression(typeService.GetTypeContainer(binaryExpression.Left).Type) };
                left = ParseExpressionMember(binaryExpression.Left, cachedMembers);
            } else {
                left = ParseExpressionMember(binaryExpression.Left, cachedMembers);
                right = ParseExpressionMember(binaryExpression.Right, cachedMembers);
            }

            switch (expressionKind) {
                case SyntaxKind.LogicalAndExpression:
                    return ReconstructBinaryExpressions(left, right, (l, r) => context.MkAnd(l.As<BoolExpr>(), r.As<BoolExpr>()));
                case SyntaxKind.LogicalOrExpression:
                    return ReconstructBinaryExpressions(left, right, (l, r) => context.MkOr(l.As<BoolExpr>(), r.As<BoolExpr>()));
                case SyntaxKind.GreaterThanExpression:
                    return ReconstructBinaryExpressions(left, right, (l, r) => context.MkGt(l.As<ArithExpr>(), r.As<ArithExpr>()));
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return ReconstructBinaryExpressions(left, right, (l, r) => context.MkGe(l.As<ArithExpr>(), r.As<ArithExpr>()));
                case SyntaxKind.LessThanExpression:
                    return ReconstructBinaryExpressions(left, right, (l, r) => context.MkLt(l.As<ArithExpr>(), r.As<ArithExpr>()));
                case SyntaxKind.LessThanOrEqualExpression:
                    return ReconstructBinaryExpressions(left, right, (l, r) => context.MkLe(l.As<ArithExpr>(), r.As<ArithExpr>()));
                case SyntaxKind.EqualsExpression:
                    return ReconstructBinaryExpressions(left, right, (l, r) => context.MkEq(l.As<Expr>(), r.As<Expr>()));
                case SyntaxKind.NotEqualsExpression:
                    return ReconstructBinaryExpressions(left, right, (l, r) => context.MkNot(context.MkEq(l.As<Expr>(), r.As<Expr>())));
                default:
                    throw new NotImplementedException();
            }
        }

        private List<BoolExpr> ReconstructBinaryExpressions(List<Expr> leftExprs, List<Expr> rightExprs, Func<Expr, Expr, BoolExpr> combiner) {
            var result = new List<BoolExpr>();

            foreach (Expr leftExpr in leftExprs) {
                foreach (Expr rightExpr in rightExprs) {
                    result.Add(combiner(leftExpr, rightExpr));
                }
            }

            return result;
        }

        private List<BoolExpr> ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, Dictionary<string, NodeType> cachedMembers) {
            if (prefixUnaryExpression.Kind() != SyntaxKind.LogicalNotExpression)
                throw new NotImplementedException();

            var innerExpression = prefixUnaryExpression.Operand;
            var innerExpressionKind = innerExpression.Kind();

            if (innerExpressionKind == SyntaxKind.SimpleMemberAccessExpression || innerExpressionKind == SyntaxKind.IdentifierName) {
                var parsedExpressions = ParseExpressionMember(innerExpression, cachedMembers);
                return parsedExpressions.Select(x => context.MkNot(x.As<BoolExpr>())).ToList();
            }

            if (innerExpressionKind == SyntaxKind.InvocationExpression) {
                typeService.IsPureMethod(innerExpression.As<InvocationExpressionSyntax>(), out var returnType);

                if (returnType == typeof(bool)) {
                    var parsedExpressions = ParseCachedInvocationExpression(innerExpression, cachedMembers).Select(x => context.MkNot(x.As<BoolExpr>())).ToList();
                    return parsedExpressions;
                }
            }

            throw new InvalidOperationException($"PrefixUnaryExpression {prefixUnaryExpression} could not be processed");
        }

        private List<Expr> ParseExpressionMember(ExpressionSyntax memberExpression, Dictionary<string, NodeType> cachedMembers) {
            var expressionKind = memberExpression.Kind();

            if (memberExpression is BinaryExpressionSyntax)
                return ParseBinaryExpression((BinaryExpressionSyntax)memberExpression, cachedMembers).OfType<Expr>().ToList();

            if (expressionKind == SyntaxKind.NumericLiteralExpression)
                return new List<Expr> { ParseNumericLiteral(memberExpression.ToString()) };

            if (expressionKind == SyntaxKind.StringLiteralExpression)
                return new List<Expr> { ParseStringLiteral(memberExpression.As<LiteralExpressionSyntax>().Token.ValueText) };

            if (expressionKind == SyntaxKind.InvocationExpression)
                return ParseCachedInvocationExpression(memberExpression, cachedMembers);

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName) {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseUnaryExpression(memberExpression, cachedMembers)
                    : ParseExpression(memberExpression, cachedMembers).OfType<Expr>().ToList();
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression)
                return ParseUnaryExpression(memberExpression, cachedMembers);

            return ParseCachedVariableExpression(memberExpression, cachedMembers);
        }

        private List<Expr> ParseCachedInvocationExpression(ExpressionSyntax expression, Dictionary<string, NodeType> cachedMembers) {
            //Currently, we treat 3rd party code as well as solution code methods within "if" test conditions the same
            var invocationExpression = (InvocationExpressionSyntax)expression;
            var className = invocationExpression
                .Expression.As<MemberAccessExpressionSyntax>()
                .Expression.As<IdentifierNameSyntax>()
                .Identifier.Text;
            var expr = typeService.TryGetType(className, out var _) ?
                ParseCachedStaticInvocationExpression(cachedMembers, invocationExpression) :
                ParseCachedReferenceInvocationExpression(cachedMembers, invocationExpression);

            return expr;
        }

        private List<Expr> ParseCachedReferenceInvocationExpression(Dictionary<string, NodeType> cachedMembers, InvocationExpressionSyntax invocationExpression) {
            bool isPure = typeService.IsPureMethod(invocationExpression, out var returnType);
            Sort sort = typeService.GetSort(returnType);

            if (!isPure) {
                Expr constExpr = context.MkConst(invocationExpression.ToString(), sort);
                return new List<Expr> { constExpr };
            }

            var instanceReference = new Reference(invocationExpression.Expression.As<MemberAccessExpressionSyntax>().Expression);
            var cachedInvocation = cachedMembers.FirstOrDefault(
                x => x.Value.Node is InvocationExpressionSyntax &&
                     reachabilityDelegate(new Reference(x.Value.Node.As<InvocationExpressionSyntax>().Expression.As<MemberAccessExpressionSyntax>().Expression), instanceReference, out var _));

            if (cachedInvocation.IsNull())
                return new List<Expr> { context.MkConst(invocationExpression.ToString(), sort) };

            var cachedArguments = cachedInvocation
                .Value.Node.As<InvocationExpressionSyntax>()
                .ArgumentList.Arguments
                .ToList();
            var arguments = invocationExpression.ArgumentList.Arguments.ToList();

            if (AreArgumentsEquivalent(cachedArguments, arguments))
                return new List<Expr> { cachedInvocation.Value.Expression };

            return new List<Expr> { context.MkConst(invocationExpression.ToString(), sort) };
        }

        private List<Expr> ParseCachedStaticInvocationExpression(Dictionary<string, NodeType> cachedMembers, InvocationExpressionSyntax invocationExpression) {
            bool isPure = typeService.IsPureMethod(invocationExpression, out var returnType);
            Sort sort = typeService.GetSort(returnType);
            var cachedInvocation = cachedMembers.FirstOrDefault(
                x => x.Value.Node is InvocationExpressionSyntax && x.Key == invocationExpression.Expression.ToString());

            if (!cachedInvocation.IsNull() && isPure) {
                var cachedArguments = cachedInvocation
                    .Value.Node.As<InvocationExpressionSyntax>()
                    .ArgumentList.Arguments
                    .ToList();
                var arguments = invocationExpression.ArgumentList.Arguments.ToList();

                if (AreArgumentsEquivalent(cachedArguments, arguments)) {
                    return new List<Expr> { cachedInvocation.Value.Expression };
                }
            }

            Expr expr = context.MkConst(invocationExpression.ToString(), sort);
            return new List<Expr> { expr };
        }

        private List<Expr> ParseUnaryExpression(ExpressionSyntax unaryExpression, Dictionary<string, NodeType> cachedMembers) {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)unaryExpression;
            var negatedExpressions = ParseExpressionMember(prefixUnaryExpression.Operand, cachedMembers);

            return negatedExpressions.Select(x => context.MkUnaryMinus((ArithExpr)x).As<Expr>()).ToList();
        }

        private List<Expr> ParseCachedVariableExpression(ExpressionSyntax memberExpression, Dictionary<string, NodeType> cachedMembers) {
            var memberType = typeService.GetTypeContainer(memberExpression).Type;
            var memberReference = new Reference(memberExpression);
            string memberName = memberExpression.ToString();
            string uniqueMemberName = $"{memberName}{memberExpression.GetLocation().SourceSpan}";
            List<Expr> reachableExprs = new List<Expr>();

            if (TryParseFixedExpression(memberExpression, memberType, out var expr)) {
                return new List<Expr> { expr };
            }

            foreach (NodeType node in cachedMembers.Values.Where(x => x.Type == memberType)) {
                if (node.IsExternal && node.ExternalReference.IsPure) {
                    if (MatchCachedExternalPureMethodAssignment(memberExpression, node, out Expr nodeExpression)) {
                        reachableExprs.Add(nodeExpression);
                    }
                } else if (node.Node is IdentifierNameSyntax && memberExpression is IdentifierNameSyntax &&
                           !node.IsExternal) {
                    if (reachabilityDelegate(new Reference(node.Node), memberReference, out Reference _)) {
                        reachableExprs.Add(node.Expression);
                    }
                } else if (node.Node is MemberAccessExpressionSyntax && memberExpression is MemberAccessExpressionSyntax) {
                    //TODO: MAJOR
                    //TODO: for now, we only match "amount1" with "amount2" (identifier with identifier) or "[from].AccountBalance" with "[from2].AccountBalance"
                    //TODO: need to extend to "amount" with "[from].AccountBalance" and other combinations
                    var firstMember = (MemberAccessExpressionSyntax)node.Node;
                    var secondMember = (MemberAccessExpressionSyntax)memberExpression;
                    var firstRootReference = new Reference(firstMember.GetRootIdentifier());
                    var secondRootReference = new Reference(secondMember.GetRootIdentifier());

                    if (reachabilityDelegate(firstRootReference, secondRootReference, out Reference _)) {
                        reachableExprs.Add(node.Expression);
                    }
                }
            }

            if (reachableExprs.Any())
                return reachableExprs;

            Sort sort = typeService.GetSort(memberType);
            expr = context.MkConst(uniqueMemberName, sort);
            reachableExprs.Add(expr);
            return reachableExprs;
        }

        private bool MatchCachedExternalPureMethodAssignment(ExpressionSyntax memberExpression, NodeType node, out Expr nodeExpression) {
            nodeExpression = null;
            var rootIdentifier = memberExpression is MemberAccessExpressionSyntax
                ? memberExpression.As<MemberAccessExpressionSyntax>().GetRootIdentifier()
                : memberExpression.As<IdentifierNameSyntax>();
            var invocationReference = getAssignmentsDelegate(rootIdentifier.Identifier)[0].RightReference.Node
                .As<InvocationExpressionSyntax>();
            var className = invocationReference
                .Expression.As<MemberAccessExpressionSyntax>()
                .Expression.As<IdentifierNameSyntax>()
                .Identifier.Text;
            var cachedArguments = node.ExternalReference
                .Node.As<InvocationExpressionSyntax>()
                .ArgumentList.Arguments
                .ToList();
            var arguments = invocationReference.ArgumentList.Arguments.ToList();

            if (typeService.TryGetType(className, out var _)) {
                if (AreArgumentsEquivalent(cachedArguments, arguments)) {
                    {
                        nodeExpression = node.Expression;
                        return true;
                    }
                }
            } else {
                var instanceReference = new Reference(invocationReference.Expression.As<MemberAccessExpressionSyntax>()
                    .Expression);
                var cachedReference = new Reference(node.ExternalReference.Node.As<InvocationExpressionSyntax>().Expression
                    .As<MemberAccessExpressionSyntax>().Expression);

                if (reachabilityDelegate(instanceReference, cachedReference, out var _) &&
                    AreArgumentsEquivalent(cachedArguments, arguments)) {
                    {
                        nodeExpression = node.Expression;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool AreArgumentsEquivalent(List<ArgumentSyntax> first, List<ArgumentSyntax> second) {
            if (first.Count != second.Count)
                return false;

            for (int i = 0; i < first.Count; i++) {
                //TODO: if we have here from1.Age and from2.Age => it will compare from1 and from2 only; in the case of from1.Name and from2.Address it will fail to compute the right values
                var firstReference = new Reference(first[i].Expression.GetRootIdentifier());
                var secondReference = new Reference(second[i].Expression.GetRootIdentifier());

                if (!reachabilityDelegate(firstReference, secondReference, out var _))
                    return false;
            }

            return true;
        }

        #endregion

        /// <summary>
        /// Parses enums or constants expressions.
        /// </summary>
        private bool TryParseFixedExpression(ExpressionSyntax memberExpression, Type type, out Expr fixedExpr) {
            string memberName = memberExpression.ToString();
            fixedExpr = null;

            if (type.IsEnum && memberName.StartsWith($"{type.Name}.")) {
                Sort sort = typeService.GetSort(type);
                fixedExpr = sort
                    .As<EnumSort>()
                    .Consts
                    .First(x => x.FuncDecl.Name.ToString() == memberExpression.As<MemberAccessExpressionSyntax>().Name.ToString());
                return true;
            }

            var containingClassName = memberName.Contains(".")
                ? memberExpression.As<MemberAccessExpressionSyntax>().Expression.ToString()
                : memberExpression.GetContainingClass().Identifier.Text;
            var constantName = memberName.Contains(".")
                ? memberExpression.As<MemberAccessExpressionSyntax>().Name.Identifier.Text
                : memberExpression.ToString();

            if (!typeService.TryGetType(containingClassName, out var containingType))
                return false;

            var constantField = containingType
                .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(x => x.Name == constantName && x.IsLiteral && !x.IsInitOnly);

            if (constantField == null)
                return false;

            var constantValue = constantField.GetRawConstantValue();

            if (constantField.FieldType.IsNumeric()) {
                fixedExpr = context.MkNumeral((int)constantValue, context.RealSort);
                return true;
            }

            if (constantField.FieldType.IsBoolean()) {
                fixedExpr = context.MkBool((bool)constantValue);
                return true;
            }

            if (constantField.FieldType.IsString()) {
                fixedExpr = context.MkString((string)constantValue);
                return true;
            }

            return false;
        }

        private Expr GetNullExpression(Type type) {
            var sort = typeService.GetSort(type);
            var nullConstructor = sort.As<DatatypeSort>().Constructors.First(x => x.Name.ToString() == "null");
            var nullExpr = context.MkConst(nullConstructor);

            return nullExpr;
        }

        private InvocationType GetInvocationType(InvocationExpressionSyntax invocationExpression)
        {
            if (invocationExpression.Expression is IdentifierNameSyntax)
                return GetLocalInvocationType(invocationExpression);

            return GetExternalInvocationType(invocationExpression);
        }

        /// <summary>
        /// Gets the invocation type of a method local to the current class.
        /// </summary>
        private InvocationType GetLocalInvocationType(InvocationExpressionSyntax invocationExpression) {
            var @class = invocationExpression.GetContainingClass();
            typeService.TryGetType(@class.Identifier.Text, out var type);
            var methodName = invocationExpression.Expression.ToString();
            var parametersCount = invocationExpression.ArgumentList.Arguments.Count;
            var method = type.GetMethods().First(x => x.Name == methodName &&
                                                      x.GetParameters().Length == parametersCount);
            var methodDeclaration = @class.DescendantNodes<MethodDeclarationSyntax>(x => x.Identifier.Text == methodName &&
                                                                                         x.ParameterList.Parameters.Count ==
                                                                                         parametersCount)
                .First();
            var invocationType = new InvocationType {
                MethodDeclaration = methodDeclaration
            };

            if (method.IsStatic) {
                invocationType.StaticType = type;
            } else {
                invocationType.InstanceType = type;
            }

            return invocationType;
        }

        /// <summary>
        /// Gets invocation type for invocation of a method external to the current type.
        /// </summary>
        private InvocationType GetExternalInvocationType(InvocationExpressionSyntax invocationExpression)
        {
            var instanceOrType = invocationExpression
                .Expression.As<MemberAccessExpressionSyntax>()
                .Expression.As<IdentifierNameSyntax>();
            var methodName = invocationExpression
                .Expression.As<MemberAccessExpressionSyntax>()
                .Name.As<IdentifierNameSyntax>()
                .Identifier.Text;
            var parametersCount = invocationExpression.ArgumentList.Arguments.Count;
            var invocationType = new InvocationType();

            if (!typeService.TryGetType(instanceOrType.Identifier.Text, out var type))
            {
                type = typeService.GetTypeContainer(instanceOrType).Type;
                invocationType.StaticType = type;
            }

            var classDeclaration = typeService.GetClassDeclaration(type);
            var methodDeclaration = classDeclaration.DescendantNodes<MethodDeclarationSyntax>(
                    x => x.Identifier.Text == methodName &&
                         x.ParameterList.Parameters.Count ==
                         parametersCount)
                .First();
            invocationType.MethodDeclaration = methodDeclaration;
            invocationType.InstanceType = invocationType.IsStatic ? null : type;
            invocationType.Instance = invocationType.IsStatic ? null : instanceOrType;

            return invocationType;
        }
    }
}