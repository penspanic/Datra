using System;
using System.IO;
using Datra.Attributes;

namespace Datra.Loaders
{
    /// <summary>
    /// Factory for creating appropriate loaders based on data format
    /// </summary>
    public class DataLoaderFactory
    {
        private readonly IDataLoader _jsonLoader = new JsonDataLoader();
        private readonly IDataLoader _yamlLoader = new YamlDataLoader();
        
        /// <summary>
        /// Returns appropriate loader based on file path and format
        /// </summary>
        public IDataLoader GetLoader(string filePath, DataFormat format = DataFormat.Auto)
        {
            if (format == DataFormat.Auto)
            {
                format = DetectFormat(filePath);
            }
            
            return format switch
            {
                DataFormat.Json => _jsonLoader,
                DataFormat.Yaml => _yamlLoader,
                DataFormat.Csv => throw new NotSupportedException("CSV format should be handled by source-generated serializers, not by DataLoader."),
                _ => throw new NotSupportedException($"Data format {format} is not supported.")
            };
        }
        
        private DataFormat DetectFormat(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLower();
            
            return extension switch
            {
                ".json" => DataFormat.Json,
                ".yaml" or ".yml" => DataFormat.Yaml,
                ".csv" => DataFormat.Csv,
                _ => throw new NotSupportedException($"File extension {extension} is not supported.")
            };
        }
    }
}
