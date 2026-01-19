using System;
using System.Collections.Generic;
using Datra.Attributes;
using Datra.Utilities;
using YamlDotNet.Serialization;

namespace Datra.Serializers
{
    /// <summary>
    /// Factory for creating appropriate serializers based on data format
    /// </summary>
    public class DataSerializerFactory
    {
        private readonly IDataSerializer _jsonSerializer;
        private readonly IDataSerializer _yamlSerializer;

        /// <summary>
        /// Creates a factory with default serializers (no polymorphic type support)
        /// </summary>
        public DataSerializerFactory()
        {
            _jsonSerializer = new JsonDataSerializer();
            _yamlSerializer = new YamlDataSerializer();
        }

        /// <summary>
        /// Creates a factory with polymorphic type support for YAML serialization
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism</param>
        public DataSerializerFactory(IEnumerable<Type> polymorphicBaseTypes)
            : this(polymorphicBaseTypes, customYamlConverters: null)
        {
        }

        /// <summary>
        /// Creates a factory with polymorphic type support and custom YAML type converters.
        /// Custom converters take priority over built-in polymorphic handling.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism. Can be null.</param>
        /// <param name="customYamlConverters">Custom YAML type converters for specialized serialization needs.</param>
        public DataSerializerFactory(
            IEnumerable<Type>? polymorphicBaseTypes,
            IEnumerable<IYamlTypeConverter>? customYamlConverters)
            : this(polymorphicBaseTypes, customYamlConverters, excludedTypes: null)
        {
        }

        /// <summary>
        /// Creates a factory with polymorphic type support, custom YAML type converters, and excluded types.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism. Can be null.</param>
        /// <param name="customYamlConverters">Custom YAML type converters for specialized serialization needs.</param>
        /// <param name="excludedTypes">Types to exclude from polymorphic handling (handled by custom converters).</param>
        public DataSerializerFactory(
            IEnumerable<Type>? polymorphicBaseTypes,
            IEnumerable<IYamlTypeConverter>? customYamlConverters,
            IEnumerable<Type>? excludedTypes)
        {
            _jsonSerializer = new JsonDataSerializer();
            _yamlSerializer = new YamlDataSerializer(polymorphicBaseTypes, customYamlConverters, excludedTypes);
        }

        /// <summary>
        /// Returns appropriate serializer based on file path and format
        /// </summary>
        public IDataSerializer GetSerializer(string filePath, DataFormat format = DataFormat.Auto)
        {
            if (format == DataFormat.Auto)
            {
                format = DataFormatHelper.DetectFormat(filePath);
            }

            return format switch
            {
                DataFormat.Json => _jsonSerializer,
                DataFormat.Yaml => _yamlSerializer,
                DataFormat.Csv => throw new NotSupportedException("CSV format should be handled by source-generated serializers, not by DataSerializer."),
                _ => throw new NotSupportedException($"Data format {format} is not supported.")
            };
        }
    }
}
