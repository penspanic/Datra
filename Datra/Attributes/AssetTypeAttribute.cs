using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Specifies the type of Unity asset for a string property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class AssetTypeAttribute : Attribute
    {
        /// <summary>
        /// The type of asset. Examples: "Unity.GameObject", "Unity.ScriptableObject", "Unity.Texture2D", "MyType.Foo"
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Optional: Filter by required components for GameObjects
        /// </summary>
        public string[] RequiredComponents { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Optional: File extension filter (e.g., "prefab", "asset")
        /// </summary>
        public string FileExtension { get; set; } = string.Empty;

        public AssetTypeAttribute(string type)
        {
            Type = type;
        }
    }
}