using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Common
{
    public static class CollectionExtensions
    {
        public static void Merge<TK, TV>(this Dictionary<TK, TV> table, Dictionary<TK, TV> newValues)
        {
            foreach (var entry in newValues)
            {
                table[entry.Key] = entry.Value;
            }
        }

        public static IEnumerable<T> DistinctBy<T>(this IEnumerable<T> collection, Func<T, object> selector) {
            IEnumerable<T> result = collection.Distinct(new FuncComparer<T>(selector));

            return result;
        }

        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] {Enumerable.Empty<T>()};

            return sequences.Aggregate(
                emptyProduct,
                (accumulator, sequence) =>
                    from accumulatorSeq in accumulator
                    from item in sequence
                    select accumulatorSeq.Concat(new[] {item}));
        }

        private class FuncComparer<T> : IEqualityComparer<T> {
            private readonly Func<T, object> comparer;

            public FuncComparer(Func<T, object> comparer) {
                this.comparer = comparer;
            }

            public bool Equals(T x, T y) {
                return comparer(x).Equals(comparer(y));
            }

            public int GetHashCode(T obj) {
                return comparer(obj).GetHashCode();
            }
        }
    }
}