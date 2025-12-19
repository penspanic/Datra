#nullable enable
using System;
using System.Collections.Generic;

namespace Datra.DataTypes
{
    /// <summary>
    /// Metadata for an asset file, stored in companion .datrameta file.
    /// Provides stable identity and optional metadata that survives file renames.
    /// </summary>
    public class AssetMetadata
    {
        /// <summary>
        /// Stable unique identifier for this asset (GUID).
        /// Never changes even if the file is renamed or moved.
        /// </summary>
        public AssetId Guid { get; set; } = AssetId.Empty;

        /// <summary>
        /// Display name for the asset (optional).
        /// Used in editors and UIs instead of file name.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Tags for categorization and filtering (optional).
        /// </summary>
        public List<string>? Tags { get; set; }

        /// <summary>
        /// Category or type classification (optional).
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Description of the asset (optional).
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Version number for tracking changes (optional).
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Creation timestamp (auto-set when meta file is created).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last modification timestamp.
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// File content type / MIME type (optional).
        /// Used for server/editor scenarios.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// File size in bytes (optional).
        /// Used for server/editor scenarios.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// User who created this asset (optional).
        /// Used for server/editor scenarios.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// User who last modified this asset (optional).
        /// Used for server/editor scenarios.
        /// </summary>
        public string? ModifiedBy { get; set; }

        /// <summary>
        /// Additional custom properties (optional).
        /// </summary>
        public Dictionary<string, object>? CustomProperties { get; set; }

        /// <summary>
        /// Creates a new AssetMetadata with a fresh GUID.
        /// </summary>
        public static AssetMetadata CreateNew()
        {
            return new AssetMetadata
            {
                Guid = AssetId.NewId(),
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates a new AssetMetadata with the specified GUID.
        /// </summary>
        public static AssetMetadata Create(AssetId guid)
        {
            return new AssetMetadata
            {
                Guid = guid,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
        }
    }
}
