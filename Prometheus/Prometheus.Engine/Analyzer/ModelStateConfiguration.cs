using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Prometheus.Engine.Analyzer
{
    public class ModelStateConfiguration
    {
        private readonly Dictionary<Type, List<MethodInfo>> stateChangeMethods;

        private ModelStateConfiguration()
        {
            stateChangeMethods = new Dictionary<Type, List<MethodInfo>>();
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

        //TODO: need to check the method as a whole not just its name
        public bool ChangesState(Type type, string methodName)
        {
            if (!stateChangeMethods.ContainsKey(type))
                return false;

            return stateChangeMethods[type].Any(x => x.Name == methodName);
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