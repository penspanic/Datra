#nullable enable
using System;
using System.IO;

namespace Datra.DataTypes
{
    /// <summary>
    /// Wrapper for asset data that includes metadata.
    /// Combines the stable identity (from .datrameta file) with the actual data.
    /// </summary>
    /// <typeparam name="T">The asset data type</typeparam>
    public class Asset<T> where T : class
    {
        /// <summary>
        /// Stable unique identifier from .datrameta file
        /// </summary>
        public AssetId Id { get; }

        /// <summary>
        /// Full metadata from .datrameta file
        /// </summary>
        public AssetMetadata Metadata { get; }

        /// <summary>
        /// The actual asset data
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// Original file path (relative to asset folder)
        /// </summary>
        public string FilePath { get; }

        private string? _localeRootId;

        /// <summary>
        /// Root ID used for locale key generation.
        /// Default: filename from FilePath (without extension), fallback to Id.
        /// Can be set to override the default behavior.
        /// </summary>
        public string LocaleRootId
        {
            get => _localeRootId ?? GetDefaultLocaleRootId();
            set => _localeRootId = value;
        }

        private string GetDefaultLocaleRootId()
        {
            if (!string.IsNullOrEmpty(FilePath))
                return Path.GetFileNameWithoutExtension(FilePath);
            return Id.ToString();
        }

        public Asset(AssetId id, AssetMetadata metadata, T data, string filePath)
        {
            Id = id;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        /// <summary>
        /// Simple constructor with just ID and data.
        /// Creates minimal metadata for runtime use (no file association).
        /// </summary>
        public Asset(AssetId id, T data)
        {
            Id = id;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Metadata = new AssetMetadata { Guid = id };
            FilePath = string.Empty;
        }

        /// <summary>
        /// Creates an Asset with new metadata
        /// </summary>
        public static Asset<T> Create(T data, string filePath)
        {
            var metadata = AssetMetadata.CreateNew();
            return new Asset<T>(metadata.Guid, metadata, data, filePath);
        }

        /// <summary>
        /// Creates an Asset with existing metadata
        /// </summary>
        public static Asset<T> Create(T data, AssetMetadata metadata, string filePath)
        {
            return new Asset<T>(metadata.Guid, metadata, data, filePath);
        }
    }
}
