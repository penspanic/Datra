#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.Attributes;
using Datra.DataTypes;

namespace Datra.Editor.Schema
{
    /// <summary>
    /// Maps a CLR <see cref="Type"/> (plus the optional originating <see cref="MemberInfo"/>) to a
    /// <see cref="FieldKind"/>. The classifier mirrors — and replaces — the <c>CanHandle</c>
    /// predicates duplicated across <c>Datra.Unity</c> and <c>Datra.WebEditor</c>.
    /// </summary>
    /// <remarks>
    /// The priority order intentionally matches <c>BlazorFieldTypeRegistry.RegisterDefaultHandlers</c>:
    /// LocaleRef (with <see cref="FixedLocaleAttribute"/>) → DataRef → Array → List → Dictionary →
    /// Enum → primitives → Nested fallback → Unsupported. The list/array/dict checks come BEFORE the
    /// Nested fallback so <see cref="IList{T}"/>-shaped containers never get rendered via the nested
    /// property walker.
    /// </remarks>
    public static class TypeClassifier
    {
        public static FieldKind Classify(Type type, MemberInfo? member = null)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            // 1. LocaleRef — only when tagged with [FixedLocale] on the originating member.
            if (type == typeof(LocaleRef))
            {
                if (member is PropertyInfo prop && prop.GetCustomAttribute<FixedLocaleAttribute>() != null)
                    return FieldKind.LocaleRef;
                if (member is FieldInfo field && field.GetCustomAttribute<FixedLocaleAttribute>() != null)
                    return FieldKind.LocaleRef;
                // LocaleRef without [FixedLocale] falls through to Nested (matches Web behaviour
                // where NestedTypeFieldHandler picks it up as a struct).
                return FieldKind.Nested;
            }

            // 2. DataRef generics.
            if (IsDataRefType(type))
                return FieldKind.DataRef;

            // 3. Array (1-D only — matches both Unity & Web handlers).
            if (type.IsArray && type.GetArrayRank() == 1)
                return FieldKind.Array;

            // 4. Dictionary BEFORE List — a Dictionary<,> is also enumerable but not IList<T>,
            //    so this ordering is defensive rather than strictly necessary.
            if (TryGetDictionaryArgs(type, out _, out _))
                return FieldKind.Dictionary;

            // 5. Generic IList<T> (interfaces include List<T>, Collection<T>, etc.).
            if (GetElementType(type) is not null && !type.IsArray && IsGenericListLike(type))
                return FieldKind.List;

            // 6. Enum.
            if (type.IsEnum)
                return FieldKind.Enum;

            // 7. Primitives by identity.
            if (type == typeof(string)) return FieldKind.String;
            if (type == typeof(bool)) return FieldKind.Boolean;
            if (type == typeof(byte) || type == typeof(sbyte)
                || type == typeof(short) || type == typeof(ushort)
                || type == typeof(int) || type == typeof(uint)
                || type == typeof(long) || type == typeof(ulong))
                return FieldKind.Integer;
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return FieldKind.Floating;
            if (type == typeof(DateTime)) return FieldKind.DateTime;

            // 8. Nested fallback — class or non-primitive struct.
            //    NOTE: We exclude raw IEnumerable (non-IList) to avoid pretending strings of types
            //    like HashSet<T> are editable as nested objects. Web's NestedTypeFieldHandler also
            //    excludes IEnumerable.
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
                return FieldKind.Unsupported;

            if (type.IsClass || (type.IsValueType && !type.IsPrimitive))
                return FieldKind.Nested;

            return FieldKind.Unsupported;
        }

        /// <summary>Returns true for <c>StringDataRef&lt;T&gt;</c> and <c>IntDataRef&lt;T&gt;</c>.</summary>
        public static bool IsDataRefType(Type type)
        {
            if (type == null) return false;
            if (!type.IsGenericType) return false;
            var def = type.GetGenericTypeDefinition();
            return def == typeof(StringDataRef<>) || def == typeof(IntDataRef<>);
        }

        /// <summary>Unpacks <c>StringDataRef&lt;T&gt;</c>/<c>IntDataRef&lt;T&gt;</c> into (referenced, key) types.</summary>
        public static (Type Referenced, Type Key) GetDataRefArgs(Type dataRefType)
        {
            if (!IsDataRefType(dataRefType))
                throw new ArgumentException($"{dataRefType} is not a DataRef type.", nameof(dataRefType));

            var referenced = dataRefType.GetGenericArguments()[0];
            var key = dataRefType.GetGenericTypeDefinition() == typeof(StringDataRef<>)
                ? typeof(string)
                : typeof(int);
            return (referenced, key);
        }

        /// <summary>
        /// Returns the element type for an array or any generic <see cref="IList{T}"/>; null otherwise.
        /// </summary>
        public static Type? GetElementType(Type type)
        {
            if (type == null) return null;
            if (type.IsArray) return type.GetElementType();

            // Check the type itself if it's IList<T> directly.
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
                return type.GetGenericArguments()[0];

            // Or any implemented IList<T> on the concrete type (List<T>, Collection<T>, ...).
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IList<>))
                    return iface.GetGenericArguments()[0];
            }
            return null;
        }

        private static bool IsGenericListLike(Type type)
        {
            if (!type.IsGenericType && !type.GetInterfaces().Any(i => i.IsGenericType))
                return false;
            return GetElementType(type) != null;
        }

        private static bool TryGetDictionaryArgs(Type type, out Type? keyType, out Type? valueType)
        {
            keyType = null;
            valueType = null;
            if (type == null) return false;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                var args = type.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
                return true;
            }

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    var args = iface.GetGenericArguments();
                    keyType = args[0];
                    valueType = args[1];
                    return true;
                }
            }
            return false;
        }
    }
}
