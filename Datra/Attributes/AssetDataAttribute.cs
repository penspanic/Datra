#nullable enable
using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Attribute for file-based asset data where each file represents a complete asset.
    /// Unlike TableData, the asset's identity comes from metadata (GUID), not from the object itself.
    ///
    /// Each asset file has a companion .datrameta file containing:
    /// - Stable GUID (survives file renames/moves)
    /// - Optional metadata (display name, tags, etc.)
    ///
    /// Note: We use .datrameta extension to avoid conflicts with Unity's .meta files.
    ///
    /// Example:
    /// [AssetData("graphs/", Pattern = "*.json")]
    /// public class Graph { ... }
    ///
    /// File structure:
    /// graphs/
    /// ├── story_01.json            (asset data)
    /// ├── story_01.json.datrameta  (metadata with GUID)
    /// └── intro.json
    /// └── intro.json.datrameta
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AssetDataAttribute : Attribute
    {
        /// <summary>
        /// Fixed extension for metadata files (uses .datrameta to avoid Unity .meta conflicts)
        /// </summary>
        public const string MetaExtension = ".datrameta";

        /// <summary>
        /// Folder path containing the asset files
        /// </summary>
        public string FolderPath { get; }

        /// <summary>
        /// File pattern for asset files (e.g., "*.json", "*.yaml")
        /// </summary>
        public string Pattern { get; set; } = "*.json";

        /// <summary>
        /// Data format for asset files (auto-detected by default)
        /// </summary>
        public DataFormat Format { get; set; } = DataFormat.Auto;

        /// <summary>
        /// Addressables label for Unity (optional)
        /// </summary>
        public string? Label { get; set; }

        public AssetDataAttribute(string folderPath)
        {
            FolderPath = folderPath;
        }
    }
}
