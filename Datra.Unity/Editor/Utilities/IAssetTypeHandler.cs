#nullable disable
using System.Collections.Generic;
using UnityEngine;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Interface for custom asset type handlers
    /// </summary>
    public interface IAssetTypeHandler
    {
        /// <summary>
        /// The asset type string this handler supports (e.g., "MyType.Foo")
        /// </summary>
        string AssetTypeString { get; }
        
        /// <summary>
        /// Get Unity object type for ObjectField
        /// </summary>
        System.Type GetUnityType();
        
        /// <summary>
        /// Get filtered asset paths based on custom logic
        /// </summary>
        List<string> GetFilteredAssetPaths(string folderPath, string searchPattern);
        
        /// <summary>
        /// Validate if an asset matches the custom type requirements
        /// </summary>
        bool ValidateAsset(Object asset);
        
        /// <summary>
        /// Get display name for the asset type
        /// </summary>
        string GetDisplayName();
        
        /// <summary>
        /// Optional: Get icon for the asset type
        /// </summary>
        Texture2D GetIcon();
    }
}