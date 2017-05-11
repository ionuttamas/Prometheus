﻿namespace Prometheus.Extensions
{
    /// <summary>
    /// Class that contains various marking extensions for formal model state verification.
    /// </summary>
    public static class ModelExtensions
    {
        /// <summary>
        /// Marks the value property or field as needed to be atomically modified.
        /// Primitive values (numeric, boolean, char) can be read without locks, but reference types cannot.
        /// E.g. for class "Account", marking "x => x.Balance.IsModifiedAtomic()" sets the "Balance" property as modifiable only within synchronized access.
        /// </summary>
        public static bool IsModifiedAtomic(this object value)
        {
            // Return value does not matter - is only used for property marking
            return false;
        }

        /// <summary>
        /// Marks the value property or field as needed to be atomically modified.
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
    }
}
