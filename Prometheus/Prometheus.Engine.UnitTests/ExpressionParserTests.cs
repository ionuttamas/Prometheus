using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis.MSBuild;
using NUnit.Framework;
using Prometheus.Engine.Analyzer.Atomic;
using Prometheus.Engine.Model;
using Prometheus.Engine.Parser;

namespace Prometheus.Engine.UnitTests
{
    [TestFixture]
    public class ExpressionParserTests
    {
        private ExpressionParser expressionParser;

        [SetUp]
        public void Init()
        {
            expressionParser = new ExpressionParser();
        }

        [TearDown]
        public void TearDown()
        {
            expressionParser = null;
        }

        [TestCaseSource(nameof(SingleTypeExpressions))]
        public void ExpressionParser_ForSingleTypeExpression_ParsesAtomicInvariants(Expression expression, int atomicInvariantCount)
        {
            var result = expressionParser.Parse(expression);
            Assert.AreEqual(atomicInvariantCount, result.Count(x=>x is AtomicInvariant));
        }

        [TestCaseSource(nameof(MultipleTypesExpressions))]
        public void ExpressionParser_ForMultipleTypesExpression_ParsesAtomicInvariants(Expression expression, int atomicInvariantCount)
        {
            var result = expressionParser.Parse(expression);
            Assert.AreEqual(atomicInvariantCount, result.Count(x => x is AtomicInvariant));
        }

        private static readonly object[] SingleTypeExpressions =
        {
            new object[] {(Expression<Func<Account, bool>>)(x => x.Balance.IsModifiedAtomic()), 1},
            new object[] {(Expression<Func<Account, bool>>)(x => x.Balance.IsModifiedAtomic() && x.IsCalledOnce(nameof(x.Transfer)) && x.IsCalledOnce("")), 1},
            new object[] {(Expression<Func<Account, bool>>)(x => x.Balance.IsModifiedAtomic() && x.Id.IsModifiedAtomic()), 2},
            new object[] {(Expression<Func<Account, bool>>)(x => x.Balance.IsModifiedAtomic() && x.IsModifiedAtomic(nameof(x.Id))), 2},
            new object[] {(Expression<Func<Account, bool>>)(x => x.Balance.IsModifiedAtomic() && x.IsModifiedAtomic("SecretProperty")), 2},
            new object[] {(Expression<Func<Account, bool>>)(x => x.Balance.IsModifiedAtomic() && x.IsModifiedAtomic("secretField")), 2},
        };

        private static readonly object[] MultipleTypesExpressions =
        {
            new object[] {(Expression<Func<Account, Invoice, bool>>)((account, invoice) => account.Balance.IsModifiedAtomic()), 1},
            new object[] {(Expression<Func<Account, Invoice, bool>>)((account, invoice) => account.Balance.IsModifiedAtomic() && invoice.IsCalledOnce(nameof(invoice.Create))), 1},
            new object[] {(Expression<Func<Account, Invoice, bool>>)((account, invoice) => account.Balance.IsModifiedAtomic() && invoice.Amount.IsModifiedAtomic()), 2},
            new object[] {(Expression<Func<Account, Invoice, bool>>)((account, invoice) => account.Balance.IsModifiedAtomic() && invoice.IsModifiedAtomic("RefId")), 2},
            new object[] {(Expression<Func<Account, Invoice, bool>>)((account, invoice) => account.Balance.IsModifiedAtomic() && invoice.IsModifiedAtomic("RefId")), 2},
            new object[] {(Expression<Func<Account, Invoice, bool>>)((account, invoice) => account.Balance.IsModifiedAtomic() && invoice.IsCalledOnce(nameof(invoice.Create)) && invoice.IsModifiedAtomic("RefId")), 2},
            new object[] {(Expression<Func<Account, Invoice, bool>>)((account, invoice) => account.Balance.IsModifiedAtomic() && invoice.Customer.IsModifiedAtomic() && invoice.IsModifiedAtomic("RefId")), 3}
        };

        private class Account
        {
            private int SecretProperty { get; set; }
            private int secretField;
            public int Id { get; set; }
            public decimal Balance { get; set; }

            public void Initialize()
            {
            }

            public void Transfer(int to, decimal amount)
            {
            }
        }

        private class Invoice
        {
            private string RefId { get; set; }
            public string Customer { get; set; }
            public string Amount { get; set; }

            public void Create()
            {
            }
        }
    }
}