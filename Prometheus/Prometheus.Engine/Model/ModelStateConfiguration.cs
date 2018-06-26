using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Prometheus.Common;
using Prometheus.Engine.Types;
using Prometheus.Engine.Types.Polymorphy;

namespace Prometheus.Engine.Model
{
    public class ModelStateConfiguration
    {
        private readonly Dictionary<Type, List<MethodInfo>> stateChangeMethods;
        private readonly Dictionary<MethodInfo, List<MethodInfo>> mutuallyExclusiveMethods;
        private IPolymorphicResolver polymorphicService;

        private ModelStateConfiguration()
        {
            stateChangeMethods = new Dictionary<Type, List<MethodInfo>>();
            mutuallyExclusiveMethods = new Dictionary<MethodInfo, List<MethodInfo>>();
        }

        public static ModelStateConfiguration Empty => new ModelStateConfiguration();

        #region Model configuration
        public ModelStateConfiguration ChangesState<T>(Expression<Action<T>> expression) {
            var type = typeof(T);

            if (!stateChangeMethods.ContainsKey(type)) {
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
        public ModelStateConfiguration MutuallyExclusive<T>(Expression<Func<T, bool>> first, Expression<Func<T, bool>> second) {
            MethodInfo firstMethod = GetMethodInfo(first);
            MethodInfo secondMethod = GetMethodInfo(second);

            if (!mutuallyExclusiveMethods.ContainsKey(firstMethod)) {
                mutuallyExclusiveMethods[firstMethod] = new List<MethodInfo>();
            }

            mutuallyExclusiveMethods[firstMethod].Add(secondMethod);

            return this;
        }

        //TODO: need to check the method as a whole not just its name
        public bool ChangesState(Type type, string methodName) {
            if (!stateChangeMethods.ContainsKey(type))
                return false;

            return stateChangeMethods[type].Any(x => x.Name == methodName);
        }
        #endregion

        #region Polymorphic utils
        public ModelStateConfiguration WithPolymorphicService(IPolymorphicResolver polymorphicService) {
            if (this.polymorphicService != null)
                throw new NotSupportedException("A polymorphic service can be specified only once");

            this.polymorphicService = polymorphicService;
            return this;
        }

        public ModelStateConfiguration WithImplementedType(MethodInfo method, string token, Type tokenType) {
            polymorphicService = polymorphicService ?? new PolymorphicResolver();
            polymorphicService.Register(method, token, tokenType);

            return this;
        }

        public ModelStateConfiguration WithImplementedType(Type classType, string method, string token, Type tokenType) {
            polymorphicService = polymorphicService ?? new PolymorphicResolver();
            polymorphicService.Register(classType, method, token, tokenType);

            return this;
        }

        public ModelStateConfiguration WithTypeResolver(Func<SyntaxToken, Type> typeResolver) {
            polymorphicService = polymorphicService ?? new PolymorphicResolver();
            polymorphicService.Configure(typeResolver);

            return this;
        }
        #endregion

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