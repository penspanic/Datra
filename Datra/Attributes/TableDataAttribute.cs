using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Attribute to indicate Key-Value table data
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TableDataAttribute : Attribute
    {
        /// <summary>
        /// Data file path (for single-file mode) or folder path (for multi-file mode)
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Data format (auto-detected but can be specified)
        /// </summary>
        public DataFormat Format { get; set; } = DataFormat.Auto;

        /// <summary>
        /// Enable multi-file mode where each file in the folder/label contains a single data item.
        /// When true, loads all files matching the pattern/label and merges them into a single table.
        /// </summary>
        public bool MultiFile { get; set; } = false;

        /// <summary>
        /// Addressables label for multi-file loading in Unity.
        /// When specified, files are loaded by label instead of folder path.
        /// Only used when MultiFile = true.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// File pattern for multi-file mode (e.g., "*.json").
        /// Only used with FileSystemRawDataProvider when MultiFile = true.
        /// </summary>
        public string Pattern { get; set; } = "*.json";

        public TableDataAttribute(string filePath)
        {
            FilePath = filePath;
        }
    }

    public enum DataFormat
    {
        Auto,   // Auto-detect by extension
        Json,
        Yaml,
        Csv
    }
}
