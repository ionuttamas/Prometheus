using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Prometheus.Engine.Verifier;

namespace Prometheus.Engine.Model
{
    public class NetStateBootstrapper
    {
        public ModelStateConfiguration Bootstrap()
        {
            var modelConfiguration = ModelStateConfiguration.Empty;

            BootstrapList(modelConfiguration);
            BootstrapHashSet(modelConfiguration);
            BootstrapDictionary(modelConfiguration);

            return modelConfiguration;
        }

        private void BootstrapList(ModelStateConfiguration modelConfiguration)
        {
            modelConfiguration
                .ChangesState<List<object>>(x => x.RemoveAt(Args.Any<int>()))
                .ChangesState<List<object>>(x => x.Remove(Args.Any<object>()))
                .ChangesState<List<object>>(x => x.RemoveAll(Args.Any<Predicate<object>>()))
                .ChangesState<List<object>>(x => x.RemoveRange(Args.Any<int>(), Args.Any<int>()))
                .ChangesState<List<object>>(x => x.Clear())
                .ChangesState<List<object>>(x => x.Insert(Args.Any<int>(), Args.Any<object>()))
                .ChangesState<List<object>>(x => x.Reverse())
                .ChangesState<List<object>>(x => x.InsertRange(Args.Any<int>(), Args.Any<IEnumerable<object>>()))
                .ChangesState<List<object>>(x => x.Sort())
                .ChangesState<List<object>>(x => x.AddRange(Args.Any<IEnumerable<object>>()));

            modelConfiguration
                .MutuallyExclusive<List<object>>(x => x.Contains(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<List<object>>(x => x.Contains(Args.Any<object>()), x => x.Any());
        }

        private void BootstrapHashSet(ModelStateConfiguration modelConfiguration)
        {
            modelConfiguration
                .ChangesState<HashSet<object>>(x => x.ExceptWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<HashSet<object>>(x => x.IntersectWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<HashSet<object>>(x => x.SymmetricExceptWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<HashSet<object>>(x => x.UnionWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<HashSet<object>>(x => x.Add(Args.Any<object>()))
                .ChangesState<HashSet<object>>(x => x.RemoveWhere(Args.Any<Predicate<object>>()))
                .ChangesState<HashSet<object>>(x => x.Remove(Args.Any<object>()))
                .ChangesState<HashSet<object>>(x => x.Clear());

            modelConfiguration
                .MutuallyExclusive<HashSet<object>>(x => x.Contains(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<HashSet<object>>(x => x.Contains(Args.Any<object>()), x => x.Any());
        }

        private void BootstrapDictionary(ModelStateConfiguration modelConfiguration) {
            modelConfiguration
                .ChangesState<Dictionary<object, object>>(x => x.Add(Args.Any<object>(), Args.Any<object>()))
                .ChangesState<Dictionary<object, object>>(x => x.Remove(Args.Any<object>()))
                .ChangesState<Dictionary<object, object>>(x => x.Clear());

            modelConfiguration
                .MutuallyExclusive<Dictionary<object, object>>(x => x.ContainsKey(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<Dictionary<object, object>>(x => x.ContainsValue(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<Dictionary<object, object>>(x => x.ContainsKey(Args.Any<object>()), x => x.Any())
                .MutuallyExclusive<Dictionary<object, object>>(x => x.ContainsValue(Args.Any<object>()), x => x.Any());
        }
    }

    public class ModelStateConfiguration
    {
        private readonly Dictionary<Type, List<MethodInfo>> stateChangeMethods;
        private readonly Dictionary<MethodInfo, List<MethodInfo>> mutuallyExclusiveMethods;

        private ModelStateConfiguration()
        {
            stateChangeMethods = new Dictionary<Type, List<MethodInfo>>();
            mutuallyExclusiveMethods = new Dictionary<MethodInfo, List<MethodInfo>>();
        }

        public static ModelStateConfiguration Empty => new ModelStateConfiguration();

        public ModelStateConfiguration ChangesState<T>(Expression<Action<T>> expression)
        {
            var type = typeof (T);

            if (!stateChangeMethods.ContainsKey(type))
            {
                stateChangeMethods[type] = new List<MethodInfo>();
            }

            stateChangeMethods[type].Add(GetMethodInfo(expression));
            return this;
        }

        /// <summary>
        /// Specifies that the first expression makes the condition expression invalid.
        /// E.g. if "collection.Add(Args.Any<T>)" is called, it makes the condition expression collection.IsEmpty() invalid (always returns false).
        /// </summary>
        public ModelStateConfiguration Invalidates<T>(Expression<Action<T>> expression, Expression<Func<T, bool>> condition) {
            return this;
        }

        /// <summary>
        /// Specifies that the first expression is mutually exclusive with the second expression.
        /// E.g. if "collection.IsEmpty()" is mutually exclusive with "collection.Contains(Args.Any<T>)".
        /// </summary>
        public ModelStateConfiguration MutuallyExclusive<T>(Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
        {
            MethodInfo firstMethod = GetMethodInfo(first);
            MethodInfo secondMethod = GetMethodInfo(second);

            if (!mutuallyExclusiveMethods.ContainsKey(firstMethod))
            {
                mutuallyExclusiveMethods[firstMethod] = new List<MethodInfo>();
            }

            mutuallyExclusiveMethods[firstMethod].Add(secondMethod);

            return this;
        }

        //TODO: need to check the method as a whole not just its name
        public bool ChangesState(Type type, string methodName)
        {
            if (!stateChangeMethods.ContainsKey(type))
                return false;

            return stateChangeMethods[type].Any(x => x.Name == methodName);
        }

        private MethodInfo GetMethodInfo<T>(Expression<Func<T, bool>> expression) {
            var member = expression.Body as MethodCallExpression;

            if (member != null)
                return member.Method;

            throw new ArgumentException("Expression is not a method call", nameof(expression));
        }

        private MethodInfo GetMethodInfo<T>(Expression<Action<T>> expression)
        {
            var member = expression.Body as MethodCallExpression;

            if (member != null)
                return member.Method;

            throw new ArgumentException("Expression is not a method call", nameof(expression));
        }
    }
}