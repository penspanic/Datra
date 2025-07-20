using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Attribute to indicate single data object
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SingleDataAttribute : Attribute
    {
        /// <summary>
        /// Data file path
        /// </summary>
        public string FilePath { get; }
        
        /// <summary>
        /// Data format (auto-detected but can be specified)
        /// </summary>
        public DataFormat Format { get; set; } = DataFormat.Auto;
        
        public SingleDataAttribute(string filePath)
        {
            FilePath = filePath;
        }
    }
}
