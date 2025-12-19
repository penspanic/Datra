using Datra.Attributes;

namespace Datra.SampleData.Models
{
    /// <summary>
    /// Sample asset data model for testing AssetData feature.
    /// Each file in the folder represents a single script asset with its own .meta file.
    /// ID comes from the .meta file's GUID, not from the object itself.
    /// </summary>
    [AssetData("Scripts", Pattern = "*.json")]
    public partial class ScriptAssetData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Content { get; set; }
        public int Version { get; set; }
        public string[] Tags { get; set; }
    }
}
