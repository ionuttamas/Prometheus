using Microsoft.Z3;
using NUnit.Framework;

namespace Prometheus.Engine.UnitTests
{
    [TestFixture]
    public class Z3Tests
    {
        private Context context;

        [SetUp]
        public void Init() {
            context = new Context();
        }

        [TearDown]
        public void TearDown() {
            context = null;
        }

        [Test]
        public void Z3Context_ForSameNameVariables_ProducesSameVariables()
        {
            Expr firstBoolean = context.MkConst("boolVar", context.BoolSort);
            Expr secondBoolean = context.MkConst("boolVar", context.BoolSort);
            BoolExpr equalityExpression = context.MkEq(firstBoolean, secondBoolean);
            Quantifier forAllQuantifier = context.MkForall(new[] {firstBoolean, secondBoolean}, equalityExpression);
            Solver solver = context.MkSolver();
            solver.Assert(forAllQuantifier);
            Status status = solver.Check();
            Assert.AreEqual(Status.SATISFIABLE, status);
        }/*

        [Test]
        public void Z3Context_ForSameNameVariables_ProducesSameVariables2() {
            Expr firstBoolean = context.MkConst("boolVar", context.BoolSort);
            Expr secondBoolean = context.MkConst("boolVar", context.BoolSort);
            BoolExpr equalityExpression = context.MkEq(firstBoolean, secondBoolean);
            BoolExpr equalityExpression2 = context.MkEq(firstBoolean, context.MkTrue());
            BoolExpr equalityExpression3 = context.MkEq(secondBoolean, context.MkTrue());

            BoolExpr expression = context.MkAnd(equalityExpression, equalityExpression2, equalityExpression3);

            Quantifier forAllQuantifier = context.MkForall(new[] { firstBoolean, secondBoolean }, equalityExpression);
            Solver solver = context.MkSolver();
            solver.Assert(expression);
            Status status = solver.Check();
            Assert.AreEqual(Status.SATISFIABLE, status);
        }*/

        [Test]
        public void Z3Context_ForDifferentNameVariables_ProducesSameVariables() {
            Expr firstBoolean = context.MkConst("boolVar1", context.BoolSort);
            Expr secondBoolean = context.MkConst("boolVar2", context.BoolSort);
            BoolExpr equalityExpression = context.MkEq(firstBoolean, secondBoolean);
            Quantifier forAllQuantifier = context.MkForall(new[] { firstBoolean, secondBoolean }, equalityExpression);
            Solver solver = context.MkSolver();
            solver.Assert(forAllQuantifier);
            Status status = solver.Check();
            Assert.AreEqual(Status.UNSATISFIABLE, status);
        }
    }
}