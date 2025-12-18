#nullable enable
using System;
using System.IO;

namespace Datra.Editor
{
    /// <summary>
    /// Type-safe wrapper for data file paths.
    /// Provides path normalization and comparison.
    /// </summary>
    public readonly struct DataFilePath : IEquatable<DataFilePath>
    {
        private readonly string _value;

        public DataFilePath(string value)
        {
            _value = NormalizePath(value ?? throw new ArgumentNullException(nameof(value)));
        }

        /// <summary>
        /// Whether this path is valid (non-empty)
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(_value);

        /// <summary>
        /// Gets the file name without directory
        /// </summary>
        public string FileName => Path.GetFileName(_value);

        /// <summary>
        /// Gets the file extension (including dot)
        /// </summary>
        public string Extension => Path.GetExtension(_value);

        /// <summary>
        /// Gets the directory path
        /// </summary>
        public string Directory => Path.GetDirectoryName(_value) ?? string.Empty;

        public override string ToString() => _value ?? string.Empty;

        public static explicit operator string(DataFilePath path) => path._value;
        public static implicit operator DataFilePath(string path) => new(path);

        public bool Equals(DataFilePath other) =>
            string.Equals(_value, other._value, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is DataFilePath other && Equals(other);

        public override int GetHashCode() =>
            _value?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0;

        public static bool operator ==(DataFilePath left, DataFilePath right) => left.Equals(right);
        public static bool operator !=(DataFilePath left, DataFilePath right) => !left.Equals(right);

        /// <summary>
        /// Empty/null path
        /// </summary>
        public static readonly DataFilePath Empty = new(string.Empty);

        /// <summary>
        /// Check if path is null or empty
        /// </summary>
        public static bool IsNullOrEmpty(DataFilePath? path)
        {
            if (path == null)
                return true;
            return !path.Value.IsValid;
        }

        /// <summary>
        /// Combine with another path segment
        /// </summary>
        public DataFilePath Combine(string segment) =>
            new(Path.Combine(_value, segment));

        /// <summary>
        /// Normalize path separators to forward slash
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Normalize to forward slashes for cross-platform consistency
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
