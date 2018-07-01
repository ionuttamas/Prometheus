# Prometheus

Licensed under **GPLv3**.

**Prometheus** is a framework for static code analysis to detect concurrency bugs that could occur in large scale systems written in C# code base.
**Prometheus** uses [Roslyn](https://github.com/dotnet/roslyn) compiler API for analyzing C# codebase and [Z3](https://github.com/Z3Prover/z3) prover for reachability satisfiability analysis.
**Prometheus** framework: 
1. An extensible framework allowing developers to write their own concurrency analyzers
2. Can be integrated in unit test suites for concurrency verification
3. Comes with a predefined set of concurrency analyzers such as:
 * Atomicity analyzer
 * Causality analyzer
 * Invariant analyzer
 
## Atomicity analyzer
Detects whether shared variables are atomically modified by concurrent execution of the system threads:

```csharp
public class Account
{
 public decimal Balance { get; set; }
}
[TestFixture]
public class TestRunner
{
 [ConcurrencyTest]
 public void Run()
 {
 var account = new Account();
 Expression<Func<Account, bool>> expression1 = x => x.Balance.IsModifiedAtomic();
 Expression<Func<Account, bool>> expression2 = x => x.IsModifiedAtomic("id");
 Assert.IsTrue(expression1);
 Assert.IsTrue(expression2);
 }
}
```

## Causality analyzer
Analyzes if some causality conditions are met given any thread configuration:

```csharp
public class Account
{
 public bool Initialized { get; set; }
 public decimal Balance { get; set; }
 private void Initialize() { ... }
 public bool Configure() { ... }
 public bool Transfer(int recipient, decimal amount) { ... }
}
public class TestRunner
{
 public void Run()
 {
 var account = new Account();
 Expression<Func<Account, bool>> expression1 = x => x.IsCalledOnce("Initialize");
 Expression<Func<Account, bool>> expression2 = x => x.Configure().IsCalledOnce();
 Expression<Func<Account, bool>> expression3 = x => x.Balance < 0 &&  x.Transfer(Args.Any<int>(), Args.Any<decimal>()).IsNotCalled();
 Expression<Func<Account, bool>> expression4 = x => x.Transfer(Args.Any<int>(), Args.Any<decimal>()).IsNotCalledBefore(x => x.Initialized);
}
```

## Invariant analyzer
Analyzes that the invariant of state of shared variable is satisfied:

```csharp
public class Account
{
 public bool Initialized { get; set; }
 public decimal Balance { get; set; }
 private CustomerType Type { get; set; }
 public decimal TransactionFee { get; set; }
}
public class TestRunner
{
 public void Run()
 {
 var account = new Account();
 Expression<Func<Account, bool>> expression1 = x => x.Type == CustomerType.VIP && x.TransactionFee > 20;
 Expression<Func<Account, bool>> expression2 = x => x.Balance > 0 && x.Initialized;
}
```
