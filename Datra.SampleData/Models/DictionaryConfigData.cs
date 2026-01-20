using System.Collections.Generic;
using Datra.Attributes;

namespace Datra.SampleData.Models
{
    /// <summary>
    /// Test SingleData with Dictionary properties for YAML serialization testing.
    /// </summary>
    [SingleData("DictionaryConfig.yaml", Format = DataFormat.Yaml)]
    public partial class DictionaryConfigData
    {
        public string Name { get; set; } = string.Empty;
        public int TotalCount { get; set; }

        /// <summary>
        /// Simple string list
        /// </summary>
        public List<string> EntryPoints { get; set; } = new List<string>();

        /// <summary>
        /// Dictionary with string key and list of strings value
        /// </summary>
        public Dictionary<string, List<string>> StartPoints { get; set; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// Dictionary with string key and int value
        /// </summary>
        public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Dictionary with string key and nested object value
        /// </summary>
        public Dictionary<string, DictionaryEntryInfo> Entries { get; set; } = new Dictionary<string, DictionaryEntryInfo>();
    }

    /// <summary>
    /// Nested object for Dictionary value testing.
    /// </summary>
    public class DictionaryEntryInfo
    {
        public string EntryId { get; set; } = string.Empty;
        public string Category { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public int NodeCount { get; set; }
        public bool IsEntryPoint { get; set; }
        public List<LinkInfo> OutgoingLinks { get; set; } = new List<LinkInfo>();
        public List<LinkInfo> IncomingLinks { get; set; } = new List<LinkInfo>();
    }

    /// <summary>
    /// Link info for nested list testing.
    /// </summary>
    public class LinkInfo
    {
        public string SourceId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public LinkType LinkType { get; set; }
        public int? ChoiceIndex { get; set; }
    }

    public enum LinkType
    {
        Immediate,
        Scheduled
    }
}
