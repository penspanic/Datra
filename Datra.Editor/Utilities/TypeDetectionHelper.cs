using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Datra.Attributes;
using Datra.DataTypes;

namespace Datra.Editor.Utilities
{
    /// <summary>
    /// 타입 감지 유틸리티
    /// Unity Editor와 Blazor WebEditor에서 공통으로 사용
    /// </summary>
    public static class TypeDetectionHelper
    {
        #region DataRef Detection

        /// <summary>
        /// DataRef 타입인지 확인 (StringDataRef, IntDataRef)
        /// </summary>
        public static bool IsDataRefType(Type type)
        {
            if (!type.IsGenericType)
                return false;

            var genericDef = type.GetGenericTypeDefinition();
            return genericDef == typeof(StringDataRef<>) ||
                   genericDef == typeof(IntDataRef<>);
        }

        /// <summary>
        /// DataRef 배열 타입인지 확인
        /// </summary>
        public static bool IsDataRefArrayType(Type type)
        {
            if (!type.IsArray)
                return false;

            return IsDataRefType(type.GetElementType());
        }

        #endregion

        #region LocaleRef Detection

        /// <summary>
        /// LocaleRef 타입인지 확인
        /// </summary>
        public static bool IsLocaleRefType(Type type)
        {
            return type == typeof(LocaleRef);
        }

        /// <summary>
        /// FixedLocale 속성이 있는 LocaleRef인지 확인
        /// </summary>
        public static bool IsFixedLocaleRef(Type type, MemberInfo member)
        {
            if (!IsLocaleRefType(type))
                return false;

            if (member == null)
                return false;

            return member.GetCustomAttribute<FixedLocaleAttribute>() != null;
        }

        #endregion

        #region Collection Detection

        /// <summary>
        /// List 타입인지 확인
        /// </summary>
        public static bool IsListType(Type type)
        {
            if (!type.IsGenericType)
                return false;

            return type.GetGenericTypeDefinition() == typeof(List<>);
        }

        /// <summary>
        /// Dictionary 타입인지 확인
        /// </summary>
        public static bool IsDictionaryType(Type type)
        {
            if (!type.IsGenericType)
                return false;

            return type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
        }

        /// <summary>
        /// 배열 타입인지 확인
        /// </summary>
        public static bool IsArrayType(Type type)
        {
            return type.IsArray;
        }

        /// <summary>
        /// 컬렉션 타입인지 확인 (List, Dictionary, Array)
        /// </summary>
        public static bool IsCollectionType(Type type)
        {
            return IsListType(type) || IsDictionaryType(type) || IsArrayType(type);
        }

        /// <summary>
        /// 컬렉션의 요소 타입 가져오기
        /// </summary>
        public static Type GetCollectionElementType(Type collectionType)
        {
            if (collectionType.IsArray)
                return collectionType.GetElementType();

            if (collectionType.IsGenericType)
            {
                var genericArgs = collectionType.GetGenericArguments();
                if (genericArgs.Length > 0)
                    return genericArgs[genericArgs.Length - 1]; // List<T> → T, Dictionary<K,V> → V
            }

            return typeof(object);
        }

        #endregion

        #region Nested Type Detection

        /// <summary>
        /// 중첩 타입인지 확인 (사용자 정의 struct/class)
        /// </summary>
        public static bool IsNestedType(Type type)
        {
            // 기본 타입 제외
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return false;

            // 배열, Enum 제외
            if (type.IsArray || type.IsEnum)
                return false;

            // System 타입 제외
            if (type.Namespace?.StartsWith("System") == true)
                return false;

            // Datra 특수 타입 제외
            if (IsDataRefType(type) || IsLocaleRefType(type))
                return false;

            // 컬렉션 제외
            if (IsCollectionType(type))
                return false;

            // struct 또는 class
            return type.IsValueType || type.IsClass;
        }

        /// <summary>
        /// 중첩 타입이 struct인지 확인
        /// </summary>
        public static bool IsNestedStruct(Type type)
        {
            return IsNestedType(type) && type.IsValueType;
        }

        #endregion

        #region Basic Type Detection

        /// <summary>
        /// 숫자 타입인지 확인
        /// </summary>
        public static bool IsNumericType(Type type)
        {
            return type == typeof(int) ||
                   type == typeof(long) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(decimal) ||
                   type == typeof(short) ||
                   type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(ushort) ||
                   type == typeof(uint) ||
                   type == typeof(ulong);
        }

        /// <summary>
        /// 정수 타입인지 확인
        /// </summary>
        public static bool IsIntegerType(Type type)
        {
            return type == typeof(int) ||
                   type == typeof(long) ||
                   type == typeof(short) ||
                   type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(ushort) ||
                   type == typeof(uint) ||
                   type == typeof(ulong);
        }

        /// <summary>
        /// 부동소수점 타입인지 확인
        /// </summary>
        public static bool IsFloatingPointType(Type type)
        {
            return type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(decimal);
        }

        #endregion

        #region Attribute Detection

        /// <summary>
        /// 특정 속성이 있는지 확인
        /// </summary>
        public static bool HasAttribute<TAttribute>(MemberInfo? member) where TAttribute : Attribute
        {
            return member?.GetCustomAttribute<TAttribute>() != null;
        }

        /// <summary>
        /// 특정 속성 가져오기
        /// </summary>
        public static TAttribute? GetAttribute<TAttribute>(MemberInfo? member) where TAttribute : Attribute
        {
            return member?.GetCustomAttribute<TAttribute>();
        }

        #endregion

        #region Member Utilities

        /// <summary>
        /// 멤버의 타입 가져오기
        /// </summary>
        public static Type? GetMemberType(MemberInfo? member)
        {
            return member switch
            {
                PropertyInfo prop => prop.PropertyType,
                FieldInfo field => field.FieldType,
                _ => null
            };
        }

        /// <summary>
        /// 멤버의 값 가져오기
        /// </summary>
        public static object? GetMemberValue(MemberInfo? member, object? target)
        {
            if (target == null)
                return null;

            return member switch
            {
                PropertyInfo prop => prop.GetValue(target),
                FieldInfo field => field.GetValue(target),
                _ => null
            };
        }

        /// <summary>
        /// 멤버의 값 설정하기
        /// </summary>
        public static void SetMemberValue(MemberInfo? member, object? target, object? value)
        {
            switch (member)
            {
                case PropertyInfo prop:
                    prop.SetValue(target, value);
                    break;
                case FieldInfo field:
                    field.SetValue(target, value);
                    break;
            }
        }

        /// <summary>
        /// 타입의 편집 가능한 멤버 목록 가져오기 (public properties and fields)
        /// </summary>
        public static IEnumerable<MemberInfo> GetEditableMembers(Type type)
        {
            // Public properties
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanRead && prop.CanWrite)
                    yield return prop;
            }

            // Public fields
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                yield return field;
            }
        }

        #endregion
    }
}
