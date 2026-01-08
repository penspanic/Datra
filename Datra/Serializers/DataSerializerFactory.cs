using System;
using Datra.Attributes;
using Datra.Utilities;

namespace Datra.Serializers
{
    /// <summary>
    /// Factory for creating appropriate serializers based on data format
    /// </summary>
    public class DataSerializerFactory
    {
        private readonly IDataSerializer _jsonSerializer = new JsonDataSerializer();
        private readonly IDataSerializer _yamlSerializer = new YamlDataSerializer();

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
