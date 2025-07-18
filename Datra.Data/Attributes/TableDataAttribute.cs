using System;

namespace Datra.Data.Attributes
{
    /// <summary>
    /// Attribute to indicate Key-Value table data
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TableDataAttribute : Attribute
    {
        /// <summary>
        /// Data file path
        /// </summary>
        public string FilePath { get; }
        
        /// <summary>
        /// Data format (auto-detected but can be specified)
        /// </summary>
        public DataFormat Format { get; set; } = DataFormat.Auto;
        
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
