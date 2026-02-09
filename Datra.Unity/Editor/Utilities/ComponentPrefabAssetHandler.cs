#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Generic asset handler for prefabs with specific component type
    /// </summary>
    /// <typeparam name="T">The component type to filter by</typeparam>
    public class ComponentPrefabAssetHandler<T> : IAssetTypeHandler where T : Component
    {
        private readonly string assetTypeString;
        private readonly string displayName;
        private readonly string[] additionalRequiredComponents;
        
        public string AssetTypeString => assetTypeString;
        
        /// <summary>
        /// Create a handler for prefabs with a specific component
        /// </summary>
        /// <param name="assetTypeString">The asset type string (e.g., "Unity.Component.PlayerController")</param>
        /// <param name="displayName">Display name for the asset type</param>
        /// <param name="additionalRequiredComponents">Optional additional component type names that must also be present</param>
        public ComponentPrefabAssetHandler(string assetTypeString, string displayName = null, params string[] additionalRequiredComponents)
        {
            this.assetTypeString = assetTypeString;
            this.displayName = displayName ?? $"{typeof(T).Name} Prefab";
            this.additionalRequiredComponents = additionalRequiredComponents;
        }
        
        public Type GetUnityType()
        {
            return typeof(GameObject);
        }
        
        public List<string> GetFilteredAssetPaths(string folderPath, string searchPattern)
        {
            var paths = new List<string>();
            var searchFolders = string.IsNullOrEmpty(folderPath) ? null : new[] { folderPath };
            var guids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null && ValidatePrefab(prefab))
                {
                    // Apply search pattern if provided
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
            var gameObject = asset as GameObject;
            return gameObject != null && ValidatePrefab(gameObject);
        }
        
        private bool ValidatePrefab(GameObject prefab)
        {
            // Check for main component type
            if (prefab.GetComponent<T>() == null)
                return false;
            
            // Check for additional required components
            if (additionalRequiredComponents != null)
            {
                foreach (var componentName in additionalRequiredComponents)
                {
                    if (prefab.GetComponent(componentName) == null)
                        return false;
                }
            }
            
            return true;
        }
        
        public string GetDisplayName()
        {
            return displayName;
        }
        
        public Texture2D GetIcon()
        {
            return EditorGUIUtility.FindTexture("Prefab Icon");
        }
    }
    
    /// <summary>
    /// Factory class for creating component-based prefab handlers
    /// </summary>
    public static class ComponentPrefabHandlerFactory
    {
        /// <summary>
        /// Create and register a handler for prefabs with a specific component
        /// </summary>
        public static void RegisterComponentHandler<T>(string assetTypeString, string displayName = null, params string[] additionalRequiredComponents) where T : Component
        {
            var handler = new ComponentPrefabAssetHandler<T>(assetTypeString, displayName, additionalRequiredComponents);
            AssetTypeHandlerRegistry.RegisterHandler(handler);
        }
        
        /// <summary>
        /// Create and register a handler by component type name (for when you don't have the type at compile time)
        /// </summary>
        public static void RegisterComponentHandler(string componentTypeName, string assetTypeString, string displayName = null, params string[] additionalRequiredComponents)
        {
            var handler = new RuntimeComponentPrefabAssetHandler(componentTypeName, assetTypeString, displayName, additionalRequiredComponents);
            AssetTypeHandlerRegistry.RegisterHandler(handler);
        }
    }
    
    /// <summary>
    /// Runtime version of component prefab handler that uses string-based component lookup
    /// </summary>
    public class RuntimeComponentPrefabAssetHandler : IAssetTypeHandler
    {
        private readonly string componentTypeName;
        private readonly string assetTypeString;
        private readonly string displayName;
        private readonly string[] additionalRequiredComponents;
        
        public string AssetTypeString => assetTypeString;
        
        public RuntimeComponentPrefabAssetHandler(string componentTypeName, string assetTypeString, string displayName = null, params string[] additionalRequiredComponents)
        {
            this.componentTypeName = componentTypeName;
            this.assetTypeString = assetTypeString;
            this.displayName = displayName ?? $"{componentTypeName} Prefab";
            this.additionalRequiredComponents = additionalRequiredComponents;
        }
        
        public Type GetUnityType()
        {
            return typeof(GameObject);
        }
        
        public List<string> GetFilteredAssetPaths(string folderPath, string searchPattern)
        {
            var paths = new List<string>();
            var searchFolders = string.IsNullOrEmpty(folderPath) ? null : new[] { folderPath };
            var guids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null && ValidatePrefab(prefab))
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
            var gameObject = asset as GameObject;
            return gameObject != null && ValidatePrefab(gameObject);
        }
        
        private bool ValidatePrefab(GameObject prefab)
        {
            // Check for main component type by name
            if (prefab.GetComponent(componentTypeName) == null)
                return false;
            
            // Check for additional required components
            if (additionalRequiredComponents != null)
            {
                foreach (var componentName in additionalRequiredComponents)
                {
                    if (prefab.GetComponent(componentName) == null)
                        return false;
                }
            }
            
            return true;
        }
        
        public string GetDisplayName()
        {
            return displayName;
        }
        
        public Texture2D GetIcon()
        {
            return EditorGUIUtility.FindTexture("Prefab Icon");
        }
    }
}