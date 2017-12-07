using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Prometheus.Extensions
{
    /// <summary>
    /// Class that contains various marking extensions for formal model state verification.
    /// </summary>
    public static class ModelExtensions
    {
        /// <summary>
        /// Marks the value property or field as needed to be atomically modified; currently, it accepts only simple properties/fields, not nesting is currently allowed.
        /// Primitive values (numeric, boolean, char) can be read without locks, but reference types cannot.
        /// E.g. for class "Account", marking "x => x.Balance.IsModifiedAtomic()" sets the "Balance" property as modifiable only within synchronized access.
        /// </summary>
        public static bool IsModifiedAtomic(this object value)
        {
            // Return value does not matter - is only used for property marking
            return false;
        }

        /// <summary>
        /// Marks the value property or field as needed to be atomically modified; currently, it accepts only simple properties/fields, not nesting is currently allowed.
        /// This check is intended for private fields.
        /// Primitive values (numeric, boolean, char) can be read without locks, but reference types cannot.
        /// E.g. for class "Account", marking "x => x.IsModifiedAtomic("Address")" sets the "Address" property as modifiable only within synchronized access.
        /// </summary>
        public static bool IsModifiedAtomic(this object value, string name)
        {
            // Return value does not matter - is only used for property marking
            return false;
        }

        /// <summary>
        /// Marks a method or event a class not being called, in the presence of other conditions.
        /// This can work also for private method or events names.
        /// E.g. for class "Account", the expression "x => !x.IsActive && x.IsNotCalled("Transfer")"
        /// specifies that the "Transfer" method should not be called when the flag "IsActive" is false.
        /// </summary>
        public static bool IsNotCalled(this object value, string method)
        {
            // Return value does not matter - is only used for method marking
            return false;
        }

        /// <summary>
        /// Marks a function a class not being called, in the presence of other conditions.
        /// This works for public class method names.
        /// E.g. for class "Account", the expression "x => !x.IsActive && x.Transfer(Arg.Any<decimal>(), Arg.Any<decimal>()).IsNotCalled()"
        /// specifies that the "Transfer" method should not be called when the flag "IsActive" is false.
        /// </summary>
        public static bool IsNotCalled<T>(this T value) {
            // Return value does not matter - is only used for method marking
            return false;
        }

        /// <summary>
        /// Marks a method or event a class being called exactly once, in the presence of other conditions.
        /// This can work also for private method or events names.
        /// E.g. for class "Account", the expression "x => x.IsActive && x.IsCalledOnce("Initialized")"
        /// specifies that the "Initialized" method should be called exactly once when the flag "IsActive" is true.
        /// </summary>
        public static bool IsCalledOnce(this object value, string method)
        {
            // Return value does not matter - is only used for method marking
            return false;
        }

        /// <summary>
        /// Marks a function a class being called exactly once, in the presence of other conditions.
        /// This works for public class method names.
        /// E.g. for class "Account", the expression "x => x.IsActive && x.Initialized().IsCalledOnce()"
        /// specifies that the "Initialized" method is called only once when the flag "IsActive" is true.
        /// </summary>
        public static bool IsCalledOnce<T>(this T value)
        {
            // Return value does not matter - is only used for method marking
            return false;
        }

        /// <summary>
        /// Marks a function as modifies the state of an object.
        /// E.g. for class "List<T>", the expression "x => x.Remove(Args.Any()).ChangesState("Add")"
        /// specifies that the "Remove" method causes state modification to the List<T> instance.
        /// </summary>
        public static bool ChangesState<T>(this T value)
        {
            // Return value does not matter - is only used for method marking
            return false;
        }

        /// <summary>
        /// Marks a function as modifies the state of an object.
        /// E.g. for class "List<T>", the expression "x => x.ChangesState("Add")"
        /// specifies that the "Add" method causes state modification to the List<T> instance.
        /// </summary>
        public static bool ChangesState<T>(this T value, string name)
        {
            // Return value does not matter - is only used for method marking
            return false;
        }
    }

    public class Account
    {
        private List<Guid> transactionIds;

        public decimal Balance { get; set; }
        public bool IsApproved { get; set; }
        public bool Initialize() { return true; }
        public bool Transfer(int recipient, decimal amount) { return true; }
    }

    public class TestRunner
    {
        public void SetUp()
        {
            Expression<Func<List<object>, bool>> setup = x => x.Remove(Args.Any<object>()).ChangesState();
        }

        public void Run()
        {
            var account = new Account();
            Expression<Func<Account, bool>> expression1 = x => x.IsCalledOnce("Initialize");
            Expression<Func<Account, bool>> expression3 = x => x.Balance < 0 && x.Transfer(Args.Any<int>(), Args.Any<decimal>()).IsCalledOnce();
        }
    }

    public static class Args
    {
        public static T Any<T>()
        {
            return default(T);
        }
    }
}
