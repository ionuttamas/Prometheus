using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using Prometheus.Common;
using Prometheus.Engine.Reachability.Prover;
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
        private Func<MethodDeclarationSyntax, DEQueue<ReferenceContext>, Dictionary<string, NodeType>, BoolExpr> parseCachedBooleanMethodDelegate;
        private TryGetUniqueAssignment tryGetUniqueAssignmentDelegate;
        private readonly Context context;
        private readonly Dictionary<Expr, List<Expr>> reachableExprsTable;
        private readonly Dictionary<Expr, List<Expr>> nonReachableExprsTable;
        private readonly Dictionary<ExpressionSyntax, Expr> cachedProcessedExprsTable;

        public Z3BooleanExpressionParser(ITypeService typeService, IReferenceParser referenceParser, Context context) {
            this.typeService = typeService;
            this.referenceParser = referenceParser;
            this.context = context;

            reachableExprsTable = new Dictionary<Expr, List<Expr>>();
            nonReachableExprsTable = new Dictionary<Expr, List<Expr>>();
            cachedProcessedExprsTable = new Dictionary<ExpressionSyntax, Expr>();
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

        public void Configure(ParseCachedBooleanMethod @delegate)
        {
            parseCachedBooleanMethodDelegate = (method, contexts, cachedNodes) => @delegate(method, contexts, cachedNodes);
        }

        public void Configure(TryGetUniqueAssignment @delegate) {
            tryGetUniqueAssignmentDelegate = @delegate;
        }

        public BoolExpr ParseExpression(ExpressionSyntax expressionSyntax, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedMembers) {
            var expressionKind = expressionSyntax.Kind();
            processedMembers = new Dictionary<string, NodeType>();

            switch (expressionKind) {
                case SyntaxKind.TrueLiteralExpression:
                    return context.MkTrue();
                case SyntaxKind.FalseLiteralExpression:
                    return context.MkFalse();
                case SyntaxKind.LogicalNotExpression:
                    return ParsePrefixUnaryExpression(expressionSyntax.As<PrefixUnaryExpressionSyntax>(), contexts, out processedMembers);
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
                    return ParseBinaryExpression(binaryExpression, contexts, out processedMembers);
                default:
                    throw new NotImplementedException();
            }

        }

        public List<BoolExpr> ParseCachedExpression(ExpressionSyntax expressionSyntax, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedMembers)
        {
            var rawExpr = ParseRawCachedExpression(expressionSyntax, contexts, cachedMembers);
            var exprs = new List<BoolExpr>();
            var sets = reachableExprsTable
                .Select(x => (x.Key, x.Value.Count == 0 ? new List<Expr> {x.Key} : x.Value))
                .ToList();
            var exprsCombinations = sets
                .Select(x => x.Item2)
                .CartesianProduct()
                .Select(x => x.ToList())
                .ToList();
            var keys = sets.Select(x => x.Item1).ToList();

            foreach (var combination in exprsCombinations)
            {
                BoolExpr processedExpr = rawExpr;

                for (int i = 0; i < combination.Count; i++)
                {
                    var rawMemberExpr = keys[i];
                    var reachableConstraint = rawMemberExpr==combination[i] ? context.MkTrue() : context.MkEq(rawMemberExpr, combination[i]);
                    var nonReachableConstraint = context.MkTrue();

                    if (nonReachableExprsTable.ContainsKey(rawMemberExpr))
                    {
                        var nonReachableExprs = nonReachableExprsTable[rawMemberExpr].Where(x => x!=null);
                        nonReachableConstraint = context.MkAnd(nonReachableExprs.Select(x => context.MkNot(context.MkEq(x, rawMemberExpr))));
                    }

                    processedExpr = context.MkAnd(processedExpr, reachableConstraint, nonReachableConstraint);
                }

                exprs.Add(processedExpr);
            }

            //reachableExprsTable.Clear();
            //nonReachableExprsTable.Clear();
            //cachedProcessedExprsTable.Clear();

            return exprs;
        }

        /// <summary>
        /// This method returns the raw, unmatched cached Z3 expressions.
        /// For instance, for "a > b & c == 3" and cached expression "x==y && z > 10", even if we have some equivalences between {a,b,c} and {x,y,z},
        /// it will return the "a > b & c == 3" expression, but while parsing it, it will store in the reachableExprsTable and nonReachableExprsTable the equivalent expressions.
        /// </summary>
        public BoolExpr ParseRawCachedExpression(ExpressionSyntax expressionSyntax, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedMembers) {
            var expressionKind = expressionSyntax.Kind();
            BoolExpr result;

            switch (expressionKind) {
                case SyntaxKind.TrueLiteralExpression:
                    result = context.MkTrue();
                    break;
                case SyntaxKind.FalseLiteralExpression:
                    result = context.MkFalse();
                    break;
                case SyntaxKind.LogicalNotExpression:
                    result = ParseCachedPrefixUnaryExpression((PrefixUnaryExpressionSyntax)expressionSyntax, contexts, cachedMembers);
                    break;
                //TODO: is this captured by other cases?
                case SyntaxKind.SimpleMemberAccessExpression:
                    result = context.MkBoolConst(expressionSyntax.ToString());
                    break;
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                    result = ParseCachedBinaryExpression(expressionSyntax.As<BinaryExpressionSyntax>(), contexts, cachedMembers);
                    break;
                default:
                    throw new NotImplementedException();
            }

            return result;
        }

        #region Non-cached processing

        private BoolExpr ParseBinaryExpression(BinaryExpressionSyntax binaryExpression, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedMembers) {
            SyntaxKind expressionKind = binaryExpression.Kind();
            var leftProcessedMembers = new Dictionary<string, NodeType>();
            var rightProcessedMembers = new Dictionary<string, NodeType>();
            Expr left;
            Expr right;

            if (binaryExpression.Left.Kind() == SyntaxKind.NullLiteralExpression) {
                left = GetNullExpression(typeService.GetTypeContainer(binaryExpression.Right).Type);
                right = ParseExpressionMember(binaryExpression.Right, contexts, out rightProcessedMembers);
            } else if (binaryExpression.Right.Kind() == SyntaxKind.NullLiteralExpression) {
                right = GetNullExpression(typeService.GetTypeContainer(binaryExpression.Left).Type);
                left = ParseExpressionMember(binaryExpression.Left, contexts, out leftProcessedMembers);
            } else {
                left = ParseExpressionMember(binaryExpression.Left, contexts, out leftProcessedMembers);
                right = ParseExpressionMember(binaryExpression.Right, contexts, out rightProcessedMembers);
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

        private BoolExpr ParsePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedMembers) {
            if (prefixUnaryExpression.Kind() != SyntaxKind.LogicalNotExpression)
                throw new NotImplementedException();

            var innerExpression = prefixUnaryExpression.Operand;
            var innerExpressionKind = innerExpression.Kind();

            if (innerExpressionKind == SyntaxKind.SimpleMemberAccessExpression) {
                var parsedExpression = ParseExpression(innerExpression, contexts, out processedMembers);
                return context.MkNot(parsedExpression);
            }

            if (innerExpressionKind == SyntaxKind.InvocationExpression) {
                typeService.IsPureMethod(innerExpression.As<InvocationExpressionSyntax>(), out var returnType);

                if (returnType == typeof(bool)) {
                    var parsedExpression = (BoolExpr)ParseInvocationExpression(innerExpression, contexts, out processedMembers);
                    return context.MkNot(parsedExpression);
                }
            }

            throw new InvalidOperationException($"PrefixUnaryExpression {prefixUnaryExpression} could not be processed");
        }

        private Expr ParseExpressionMember(ExpressionSyntax memberExpression, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedMembers) {
            var expressionKind = memberExpression.Kind();
            processedMembers = new Dictionary<string, NodeType>();

            if (memberExpression is BinaryExpressionSyntax)
                return ParseBinaryExpression((BinaryExpressionSyntax)memberExpression, contexts, out processedMembers);

            if (expressionKind == SyntaxKind.NumericLiteralExpression)
                return ParseNumericLiteral(memberExpression.ToString());

            if (expressionKind == SyntaxKind.StringLiteralExpression)
                return ParseStringLiteral(memberExpression.As<LiteralExpressionSyntax>().Token.ValueText);

            if (expressionKind == SyntaxKind.InvocationExpression)
                return ParseInvocationExpression(memberExpression, contexts, out processedMembers);

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName) {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseUnaryExpression(memberExpression, contexts, out processedMembers)
                    : ParseExpression(memberExpression, contexts, out processedMembers);
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression)
                return ParseUnaryExpression(memberExpression, contexts, out processedMembers);

            var memberType = typeService.GetTypeContainer(memberExpression).Type;

            //TODO: check nested reference chains from different chains: "customer.Address.ShipInfo" & "order.ShipInfo" to be the same
            //TODO: check agains same reference chains: "from.Address.ShipInfo" & "to.Address.ShipInfo"
            //Check against the nodes from already parsed the first conditional assignment
            return ParseVariableExpression(memberExpression, contexts, memberType, processedMembers);
        }

        private Expr ParseInvocationExpression(ExpressionSyntax expression, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedMembers) {
            var invocationExpression = (InvocationExpressionSyntax)expression;
            var invocationType = GetInvocationType(invocationExpression);

            return typeService.Is3rdParty(invocationType.Type) ?
                Parse3rdPartyCodeInvocationExpression(invocationExpression, contexts, out processedMembers) :
                ParseInternalCodeInvocationExpression(invocationExpression, contexts, invocationType, out processedMembers);
        }

        private Expr Parse3rdPartyCodeInvocationExpression(InvocationExpressionSyntax invocationExpression, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedMembers) {
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

        private Expr ParseInternalCodeInvocationExpression(InvocationExpressionSyntax invocationExpression, DEQueue<ReferenceContext> contexts, InvocationType invocationType, out Dictionary<string, NodeType> processedMembers) {
            //TODO: need to pass instance as reference
            referenceParser.GetMethodBindings(invocationExpression, invocationType.MethodDeclaration.GetContainingClass(), invocationType.MethodDeclaration.Identifier.Text, out var argumentsTable);
            var callContext = new CallContext {
                InstanceNode = invocationType.Instance,
                ArgumentsTable = argumentsTable,
                InvocationExpression = invocationExpression
            };
            var referenceContext = new ReferenceContext(callContext);
            contexts.Prepend(referenceContext);
            var expr = parseBooleanMethodDelegate(invocationType.MethodDeclaration, contexts, out processedMembers);
            contexts.DeleteLast();

            foreach (var processedMember in processedMembers)
            {
                processedMember.Value.Reference = processedMember.Value.Reference ?? new Reference(processedMember.Value.Node);
                processedMember.Value.Reference.PrependContext(referenceContext);
            }

            return expr;
        }

        private Expr ParseUnaryExpression(ExpressionSyntax unaryExpression, DEQueue<ReferenceContext> contexts, out Dictionary<string, NodeType> processedMembers) {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)unaryExpression;
            var negatedExpression = ParseExpressionMember(prefixUnaryExpression.Operand, contexts, out processedMembers);

            return context.MkUnaryMinus((ArithExpr)negatedExpression);
        }

        private Expr ParseVariableExpression(ExpressionSyntax memberExpression, DEQueue<ReferenceContext> contexts, Type type, Dictionary<string, NodeType> processedMembers) {
            string memberName = memberExpression.ToString();
            var locationSpan = memberExpression.GetLocation().SourceSpan;
            string uniqueMemberName = $"{memberName}[{locationSpan.Start}..{locationSpan.End}]";
            Expr expr;
            bool is3rdParty = false;
            Reference thirdPartyReference = null;
            Reference uniqueReference = null;

            if (!TryParseFixedExpression(memberExpression, type, out expr)) {
                Sort sort = typeService.GetSort(type);
                expr = context.MkConst(uniqueMemberName, sort);
                var rootIdentifier = memberExpression.GetRootIdentifier();
                var rootType = typeService.GetTypeContainer(rootIdentifier).Type;
                var reference = new Reference(rootIdentifier) {ReferenceContexts = contexts};
                //TODO: https://github.com/ionuttamas/Prometheus/issues/25
                var firstAssignment = getAssignmentsDelegate(reference).FirstOrDefault();

                if (firstAssignment != null && firstAssignment.RightReference.Is3rdParty || typeService.Is3rdParty(rootType)) {
                    is3rdParty = true;
                    //todo: currently we only take the first assignments of the rootIdentifier to see if is pure or not, regardless of any conditions
                    thirdPartyReference = firstAssignment.RightReference;
                }

                tryGetUniqueAssignmentDelegate(reference, out uniqueReference);
            }

            if (type.IsSimple() && uniqueReference != null && TryParseSimpleTypeLiteral(uniqueReference.Node, out var literalExpr))
            {
                expr = literalExpr;
            }

            processedMembers[memberName] = new NodeType {
                Expression = expr,
                Node = memberExpression,
                Type = type,
                Is3rdParty = is3rdParty,
                Reference = thirdPartyReference ?? new Reference(memberExpression)
            };



            return expr;
        }

        private bool TryParseSimpleTypeLiteral(SyntaxNode node, out Expr expr)
        {
            expr = null;

            if (node.Kind() == SyntaxKind.NumericLiteralExpression)
            {
                expr = ParseNumericLiteral(node.ToString());
                return true;
            }

            if (node.Kind() == SyntaxKind.StringLiteralExpression)
            {
                expr = ParseStringLiteral(node.ToString());
                return true;
            }

            if (node.Kind() == SyntaxKind.FalseLiteralExpression)
            {
                expr = context.MkFalse();
                return true;
            }

            if (node.Kind() == SyntaxKind.TrueLiteralExpression) {
                expr = context.MkTrue();
                return true;
            }

            return false;
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

        private BoolExpr ParseCachedBinaryExpression(BinaryExpressionSyntax binaryExpression, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedMembers) {
            SyntaxKind expressionKind = binaryExpression.Kind();
            Expr left;
            Expr right;

            if (binaryExpression.Left.Kind() == SyntaxKind.NullLiteralExpression) {
                left = GetNullExpression(typeService.GetTypeContainer(binaryExpression.Right).Type);
                right = ParseCachedExpressionMember(binaryExpression.Right, contexts, cachedMembers);
            } else if (binaryExpression.Right.Kind() == SyntaxKind.NullLiteralExpression) {
                right = GetNullExpression(typeService.GetTypeContainer(binaryExpression.Left).Type);
                left = ParseCachedExpressionMember(binaryExpression.Left, contexts, cachedMembers);
            } else {
                left = ParseCachedExpressionMember(binaryExpression.Left, contexts, cachedMembers);
                right = ParseCachedExpressionMember(binaryExpression.Right, contexts, cachedMembers);
            }

            switch (expressionKind) {
                case SyntaxKind.LogicalAndExpression:
                    return context.MkAnd(left.As<BoolExpr>(), right.As<BoolExpr>());
                case SyntaxKind.LogicalOrExpression:
                    return context.MkOr(left.As<BoolExpr>(), right.As<BoolExpr>());
                case SyntaxKind.GreaterThanExpression:
                    return context.MkGt(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.GreaterThanOrEqualExpression:
                    return context.MkGe(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.LessThanExpression:
                    return context.MkLt(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.LessThanOrEqualExpression:
                    return context.MkLe(left.As<ArithExpr>(), right.As<ArithExpr>());
                case SyntaxKind.EqualsExpression:
                    return context.MkEq(left.As<Expr>(), right.As<Expr>());
                case SyntaxKind.NotEqualsExpression:
                    return context.MkNot(context.MkEq(left.As<Expr>(), right.As<Expr>()));
                default:
                    throw new NotImplementedException();
            }
        }

        private BoolExpr ParseCachedPrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnaryExpression, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedMembers) {
            if (prefixUnaryExpression.Kind() != SyntaxKind.LogicalNotExpression)
                throw new NotImplementedException();

            var innerExpression = prefixUnaryExpression.Operand;
            var innerExpressionKind = innerExpression.Kind();

            if (innerExpressionKind == SyntaxKind.SimpleMemberAccessExpression || innerExpressionKind == SyntaxKind.IdentifierName) {
                var parsedExpression = ParseCachedExpressionMember(innerExpression, contexts, cachedMembers);
                return context.MkNot(parsedExpression.As<BoolExpr>());
            }

            if (innerExpressionKind == SyntaxKind.InvocationExpression) {
                typeService.IsPureMethod(innerExpression.As<InvocationExpressionSyntax>(), out var returnType);

                if (returnType == typeof(bool)) {
                    var parsedExpression = ParseCachedInvocationExpression(innerExpression, contexts, cachedMembers);
                    return context.MkNot(parsedExpression.As<BoolExpr>());
                }
            }

            throw new InvalidOperationException($"PrefixUnaryExpression {prefixUnaryExpression} could not be processed");
        }

        private Expr ParseCachedExpressionMember(ExpressionSyntax memberExpression, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedMembers) {
            var expressionKind = memberExpression.Kind();

            if (memberExpression is BinaryExpressionSyntax)
                return ParseCachedBinaryExpression((BinaryExpressionSyntax)memberExpression, contexts, cachedMembers);

            if (expressionKind == SyntaxKind.NumericLiteralExpression)
                return ParseNumericLiteral(memberExpression.ToString());

            if (expressionKind == SyntaxKind.StringLiteralExpression)
                return ParseStringLiteral(memberExpression.As<LiteralExpressionSyntax>().Token.ValueText);

            if (expressionKind == SyntaxKind.InvocationExpression)
                return ParseCachedInvocationExpression(memberExpression, contexts, cachedMembers);

            if (expressionKind != SyntaxKind.SimpleMemberAccessExpression && expressionKind != SyntaxKind.IdentifierName) {
                return expressionKind == SyntaxKind.UnaryMinusExpression
                    ? ParseCachedUnaryExpression(memberExpression, contexts, cachedMembers)
                    : ParseRawCachedExpression(memberExpression, contexts, cachedMembers);
            }

            if (expressionKind == SyntaxKind.UnaryMinusExpression)
                return ParseCachedUnaryExpression(memberExpression, contexts, cachedMembers);

            return ParseCachedVariableExpression(memberExpression, contexts, cachedMembers);
        }

        private Expr ParseCachedInvocationExpression(ExpressionSyntax expression, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedMembers) {
            //Currently, we treat 3rd party code as well as solution code methods within "if" test conditions the same
            var invocationExpression = (InvocationExpressionSyntax)expression;
            var invocationType = GetInvocationType(invocationExpression);

            return typeService.Is3rdParty(invocationType.Type) ?
                ParseCached3rdPartyCodeInvocationExpression(invocationType, cachedMembers) :
                ParseCachedInternalCodeInvocationExpression(invocationType, contexts, cachedMembers);
        }

        private Expr ParseCached3rdPartyCodeInvocationExpression(InvocationType invocationType, Dictionary<string, NodeType> cachedMembers) {
            var expr = invocationType.IsStatic ?
                ParseCachedStaticInvocationExpression(cachedMembers, invocationType.Expression) :
                ParseCachedReferenceInvocationExpression(cachedMembers, invocationType.Expression);

            return expr;
        }

        private Expr ParseCachedInternalCodeInvocationExpression(InvocationType invocationType, DEQueue<ReferenceContext> oldContexts, Dictionary<string, NodeType> cachedMembers) {
            referenceParser.GetMethodBindings(invocationType.Expression, invocationType.MethodDeclaration.GetContainingClass(), invocationType.MethodDeclaration.Identifier.Text, out var argumentsTable);
            var callContext = new CallContext {
                InstanceNode = invocationType.Instance,
                ArgumentsTable = argumentsTable,
                InvocationExpression = invocationType.Expression
            };
            var referenceContext = new ReferenceContext(callContext);
            var contexts = new DEQueue<ReferenceContext>(referenceContext);

            foreach (var oldContext in oldContexts.ToList())
            {
                contexts.Append(oldContext);
            }

            var reachableExprs = parseCachedBooleanMethodDelegate(invocationType.MethodDeclaration, contexts, cachedMembers);

            return reachableExprs;
        }

        private Expr ParseCachedReferenceInvocationExpression(Dictionary<string, NodeType> cachedMembers, InvocationExpressionSyntax invocationExpression) {
            bool isPure = typeService.IsPureMethod(invocationExpression, out var returnType);
            Sort sort = typeService.GetSort(returnType);

            if (!isPure) {
                //todo: this is a bit too simplistic
                Expr constExpr = context.MkConst(invocationExpression.ToString(), sort);
                return constExpr;
            }

            var instanceReference = new Reference(invocationExpression.Expression.As<MemberAccessExpressionSyntax>().Expression);
            var cachedInvocation = cachedMembers.FirstOrDefault(
                x => x.Value.Node is InvocationExpressionSyntax &&
                     reachabilityDelegate(new Reference(x.Value.Node.As<InvocationExpressionSyntax>().Expression.As<MemberAccessExpressionSyntax>().Expression), instanceReference, out var _));

            if (cachedInvocation.IsNull())
                return context.MkConst(invocationExpression.ToString(), sort);

            var cachedArguments = cachedInvocation.Value
                .Node.As<InvocationExpressionSyntax>()
                .ArgumentList;
            var arguments = invocationExpression.ArgumentList;

            if (AreArgumentsEquivalent(cachedArguments, arguments))
                return cachedInvocation.Value.Expression;

            return context.MkConst(invocationExpression.ToString(), sort);
        }

        private Expr ParseCachedStaticInvocationExpression(Dictionary<string, NodeType> cachedMembers, InvocationExpressionSyntax invocationExpression) {
            bool isPure = typeService.IsPureMethod(invocationExpression, out var returnType);
            Sort sort = typeService.GetSort(returnType);
            var cachedInvocation = cachedMembers.FirstOrDefault(
                x => x.Value.Node is InvocationExpressionSyntax && x.Key == invocationExpression.Expression.ToString());

            if (!cachedInvocation.IsNull() && isPure) {
                var cachedArguments = cachedInvocation.Value
                    .Node.As<InvocationExpressionSyntax>().ArgumentList;
                var arguments = invocationExpression.ArgumentList;

                if (AreArgumentsEquivalent(cachedArguments, arguments)) {
                    return cachedInvocation.Value.Expression;
                }
            }

            Expr expr = context.MkConst(invocationExpression.ToString(), sort);
            return expr;
        }

        private Expr  ParseCachedUnaryExpression(ExpressionSyntax unaryExpression, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedMembers) {
            var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)unaryExpression;
            var expression  = ParseCachedExpressionMember(prefixUnaryExpression.Operand, contexts, cachedMembers);

            return context.MkUnaryMinus((ArithExpr)expression).As<Expr>();
        }

        /// <summary>
        /// Returns both the expression and sets the global reachable expressions and the of non reachable expressions tables.
        /// E.g. for (memberExpression = a, cachedNodes = {x,y,z} - all of the same type), if we find equivalences (a ≡ x), but not with {y,z}, we will add to (reachableExprsTable - {x}, nonReachableExprsTable - {y, z}).
        /// E.g. for (memberExpression = a, cachedNodes = {x,y,z} - all of the same type), if we find equivalences (a ≡ x) or (a ≡ y), but not with {z}, we will add to (reachableExprsTable - {x, y}, nonReachableExprsTable - {z}).
        /// </summary>
        private Expr ParseCachedVariableExpression(ExpressionSyntax memberExpression, DEQueue<ReferenceContext> contexts, Dictionary<string, NodeType> cachedMembers) {
            // When processing the same memberExpression from the same "if" condition twice, we will generate only one unique member and cache it
            var memberType = typeService.GetTypeContainer(memberExpression).Type;
            var memberReference = new Reference(memberExpression, contexts);
            string memberName = memberExpression.ToString();
            string randomToken = Guid.NewGuid().ToString("n").Substring(0, 8);
            var locationSpan = memberExpression.GetLocation().SourceSpan;
            var containingMethod = memberExpression.GetContainingMethod();
            var processedMember = cachedProcessedExprsTable.FirstOrDefault(x => x.Key.GetContainingMethod() == containingMethod && x.Key.ToString()==memberName);

            if (!processedMember.Equals(default(KeyValuePair<ExpressionSyntax, Expr>)))
                return processedMember.Value;

            string uniqueMemberName = $"{memberName}[{locationSpan.Start}..{locationSpan.End}]{randomToken}";
            Sort sort = typeService.GetSort(memberType);
            Expr expr = context.MkConst(uniqueMemberName, sort);
            cachedProcessedExprsTable[memberExpression] = expr;
            reachableExprsTable[expr] = new List<Expr>();
            nonReachableExprsTable[expr] = new List<Expr>();

            if (memberType.IsSimple() &&
                tryGetUniqueAssignmentDelegate(memberReference, out var uniqueReference) &&
                TryParseSimpleTypeLiteral(uniqueReference.Node, out var literalExpr))
            {
                cachedProcessedExprsTable[memberExpression] = literalExpr;
                return literalExpr;
            }

            if (TryParseFixedExpression(memberExpression, memberType, out var fixedExpr))
            {
                reachableExprsTable[expr] = new List<Expr> { fixedExpr };
                return expr;
            }

            foreach (NodeType cachedNode in cachedMembers.Values.Where(x => x.Type == memberType))
            {
                var matchesCachedExpr = MatchesCachedNode(memberReference, cachedNode, out var cachedExpr);

                if (matchesCachedExpr)
                {
                    reachableExprsTable[expr].Add(cachedExpr);
                }
                else
                {
                    //TODO: we should also add different types expressions as constraints
                    nonReachableExprsTable[expr].Add(cachedExpr);
                }
            }

            return  expr;
        }

        private bool MatchesCachedNode(Reference memberReference, NodeType cachedNode, out Expr expr)
        {
            ExpressionSyntax memberExpression = memberReference.Node.As<ExpressionSyntax>();
            expr = null;

            if (cachedNode.Is3rdParty && cachedNode.Reference.IsPure)
            {
                if (TryMatchCached3rdPartyPureMethodAssignment(memberExpression, cachedNode, out expr))
                {
                    return true;
                }

                //todo: see if we need to treat this case as well
                return false;
            }

            if (!cachedNode.Is3rdParty && cachedNode.Node is IdentifierNameSyntax && memberExpression is IdentifierNameSyntax)
            {
                expr = cachedNode.Expression;
                return reachabilityDelegate(memberReference, cachedNode.Reference, out Reference _);
            }

            if (cachedNode.Node is MemberAccessExpressionSyntax && memberExpression is MemberAccessExpressionSyntax)
            {
                //TODO: MAJOR
                //TODO: for now, we only match "amount1" with "amount2" (identifier with identifier) or "[from].AccountBalance" with "[from2].AccountBalance"
                //TODO: need to extend to "amount" with "[from].AccountBalance" and other combinations
                var firstMember = (MemberAccessExpressionSyntax) cachedNode.Node;
                var secondMember = (MemberAccessExpressionSyntax) memberExpression;
                var firstRootReference = new Reference(firstMember.GetRootIdentifier());
                var secondRootReference = new Reference(secondMember.GetRootIdentifier());

                expr = cachedNode.Expression;
                return reachabilityDelegate(firstRootReference, secondRootReference, out Reference _);
            }

            return false;
        }

        private bool TryMatchCached3rdPartyPureMethodAssignment(ExpressionSyntax memberExpression, NodeType node, out Expr nodeExpression) {
            nodeExpression = null;
            var rootIdentifier = memberExpression is MemberAccessExpressionSyntax
                ? memberExpression.As<MemberAccessExpressionSyntax>().GetRootIdentifier()
                : memberExpression.As<IdentifierNameSyntax>();
            var reference = new Reference(rootIdentifier);
            var invocationReference = getAssignmentsDelegate(reference)[0].RightReference.Node
                .As<InvocationExpressionSyntax>();
            var className = invocationReference
                .Expression.As<MemberAccessExpressionSyntax>()
                .Expression.As<IdentifierNameSyntax>()
                .Identifier.Text;
            var cachedArguments = node.Reference
                .Node.As<InvocationExpressionSyntax>().ArgumentList;
            var arguments = invocationReference.ArgumentList;

            // Process static method calls
            if (typeService.TryGetType(className, out var _) && AreArgumentsEquivalent(cachedArguments, arguments)) {
                nodeExpression = node.Expression;
                return true;
            }

            // Process instance method calls like "instanceReference.Do(...)"
            var instanceReference = new Reference(invocationReference.Expression.As<MemberAccessExpressionSyntax>().Expression);
            var cachedReference = new Reference(node.Reference.Node.As<InvocationExpressionSyntax>().Expression.As<MemberAccessExpressionSyntax>().Expression);

            if (reachabilityDelegate(instanceReference, cachedReference, out var _) && AreArgumentsEquivalent(cachedArguments, arguments))
            {
                nodeExpression = node.Expression;
                return true;
            }

            return false;
        }

        private bool AreArgumentsEquivalent(ArgumentListSyntax firstSyntax, ArgumentListSyntax secondSyntax)
        {
            if (firstSyntax.Arguments.Count != secondSyntax.Arguments.Count)
                return false;

            for (int i = 0; i < firstSyntax.Arguments.Count; i++) {
                //TODO: if we have here from1.Age and from2.Age => it will compare from1 and from2 only; in the case of from1.Name and from2.Address it will fail to compute the right values
                var firstReference = new Reference(firstSyntax.Arguments[i].Expression.GetRootIdentifier());
                var secondReference = new Reference(secondSyntax.Arguments[i].Expression.GetRootIdentifier());

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
            var method = type.GetMethod(x => x.Name == methodName && x.GetParameters().Length == parametersCount);
            var methodDeclaration = @class.DescendantNodes<MethodDeclarationSyntax>(x => x.Identifier.Text == methodName &&
                                                                                         x.ParameterList.Parameters.Count ==
                                                                                         parametersCount)
                .First();
            var invocationType = new InvocationType {
                Expression = invocationExpression,
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
            }
            else
            {
                invocationType.StaticType = type;
            }

            var is3rdParty = typeService.Is3rdParty(type);

            if (!is3rdParty)
            {
                var classDeclaration = typeService.GetClassDeclaration(type);
                MethodDeclarationSyntax methodDeclaration = classDeclaration.DescendantNodes<MethodDeclarationSyntax>(
                        x => x.Identifier.Text == methodName &&
                             x.ParameterList.Parameters.Count ==
                             parametersCount)
                    .First();
                invocationType.MethodDeclaration = methodDeclaration;
            }

            invocationType.Expression = invocationExpression;
            invocationType.InstanceType = invocationType.IsStatic ? null : type;
            invocationType.Instance = invocationType.IsStatic ? null : instanceOrType;

            return invocationType;
        }
    }
}