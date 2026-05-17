#nullable enable
using System;

namespace Datra.Editor.Schema
{
    /// <summary>
    /// Best-effort default-value factory used when a collection editor needs to add a new element.
    /// Replaces the <c>CreateDefaultValue</c> helpers duplicated in <c>ArrayFieldHandler</c> /
    /// <c>ListFieldHandler</c> across Unity and Web.
    /// </summary>
    public static class DefaultValueFactory
    {
        /// <summary>
        /// Returns a sensible default for <paramref name="type"/>:
        /// <list type="bullet">
        /// <item>empty string for <see cref="string"/>;</item>
        /// <item><c>default(T)</c> for value types (boxed);</item>
        /// <item>a new instance via parameterless ctor for classes that have one;</item>
        /// <item><c>null</c> otherwise.</item>
        /// </list>
        /// </summary>
        public static object? CreateDefault(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (type == typeof(string)) return string.Empty;
            if (type.IsValueType) return Activator.CreateInstance(type);
            return type.GetConstructor(Type.EmptyTypes) != null
                ? Activator.CreateInstance(type)
                : null;
        }
    }
}
