using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Prometheus.Common
{
    public static class ExpressionExtensions
    {
        public static Type GetParameterType(this Expression value) {
            dynamic expression = value;
            Type type = expression.Parameters[0].Type as Type;

            return type;
        }

        public static string GetParameterName(this Expression value) {
            dynamic expression = value;
            string name = expression.Parameters[0].Name;

            return name;
        }

        public static string GetPropertyName<TSource, TProperty>(this Expression<Func<TSource, TProperty>> propertyLambda) {
            string propertyName = propertyLambda.GetPropertyInfo().Name;

            return propertyName;
        }

        public static PropertyInfo GetPropertyInfo<TSource, TProperty>(this Expression<Func<TSource, TProperty>> propertyLambda) {
            Type type = typeof(TSource);

            MemberExpression member = (propertyLambda.Body.NodeType == ExpressionType.Convert ?
                ((UnaryExpression)propertyLambda.Body).Operand :
                propertyLambda.Body) as MemberExpression;

            if (member == null)
                throw new ArgumentException(string.Format(
                    "Expression '{0}' refers to a method, not a property.",
                    propertyLambda));

            PropertyInfo propertyInfo = member.Member as PropertyInfo;
            if (propertyInfo == null)
                throw new ArgumentException(string.Format(
                    "Expression '{0}' refers to a field, not a property.",
                    propertyLambda));

            if (type != propertyInfo.ReflectedType &&
                !type.IsSubclassOf(propertyInfo.ReflectedType))
                throw new ArgumentException(string.Format(
                    "Expresion '{0}' refers to a property that is not from type {1}.",
                    propertyLambda,
                    type));

            return propertyInfo;
        }

    }
}
