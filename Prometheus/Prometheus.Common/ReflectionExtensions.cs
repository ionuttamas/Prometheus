using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Prometheus.Common
{
    public static class ReflectionExtensions
    {
        public static Type GetMemberType(this MemberInfo member)
        {
            switch (member.MemberType) {
                case MemberTypes.Event:
                    return ((EventInfo)member).EventHandlerType;
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                default:
                    throw new ArgumentException
                    (
                        "Argument must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"
                    );
            }
        }

        public static MethodInfo GetMethod(this Type type, Predicate<MethodInfo> filter)
        {
            var method = type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(x => filter(x));

            return method;
        }

        public static Expression GetExpression(this PropertyInfo propertyInfo, Type type)
        {
            ParameterExpression parameter = Expression.Parameter(type, "x");
            MemberExpression property = Expression.Property(parameter, propertyInfo);
            Type funcType = typeof (Func<,>).MakeGenericType(type, propertyInfo.PropertyType);
            LambdaExpression expression = Expression.Lambda(funcType, property, parameter);

            return expression;
        }

        public static bool HasAttribute(this Type value, Type attributeType)
        {
            return value.GetCustomAttributes(attributeType, true).Length > 0;
        }

        public static T GetCustomAttribute<T>(this PropertyInfo property) where T : class
        {
            Attribute attribute = property.GetCustomAttribute(typeof (T), true);

            return attribute as T;
        }

        public static object GetValue(this object value, string propertyName)
        {
            PropertyInfo property = value
                .GetType()
                .GetProperty(propertyName);

            object result = property?.GetValue(value);

            return result;
        }

        public static bool IsCollection(this PropertyInfo property)
        {
            if (property.PropertyType == typeof (string))
                return false;

            return property
                .PropertyType
                .GetInterface(typeof (IEnumerable<>).FullName) != null;
        }

        public static Type GetCollectionType(this Type value)
        {
            return value.GetGenericArguments().Single();
        }

        public static bool IsSimple(this Type type)
        {
            var simpleTypes = new[]
            {
                typeof (string)
            };

            return
                type.IsValueType ||
                type.IsPrimitive ||
                simpleTypes.Contains(type) ||
                Convert.GetTypeCode(type) != TypeCode.Object;
        }

        public static bool IsNumeric(this Type type)
        {
            if (type == typeof (byte?) ||
                type == typeof (int?) ||
                type == typeof (short?) ||
                type == typeof (long?) ||
                type == typeof (double?) ||
                type == typeof (float?) ||
                type == typeof (decimal?))
                return true;

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsTimeBased(this Type type)
        {
            if (type == typeof (DateTime?) ||
                type == typeof (DateTime))
                return true;

            return false;
        }

        public static bool IsString(this Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.String:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsBoolean(this Type type)
        {
            if (type == typeof (bool?))
                return true;

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return true;
                default:
                    return false;
            }
        }
    }
}