using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Common
{
    public static class ReflectionExtensions
    {
        public static Type GetMemberType(this MemberInfo memberInfo)
        {
            if (memberInfo is PropertyInfo)
            {
                return memberInfo.As<PropertyInfo>().PropertyType;
            }

            if (memberInfo is FieldInfo)
            {
                return memberInfo.As<FieldInfo>().FieldType;
            }

            throw new InvalidCastException("Only FieldInfo and PropertyInfo are supported");
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

    public static class RoslynExtensions
    {
        public static string GetName(this FieldDeclarationSyntax fieldDeclaration)
        {
            var variable = fieldDeclaration.Declaration.Variables.First();
            return variable.Identifier.Text;
        }

        public static string GetName(this PropertyDeclarationSyntax propertyDeclaration)
        {
            return propertyDeclaration.Identifier.Text;
        }

        public static Compilation GetCompilation(this Project project)
        {
            return project.GetCompilationAsync().Result;
        }
    }
}