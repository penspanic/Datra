#nullable enable
using System;
using System.Reflection;
using Datra.DataTypes;

namespace Datra.Editor.Schema
{
    /// <summary>
    /// Encapsulates everything an editor handler needs to read and build a
    /// <see cref="StringDataRef{T}"/> / <see cref="IntDataRef{T}"/> value without rolling its own
    /// reflection.
    /// </summary>
    /// <remarks>
    /// Both DataRef structs have a public <c>Value</c> property; this class caches the
    /// <see cref="PropertyInfo"/> once so per-keystroke get/set on the editor side is cheap.
    /// </remarks>
    public sealed class DataRefTypeInfo
    {
        private readonly PropertyInfo _valueProperty;

        public Type DataRefType { get; }
        public Type ReferencedType { get; }
        public Type KeyType { get; }
        public bool IsStringKey => KeyType == typeof(string);

        private DataRefTypeInfo(Type dataRefType, Type referencedType, Type keyType, PropertyInfo valueProperty)
        {
            DataRefType = dataRefType;
            ReferencedType = referencedType;
            KeyType = keyType;
            _valueProperty = valueProperty;
        }

        /// <summary>Returns null when <paramref name="dataRefType"/> is not a recognised DataRef generic.</summary>
        public static DataRefTypeInfo? TryCreate(Type dataRefType)
        {
            if (dataRefType == null) return null;
            if (!TypeClassifier.IsDataRefType(dataRefType)) return null;
            var (referenced, key) = TypeClassifier.GetDataRefArgs(dataRefType);
            var valueProp = dataRefType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (valueProp == null) return null;
            return new DataRefTypeInfo(dataRefType, referenced, key, valueProp);
        }

        /// <summary>An empty boxed DataRef (default-key — empty string or 0).</summary>
        public object CreateEmpty()
        {
            return Activator.CreateInstance(DataRefType)!;
        }

        /// <summary>Reads the boxed Value of <paramref name="dataRefInstance"/> (null when unset).</summary>
        public object? GetKey(object dataRefInstance)
        {
            if (dataRefInstance == null) throw new ArgumentNullException(nameof(dataRefInstance));
            return _valueProperty.GetValue(dataRefInstance);
        }

        /// <summary>Builds a fresh DataRef boxed instance with <paramref name="key"/> assigned.</summary>
        /// <exception cref="ArgumentException">When <paramref name="key"/>'s type doesn't match <see cref="KeyType"/>.</exception>
        public object Build(object? key)
        {
            var instance = Activator.CreateInstance(DataRefType)!;
            if (key == null) return instance; // leaves default (empty string / 0)
            if (!KeyType.IsInstanceOfType(key))
                throw new ArgumentException(
                    $"Key of type {key.GetType()} cannot populate {DataRefType} (expected {KeyType}).",
                    nameof(key));
            _valueProperty.SetValue(instance, key);
            return instance;
        }
    }
}
