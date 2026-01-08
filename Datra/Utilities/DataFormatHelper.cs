#nullable enable
using System;
using System.IO;
using Datra.Attributes;

namespace Datra.Utilities
{
    /// <summary>
    /// Utility class for data format and file extension handling.
    /// Centralizes all format detection logic to avoid duplication.
    /// </summary>
    public static class DataFormatHelper
    {
        /// <summary>
        /// Detect DataFormat from file path extension.
        /// </summary>
        /// <param name="filePath">File path or extension (e.g., "data.json" or ".json")</param>
        /// <returns>Detected DataFormat</returns>
        /// <exception cref="NotSupportedException">If extension is not supported</exception>
        public static DataFormat DetectFormat(string filePath)
        {
            var extension = GetExtension(filePath);

            return extension switch
            {
                ".json" => DataFormat.Json,
                ".yaml" or ".yml" => DataFormat.Yaml,
                ".csv" => DataFormat.Csv,
                _ => throw new NotSupportedException($"File extension '{extension}' is not supported.")
            };
        }

        /// <summary>
        /// Try to detect DataFormat from file path extension.
        /// </summary>
        /// <param name="filePath">File path or extension</param>
        /// <param name="format">Detected format if successful</param>
        /// <returns>True if format was detected, false otherwise</returns>
        public static bool TryDetectFormat(string filePath, out DataFormat format)
        {
            var extension = GetExtension(filePath);

            switch (extension)
            {
                case ".json":
                    format = DataFormat.Json;
                    return true;
                case ".yaml":
                case ".yml":
                    format = DataFormat.Yaml;
                    return true;
                case ".csv":
                    format = DataFormat.Csv;
                    return true;
                default:
                    format = DataFormat.Auto;
                    return false;
            }
        }

        /// <summary>
        /// Extract file extension from a glob pattern (e.g., "*.yaml" -> ".yaml").
        /// </summary>
        /// <param name="pattern">Glob pattern like "*.json", "*.yaml", "*.csv"</param>
        /// <returns>Extension with dot prefix (e.g., ".yaml")</returns>
        public static string GetExtensionFromPattern(string? pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return ".json"; // default fallback

            var lastDot = pattern.LastIndexOf('.');
            if (lastDot >= 0)
            {
                return pattern.Substring(lastDot).ToLowerInvariant();
            }

            return ".json"; // default fallback
        }

        /// <summary>
        /// Check if extension is JSON format.
        /// </summary>
        public static bool IsJsonExtension(string? extension)
        {
            if (string.IsNullOrEmpty(extension))
                return false;
            return extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if extension is YAML format.
        /// </summary>
        public static bool IsYamlExtension(string? extension)
        {
            if (string.IsNullOrEmpty(extension))
                return false;
            return extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if extension is CSV format.
        /// </summary>
        public static bool IsCsvExtension(string? extension)
        {
            if (string.IsNullOrEmpty(extension))
                return false;
            return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if file path has JSON extension.
        /// </summary>
        public static bool IsJsonFile(string? filePath)
        {
            return IsJsonExtension(GetExtension(filePath));
        }

        /// <summary>
        /// Check if file path has YAML extension.
        /// </summary>
        public static bool IsYamlFile(string? filePath)
        {
            return IsYamlExtension(GetExtension(filePath));
        }

        /// <summary>
        /// Check if file path has CSV extension.
        /// </summary>
        public static bool IsCsvFile(string? filePath)
        {
            return IsCsvExtension(GetExtension(filePath));
        }

        /// <summary>
        /// Get normalized extension from file path (lowercase with dot prefix).
        /// </summary>
        private static string GetExtension(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return string.Empty;

            return Path.GetExtension(filePath).ToLowerInvariant();
        }
    }
}
