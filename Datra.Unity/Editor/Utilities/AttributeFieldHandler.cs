using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Datra.Attributes;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Handles reading and processing Datra attributes for field rendering
    /// </summary>
    public static class AttributeFieldHandler
    {
        /// <summary>
        /// Check if a property has asset-related attributes
        /// </summary>
        public static bool HasAssetAttributes(PropertyInfo property)
        {
            return property.GetCustomAttribute<AssetTypeAttribute>() != null ||
                   property.GetCustomAttribute<FolderPathAttribute>() != null;
        }

        /// <summary>
        /// Check if a field has asset-related attributes
        /// </summary>
        public static bool HasAssetAttributes(FieldInfo field)
        {
            return field.GetCustomAttribute<AssetTypeAttribute>() != null ||
                   field.GetCustomAttribute<FolderPathAttribute>() != null;
        }

        /// <summary>
        /// Get AssetTypeAttribute from property
        /// </summary>
        public static AssetTypeAttribute GetAssetTypeAttribute(PropertyInfo property)
        {
            return property.GetCustomAttribute<AssetTypeAttribute>();
        }

        /// <summary>
        /// Get AssetTypeAttribute from field
        /// </summary>
        public static AssetTypeAttribute GetAssetTypeAttribute(FieldInfo field)
        {
            return field.GetCustomAttribute<AssetTypeAttribute>();
        }

        /// <summary>
        /// Get FolderPathAttribute from property
        /// </summary>
        public static FolderPathAttribute GetFolderPathAttribute(PropertyInfo property)
        {
            return property.GetCustomAttribute<FolderPathAttribute>();
        }

        /// <summary>
        /// Get FolderPathAttribute from field
        /// </summary>
        public static FolderPathAttribute GetFolderPathAttribute(FieldInfo field)
        {
            return field.GetCustomAttribute<FolderPathAttribute>();
        }
        
        /// <summary>
        /// Convert attribute asset type string to Unity Type
        /// </summary>
        public static Type GetUnityAssetType(string assetTypeString)
        {
            if (string.IsNullOrEmpty(assetTypeString))
                return typeof(UnityEngine.Object);
            
            // Check for custom handler first
            var customHandler = AssetTypeHandlerRegistry.GetHandler(assetTypeString);
            if (customHandler != null)
            {
                return customHandler.GetUnityType();
            }
            
            // Handle Unity built-in types
            switch (assetTypeString)
            {
                case UnityAssetTypes.GameObject:
                case UnityAssetTypes.Prefab:
                    return typeof(GameObject);
                    
                case UnityAssetTypes.ScriptableObject:
                    return typeof(ScriptableObject);
                    
                case UnityAssetTypes.Texture2D:
                    return typeof(Texture2D);
                    
                case UnityAssetTypes.Texture3D:
                    return typeof(Texture3D);
                    
                case UnityAssetTypes.Sprite:
                    return typeof(Sprite);
                    
                case UnityAssetTypes.RenderTexture:
                    return typeof(RenderTexture);
                    
                case UnityAssetTypes.AudioClip:
                    return typeof(AudioClip);
                    
                case UnityAssetTypes.AudioMixer:
                    return typeof(UnityEngine.Audio.AudioMixer);
                    
                case UnityAssetTypes.AnimationClip:
                    return typeof(AnimationClip);
                    
                case UnityAssetTypes.AnimatorController:
                    return typeof(UnityEditor.Animations.AnimatorController);
                    
                case UnityAssetTypes.Avatar:
                    return typeof(Avatar);
                    
                case UnityAssetTypes.Material:
                    return typeof(Material);
                    
                case UnityAssetTypes.Shader:
                    return typeof(Shader);
                    
                case UnityAssetTypes.Font:
                    return typeof(Font);
                    
                case UnityAssetTypes.Mesh:
                    return typeof(Mesh);
                    
                case UnityAssetTypes.PhysicMaterial:
                    return typeof(PhysicsMaterial);
                    
                case UnityAssetTypes.PhysicsMaterial2D:
                    return typeof(PhysicsMaterial2D);
                    
                case UnityAssetTypes.ComputeShader:
                    return typeof(ComputeShader);
                    
                case UnityAssetTypes.VideoClip:
                    return typeof(UnityEngine.Video.VideoClip);
                    
                case UnityAssetTypes.Scene:
                    return typeof(SceneAsset);
                    
                default:
                    // For custom types, return generic Object
                    return typeof(UnityEngine.Object);
            }
        }
        
        /// <summary>
        /// Get all assets matching the attribute constraints
        /// </summary>
        public static List<string> GetFilteredAssetPaths(AssetTypeAttribute assetType, FolderPathAttribute folderPath)
        {
            // Check for custom handler
            if (assetType != null)
            {
                var customHandler = AssetTypeHandlerRegistry.GetHandler(assetType.Type);
                if (customHandler != null)
                {
                    return customHandler.GetFilteredAssetPaths(folderPath?.Path, folderPath?.SearchPattern);
                }
            }
            
            var paths = new List<string>();
            var searchFilter = BuildSearchFilter(assetType, folderPath);
            var searchInFolders = folderPath != null ? new[] { folderPath.Path } : null;
            
            var guids = AssetDatabase.FindAssets(searchFilter, searchInFolders);
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Apply additional filters
                if (IsAssetMatchingConstraints(path, assetType, folderPath))
                {
                    paths.Add(path);
                }
            }
            
            return paths;
        }
        
        /// <summary>
        /// Build search filter string for AssetDatabase
        /// </summary>
        private static string BuildSearchFilter(AssetTypeAttribute assetType, FolderPathAttribute folderPath)
        {
            var filters = new List<string>();
            
            if (assetType != null)
            {
                var typeFilter = GetTypeFilter(assetType.Type);
                if (!string.IsNullOrEmpty(typeFilter))
                {
                    filters.Add($"t:{typeFilter}");
                }
            }
            
            if (folderPath != null && !string.IsNullOrEmpty(folderPath.SearchPattern))
            {
                // Remove file extension from pattern for AssetDatabase search
                var pattern = folderPath.SearchPattern.Replace("*", "").Replace(".prefab", "").Replace(".asset", "").Replace(".png", "").Replace(".jpg", "");
                if (!string.IsNullOrEmpty(pattern))
                {
                    filters.Add(pattern);
                }
            }
            
            return string.Join(" ", filters);
        }
        
        /// <summary>
        /// Get type filter string for AssetDatabase search
        /// </summary>
        private static string GetTypeFilter(string assetTypeString)
        {
            switch (assetTypeString)
            {
                case UnityAssetTypes.GameObject:
                case UnityAssetTypes.Prefab:
                    return "Prefab";
                case UnityAssetTypes.ScriptableObject:
                    return "ScriptableObject";
                case UnityAssetTypes.Texture2D:
                case UnityAssetTypes.Sprite:
                    return "Texture";
                case UnityAssetTypes.AudioClip:
                    return "AudioClip";
                case UnityAssetTypes.AnimationClip:
                    return "AnimationClip";
                case UnityAssetTypes.Material:
                    return "Material";
                case UnityAssetTypes.Shader:
                case UnityAssetTypes.ShaderGraph:
                    return "Shader";
                case UnityAssetTypes.Scene:
                    return "Scene";
                default:
                    return "";
            }
        }
        
        /// <summary>
        /// Check if asset matches all constraints
        /// </summary>
        private static bool IsAssetMatchingConstraints(string assetPath, AssetTypeAttribute assetType, FolderPathAttribute folderPath)
        {
            string errorMessage;
            return IsAssetMatchingConstraints(assetPath, assetType, folderPath, out errorMessage);
        }
        
        /// <summary>
        /// Check if asset matches all constraints with detailed error message
        /// </summary>
        private static bool IsAssetMatchingConstraints(string assetPath, AssetTypeAttribute assetType, FolderPathAttribute folderPath, out string errorMessage)
        {
            errorMessage = null;
            
            // Check folder constraints
            if (folderPath != null)
            {
                var directory = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
                
                if (!folderPath.IncludeSubfolders)
                {
                    if (directory != folderPath.Path.TrimEnd('/'))
                    {
                        errorMessage = $"Asset must be in folder '{folderPath.Path}' (subfolders not allowed)";
                        return false;
                    }
                }
                else
                {
                    // Check if asset is in the specified folder or its subfolders
                    if (!directory.StartsWith(folderPath.Path.TrimEnd('/')))
                    {
                        errorMessage = $"Asset must be in folder '{folderPath.Path}' or its subfolders";
                        return false;
                    }
                }
                
                // Check search pattern
                if (!string.IsNullOrEmpty(folderPath.SearchPattern))
                {
                    var fileName = System.IO.Path.GetFileName(assetPath);
                    if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, 
                        folderPath.SearchPattern.Replace("*", ".*").Replace("?", ".")))
                    {
                        errorMessage = $"File name does not match pattern '{folderPath.SearchPattern}'";
                        return false;
                    }
                }
            }
            
            // Check component requirements for GameObjects
            if (assetType != null && assetType.RequiredComponents != null && assetType.RequiredComponents.Length > 0)
            {
                if (assetType.Type == UnityAssetTypes.GameObject || assetType.Type == UnityAssetTypes.Prefab)
                {
                    var obj = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (obj != null)
                    {
                        var missingComponents = new List<string>();
                        foreach (var componentName in assetType.RequiredComponents)
                        {
                            if (obj.GetComponent(componentName) == null)
                            {
                                missingComponents.Add(componentName);
                            }
                        }
                        
                        if (missingComponents.Count > 0)
                        {
                            errorMessage = $"Missing required components: {string.Join(", ", missingComponents)}";
                            return false;
                        }
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Validate if a path matches the attribute constraints
        /// </summary>
        public static bool ValidateAssetPath(string path, AssetTypeAttribute assetType, FolderPathAttribute folderPath)
        {
            string errorMessage;
            return ValidateAssetPath(path, assetType, folderPath, out errorMessage);
        }
        
        /// <summary>
        /// Validate if a path matches the attribute constraints with detailed error message
        /// </summary>
        public static bool ValidateAssetPath(string path, AssetTypeAttribute assetType, FolderPathAttribute folderPath, out string errorMessage)
        {
            errorMessage = null;
            
            if (string.IsNullOrEmpty(path))
                return true; // Empty path is valid
            
            // Check if asset exists
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null)
            {
                errorMessage = $"Asset not found at path: {path}";
                return false;
            }
            
            // Check for custom handler
            if (assetType != null)
            {
                var customHandler = AssetTypeHandlerRegistry.GetHandler(assetType.Type);
                if (customHandler != null)
                {
                    if (!customHandler.ValidateAsset(asset))
                    {
                        errorMessage = $"Asset does not match custom type '{customHandler.GetDisplayName()}'";
                        return false;
                    }
                    return true;
                }
            }
            
            // Check type
            if (assetType != null)
            {
                // Special handling for Texture2D/Sprite
                if (assetType.Type == UnityAssetTypes.Sprite)
                {
                    // Check if it's a texture that can be used as sprite
                    if (asset is Texture2D texture)
                    {
                        // Try to load as sprite
                        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                        if (sprites.Length == 0)
                        {
                            errorMessage = $"Texture is not imported as Sprite. Change Texture Type to 'Sprite (2D and UI)' in import settings";
                            return false;
                        }
                        // If it has sprites, it's valid
                        return IsAssetMatchingConstraints(path, assetType, folderPath, out errorMessage);
                    }
                    else if (!(asset is Sprite))
                    {
                        errorMessage = $"Expected Sprite but found {asset.GetType().Name}";
                        return false;
                    }
                }
                else
                {
                    var expectedType = GetUnityAssetType(assetType.Type);
                    if (!expectedType.IsAssignableFrom(asset.GetType()))
                    {
                        errorMessage = $"Expected {GetDisplayTypeName(assetType.Type)} but found {asset.GetType().Name}";
                        return false;
                    }
                }
            }
            
            // Check constraints
            return IsAssetMatchingConstraints(path, assetType, folderPath, out errorMessage);
        }
        
        /// <summary>
        /// Get display name for asset type
        /// </summary>
        private static string GetDisplayTypeName(string assetTypeString)
        {
            if (assetTypeString.StartsWith("Unity."))
                return assetTypeString.Substring(6);
            return assetTypeString;
        }
    }
}