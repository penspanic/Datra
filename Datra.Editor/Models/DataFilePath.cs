#nullable enable
using System;
using System.IO;

namespace Datra.Editor.Interfaces
{
    /// <summary>
    /// Type-safe wrapper for file paths in the data system.
    /// Provides path normalization and validation.
    /// </summary>
    public readonly struct DataFilePath : IEquatable<DataFilePath>
    {
        private readonly string _value;

        /// <summary>
        /// Empty path value
        /// </summary>
        public static readonly DataFilePath Empty = new DataFilePath(string.Empty);

        public DataFilePath(string value)
        {
            _value = NormalizePath(value);
        }

        /// <summary>
        /// The normalized file path value
        /// </summary>
        public string Value => _value ?? string.Empty;

        /// <summary>
        /// Whether this path has a valid value
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(_value);

        /// <summary>
        /// Get just the file name portion
        /// </summary>
        public string FileName => Path.GetFileName(_value ?? string.Empty);

        /// <summary>
        /// Get the directory portion
        /// </summary>
        public string Directory => NormalizePath(Path.GetDirectoryName(_value ?? string.Empty) ?? string.Empty);

        /// <summary>
        /// Get the file extension
        /// </summary>
        public string Extension => Path.GetExtension(_value ?? string.Empty);

        /// <summary>
        /// Get the file name without extension
        /// </summary>
        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(_value ?? string.Empty);

        /// <summary>
        /// Combine this path with another path segment
        /// </summary>
        public DataFilePath Combine(string path)
        {
            if (string.IsNullOrEmpty(_value))
                return new DataFilePath(path);
            if (string.IsNullOrEmpty(path))
                return this;
            return new DataFilePath(_value + "/" + path);
        }

        /// <summary>
        /// Check if a path is null or empty
        /// </summary>
        public static bool IsNullOrEmpty(DataFilePath? path)
        {
            return !path.HasValue || !path.Value.IsValid;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Normalize to forward slashes for cross-platform consistency
            var normalized = path.Replace('\\', '/');

            // Remove trailing slash
            if (normalized.Length > 1 && normalized.EndsWith("/"))
                normalized = normalized.TrimEnd('/');

            return normalized;
        }

        public override string ToString() => _value ?? string.Empty;

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(_value ?? string.Empty);
        }

        public override bool Equals(object? obj)
        {
            return obj is DataFilePath other && Equals(other);
        }

        public bool Equals(DataFilePath other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(_value, other._value);
        }

        public static bool operator ==(DataFilePath left, DataFilePath right) => left.Equals(right);
        public static bool operator !=(DataFilePath left, DataFilePath right) => !left.Equals(right);

        public static implicit operator string(DataFilePath path) => path.Value;
        public static implicit operator DataFilePath(string path) => new DataFilePath(path);
    }
}
