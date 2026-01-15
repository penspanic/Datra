using System;
using System.Collections.Generic;
using Datra.Attributes;
using Datra.Utilities;

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
        {
            _jsonSerializer = new JsonDataSerializer();
            _yamlSerializer = new YamlDataSerializer(polymorphicBaseTypes);
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
