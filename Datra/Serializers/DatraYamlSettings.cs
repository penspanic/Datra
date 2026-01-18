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
        /// Creates a deserializer with default settings.
        /// </summary>
        public static IDeserializer CreateDeserializer()
        {
            return CreateDeserializer(null);
        }

        /// <summary>
        /// Creates a deserializer with optional polymorphic type support.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism. Null for no polymorphism.</param>
        public static IDeserializer CreateDeserializer(IEnumerable<Type>? polymorphicBaseTypes)
        {
            HashSet<Type>? typeSet = polymorphicBaseTypes != null
                ? new HashSet<Type>(polymorphicBaseTypes)
                : null;
            return CreateDeserializer(typeSet);
        }

        /// <summary>
        /// Creates a deserializer with polymorphic type support using a shared HashSet.
        /// The HashSet reference is preserved, allowing dynamic type registration.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Shared HashSet of base types. Null for no polymorphism.</param>
        public static IDeserializer CreateDeserializer(HashSet<Type>? polymorphicBaseTypes)
        {
            var dataRefConverter = new DataRefYamlConverter();
            var localeRefConverter = new LocaleRefYamlConverter();

            var builder = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .WithTypeConverter(dataRefConverter)
                .WithTypeConverter(localeRefConverter);

            // Add polymorphic support if types are specified
            if (polymorphicBaseTypes != null)
            {
                var typeResolver = new PortableTypeResolver();
                var polymorphicConverter = new PolymorphicYamlTypeConverter(typeResolver, polymorphicBaseTypes);
                builder.WithTypeConverter(polymorphicConverter);
            }

            return builder.Build();
        }

        /// <summary>
        /// Creates a serializer with default settings.
        /// </summary>
        public static ISerializer CreateSerializer()
        {
            return CreateSerializer(null);
        }

        /// <summary>
        /// Creates a serializer with optional polymorphic type support.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism. Null for no polymorphism.</param>
        public static ISerializer CreateSerializer(IEnumerable<Type>? polymorphicBaseTypes)
        {
            HashSet<Type>? typeSet = polymorphicBaseTypes != null
                ? new HashSet<Type>(polymorphicBaseTypes)
                : null;
            return CreateSerializer(typeSet);
        }

        /// <summary>
        /// Creates a serializer with polymorphic type support using a shared HashSet.
        /// The HashSet reference is preserved, allowing dynamic type registration.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Shared HashSet of base types. Null for no polymorphism.</param>
        public static ISerializer CreateSerializer(HashSet<Type>? polymorphicBaseTypes)
        {
            var dataRefConverter = new DataRefYamlConverter();
            var localeRefConverter = new LocaleRefYamlConverter();

            var builder = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeInspector(inner => new WritablePropertiesTypeInspector(inner))
                .WithTypeConverter(dataRefConverter)
                .WithTypeConverter(localeRefConverter)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull);

            // Add polymorphic support if types are specified
            if (polymorphicBaseTypes != null)
            {
                var typeResolver = new PortableTypeResolver();
                var polymorphicConverter = new PolymorphicYamlTypeConverter(typeResolver, polymorphicBaseTypes);
                builder.WithTypeConverter(polymorphicConverter);
            }

            return builder.Build();
        }
    }
}
