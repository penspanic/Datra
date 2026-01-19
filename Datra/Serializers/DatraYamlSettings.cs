#nullable enable
using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Datra.Converters;

namespace Datra.Serializers
{
    /// <summary>
    /// Common YAML serializer settings for Datra.
    /// Provides consistent settings for type handling across all components.
    /// </summary>
    public static class DatraYamlSettings
    {
        /// <summary>
        /// Creates a deserializer with optional polymorphic type support and custom type converters.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism. Null for no polymorphism.</param>
        /// <param name="customConverters">Custom type converters to register. These take priority over built-in polymorphic handling.</param>
        public static IDeserializer CreateDeserializer(
            IEnumerable<Type>? polymorphicBaseTypes = null,
            IEnumerable<IYamlTypeConverter>? customConverters = null)
        {
            return CreateDeserializer(polymorphicBaseTypes, customConverters, excludedTypes: null);
        }

        /// <summary>
        /// Creates a deserializer with optional polymorphic type support and custom type converters.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism. Null for no polymorphism.</param>
        /// <param name="customConverters">Custom type converters to register. These take priority over built-in polymorphic handling.</param>
        /// <param name="excludedTypes">Types to exclude from polymorphic handling (handled by custom converters).</param>
        public static IDeserializer CreateDeserializer(
            IEnumerable<Type>? polymorphicBaseTypes,
            IEnumerable<IYamlTypeConverter>? customConverters,
            IEnumerable<Type>? excludedTypes)
        {
            var builder = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .WithTypeConverter(new DataRefYamlConverter())
                .WithTypeConverter(new LocaleRefYamlConverter());

            // Polymorphic support first (lower priority in YamlDotNet - later registered = higher priority)
            var typeSet = polymorphicBaseTypes != null ? new HashSet<Type>(polymorphicBaseTypes) : new HashSet<Type>();
            var excludedSet = excludedTypes != null ? new HashSet<Type>(excludedTypes) : new HashSet<Type>();
            var typeResolver = new PortableTypeResolver();
            builder.WithTypeConverter(new PolymorphicYamlTypeConverter(typeResolver, typeSet, excludedSet));

            // Register custom converters last (they take priority - later registered = higher priority in YamlDotNet)
            if (customConverters != null)
            {
                foreach (var converter in customConverters)
                {
                    builder.WithTypeConverter(converter);
                }
            }

            return builder.Build();
        }

        /// <summary>
        /// Creates a serializer with optional polymorphic type support and custom type converters.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism. Null for no polymorphism.</param>
        /// <param name="customConverters">Custom type converters to register. These take priority over built-in polymorphic handling.</param>
        public static ISerializer CreateSerializer(
            IEnumerable<Type>? polymorphicBaseTypes = null,
            IEnumerable<IYamlTypeConverter>? customConverters = null)
        {
            return CreateSerializer(polymorphicBaseTypes, customConverters, excludedTypes: null);
        }

        /// <summary>
        /// Creates a serializer with optional polymorphic type support and custom type converters.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism. Null for no polymorphism.</param>
        /// <param name="customConverters">Custom type converters to register. These take priority over built-in polymorphic handling.</param>
        /// <param name="excludedTypes">Types to exclude from polymorphic handling (handled by custom converters).</param>
        public static ISerializer CreateSerializer(
            IEnumerable<Type>? polymorphicBaseTypes,
            IEnumerable<IYamlTypeConverter>? customConverters,
            IEnumerable<Type>? excludedTypes)
        {
            var builder = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeInspector(inner => new WritablePropertiesTypeInspector(inner))
                .WithTypeConverter(new DataRefYamlConverter())
                .WithTypeConverter(new LocaleRefYamlConverter())
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull);

            // Polymorphic support first (lower priority in YamlDotNet - later registered = higher priority)
            var typeSet = polymorphicBaseTypes != null ? new HashSet<Type>(polymorphicBaseTypes) : new HashSet<Type>();
            var excludedSet = excludedTypes != null ? new HashSet<Type>(excludedTypes) : new HashSet<Type>();
            var typeResolver = new PortableTypeResolver();
            builder.WithTypeConverter(new PolymorphicYamlTypeConverter(typeResolver, typeSet, excludedSet));

            // Register custom converters last (they take priority - later registered = higher priority in YamlDotNet)
            if (customConverters != null)
            {
                foreach (var converter in customConverters)
                {
                    builder.WithTypeConverter(converter);
                }
            }

            return builder.Build();
        }
    }
}
