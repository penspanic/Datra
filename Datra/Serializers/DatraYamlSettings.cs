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
            var builder = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .WithTypeConverter(new DataRefYamlConverter())
                .WithTypeConverter(new LocaleRefYamlConverter());

            // Register custom converters first (they take priority)
            if (customConverters != null)
            {
                foreach (var converter in customConverters)
                {
                    builder.WithTypeConverter(converter);
                }
            }

            // Always add polymorphic support (handles abstract types and interfaces)
            var typeSet = polymorphicBaseTypes != null ? new HashSet<Type>(polymorphicBaseTypes) : new HashSet<Type>();
            var typeResolver = new PortableTypeResolver();
            builder.WithTypeConverter(new PolymorphicYamlTypeConverter(typeResolver, typeSet));

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
            var builder = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeInspector(inner => new WritablePropertiesTypeInspector(inner))
                .WithTypeConverter(new DataRefYamlConverter())
                .WithTypeConverter(new LocaleRefYamlConverter())
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull);

            // Register custom converters first (they take priority)
            if (customConverters != null)
            {
                foreach (var converter in customConverters)
                {
                    builder.WithTypeConverter(converter);
                }
            }

            // Always add polymorphic support (handles abstract types and interfaces)
            var typeSet = polymorphicBaseTypes != null ? new HashSet<Type>(polymorphicBaseTypes) : new HashSet<Type>();
            var typeResolver = new PortableTypeResolver();
            builder.WithTypeConverter(new PolymorphicYamlTypeConverter(typeResolver, typeSet));

            return builder.Build();
        }
    }
}
