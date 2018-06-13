using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Prometheus.Common
{
    public static class StringExtensions {
        private static readonly Regex DuplicateSpacesRegex = new Regex(@"[ ]{2,}", RegexOptions.None);

        public static bool IsNullOrEmpty(this string value) {
            bool isNullOrEmpty = string.IsNullOrEmpty(value);

            return isNullOrEmpty;
        }

        public static string JoinBy<T>(this IEnumerable<T> value, string separator) {
            return string.Join(separator, value);
        }

        public static string TrimStart(this string value, string trimString, StringComparison comparison) {
            string result = value;

            if (string.IsNullOrEmpty(trimString))
                return value;

            while (result.StartsWith(trimString, comparison)) {
                result = result.Substring(trimString.Length);
            }

            return result;
        }

        public static string Trim(this string value, string trimString, StringComparison comparison) {
            string result = TrimStart(value, trimString, comparison);
            result = TrimEnd(result, trimString, comparison);

            return result;
        }

        public static string TrimEnd(this string value, string trimString, StringComparison comparison) {
            string result = value;

            if (string.IsNullOrEmpty(trimString))
                return value;

            while (result.EndsWith(trimString, comparison)) {
                result = result.Substring(0, result.Length - trimString.Length);
            }

            return result;
        }

        public static string Capitalize(this string input) {
            if (input == null)
                return null;

            if (input.Length > 1)
                return char.ToUpper(input[0]) + input.Substring(1).ToLowerInvariant();

            return input.ToUpper();
        }

        public static bool EqualsInvariant(this string input, string value) {
            return input.Equals(value, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool ContainsInvariant(this string input, string value) {
            return input.IndexOf(value, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        public static bool ContainsString(this object instance, string value) {
            return instance.ToString().Contains(value);
        }

        public static bool ContainsStringInvariant(this object instance, string value) {
            return instance.ToString().ContainsInvariant(value);
        }

        public static int InvariantIndexOf(this string input, string value) {
            return input.IndexOf(value, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool StartsWithInvariant(this string input, string value) {
            return input.StartsWith(value, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool EndsWithInvariant(this string input, string value) {
            return input.EndsWith(value, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool StartsWithRegex(this string input, string regex, out string matchExpression) {
            Match match = new Regex(regex, RegexOptions.IgnoreCase).Match(input);

            if (match.Success && match.Index == 0) {
                matchExpression = match.Value;
                return true;
            }

            matchExpression = null;
            return false;
        }

        public static bool IsMatch(this string input, string regex, out string matchExpression) {
            Match match = new Regex(regex, RegexOptions.IgnoreCase).Match(input);
            matchExpression = match.Success ? match.Groups[1].Value : null;

            return match.Success;
        }

        public static string RemoveDuplicateSpaces(this string input) {
            return DuplicateSpacesRegex.Replace(input, " ").Trim();
        }
    }
}