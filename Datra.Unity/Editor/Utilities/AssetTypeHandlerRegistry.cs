using System;
using System.Collections.Generic;
using UnityEngine;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Registry for custom asset type handlers
    /// </summary>
    public static class AssetTypeHandlerRegistry
    {
        private static Dictionary<string, IAssetTypeHandler> handlers = new Dictionary<string, IAssetTypeHandler>();
        
        /// <summary>
        /// Register a custom asset type handler
        /// </summary>
        public static void RegisterHandler(IAssetTypeHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            
            if (string.IsNullOrEmpty(handler.AssetTypeString))
                throw new ArgumentException("Asset type string cannot be null or empty", nameof(handler));
            
            handlers[handler.AssetTypeString] = handler;
            Debug.Log($"[AssetTypeHandlerRegistry] Registered handler for type: {handler.AssetTypeString}");
        }
        
        /// <summary>
        /// Unregister a custom asset type handler
        /// </summary>
        public static void UnregisterHandler(string assetTypeString)
        {
            if (handlers.Remove(assetTypeString))
            {
                Debug.Log($"[AssetTypeHandlerRegistry] Unregistered handler for type: {assetTypeString}");
            }
        }
        
        /// <summary>
        /// Get handler for a specific asset type
        /// </summary>
        public static IAssetTypeHandler GetHandler(string assetTypeString)
        {
            if (string.IsNullOrEmpty(assetTypeString))
                return null;
            
            handlers.TryGetValue(assetTypeString, out var handler);
            return handler;
        }
        
        /// <summary>
        /// Check if a handler is registered for a type
        /// </summary>
        public static bool HasHandler(string assetTypeString)
        {
            return !string.IsNullOrEmpty(assetTypeString) && handlers.ContainsKey(assetTypeString);
        }
        
        /// <summary>
        /// Get all registered asset type strings
        /// </summary>
        public static IEnumerable<string> GetRegisteredTypes()
        {
            return handlers.Keys;
        }
        
        /// <summary>
        /// Clear all registered handlers
        /// </summary>
        public static void Clear()
        {
            handlers.Clear();
            Debug.Log("[AssetTypeHandlerRegistry] Cleared all handlers");
        }
    }
    
    /// <summary>
    /// Example implementation of a custom asset type handler
    /// </summary>
    public class ExampleCustomAssetHandler : IAssetTypeHandler
    {
        public string AssetTypeString => "MyType.WeaponConfig";
        
        public Type GetUnityType()
        {
            // Return ScriptableObject as base type
            return typeof(ScriptableObject);
        }
        
        public List<string> GetFilteredAssetPaths(string folderPath, string searchPattern)
        {
            var paths = new List<string>();
            
            // Custom logic to find weapon config assets
            var guids = UnityEditor.AssetDatabase.FindAssets("t:ScriptableObject", 
                string.IsNullOrEmpty(folderPath) ? null : new[] { folderPath });
            
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                
                // Check if it's actually a weapon config (example: by type name)
                if (asset != null && asset.GetType().Name.Contains("WeaponConfig"))
                {
                    if (string.IsNullOrEmpty(searchPattern) || 
                        System.IO.Path.GetFileName(path).Contains(searchPattern.Replace("*", "")))
                    {
                        paths.Add(path);
                    }
                }
            }
            
            return paths;
        }
        
        public bool ValidateAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return false;
            
            // Validate it's a weapon config type
            return asset is ScriptableObject && asset.GetType().Name.Contains("WeaponConfig");
        }
        
        public string GetDisplayName()
        {
            return "Weapon Config";
        }
        
        public Texture2D GetIcon()
        {
            // Could return a custom icon
            return null;
        }
    }
}