using System;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Prometheus.Extensions;

namespace Prometheus.Engine.UnitTests {
    public class Counter {
        private readonly object locker = new object();
        private int counter = 0;

        public void Add() {
            lock (locker) { counter++; }
        }
        public void Substract() {
            lock (locker) { counter--; }
        }
        public int Get() {
            return counter;
        }
    }

    [TestFixture]
    public class EngineTest {
        [Test]
        public void EngineTest_WithAtomicityAnalyzer() {
            var ws = new AdhocWorkspace();
            var project = ws.AddProject("Sample", "C#");
            ws.TryApplyChanges(project.Solution);
            string text = @"
                            public class C
                            {
                                private readonly object locker = new object();
                                private int counter = 0;

                                public void Add()
                                {
                                    lock(locker)
                                    { counter++; }
                                }
                                public void Substract()
                                {
                                    lock(locker)
                                    { counter--; }
                                }
                                public int Get()
                                {
                                    return counter;
                                }
                            }";
            var atomicAnalyzer = new AtomicAnalyzer();
             atomicAnalyzer.Analyze((Expression<Func<Counter, bool>>)(x=>x.IsModifiedAtomic("counter")), ws);
        }

        [Test]
        public void EngineTest_WithSortedLinkedListInvariant() {
        }

        [Test]
        public void EngineTest_AccountBalance_IsNotActive_CantCallTransfer() {
        }

        [Test]
        public void EngineTest_AccountBalance_Total_Equals_Debit_Plus_Credit() {
        }
    }
}
