using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Manages user preferences for the Datra editor
    /// </summary>
    public static class DatraUserPreferences
    {
        private const string PrefsKeyPrefix = "Datra_";
        
        // View preferences
        private const string ViewModePrefix = "ViewMode_";
        
        /// <summary>
        /// Gets the preferred view mode for a specific data type
        /// </summary>
        public static string GetViewMode(Type dataType, string defaultMode = null)
        {
            var key = $"{PrefsKeyPrefix}{ViewModePrefix}{dataType.FullName}";
            return EditorPrefs.GetString(key, defaultMode);
        }
        
        /// <summary>
        /// Sets the preferred view mode for a specific data type
        /// </summary>
        public static void SetViewMode(Type dataType, string viewMode)
        {
            var key = $"{PrefsKeyPrefix}{ViewModePrefix}{dataType.FullName}";
            EditorPrefs.SetString(key, viewMode);
        }
        
        /// <summary>
        /// Gets a string preference
        /// </summary>
        public static string GetString(string key, string defaultValue = "")
        {
            return EditorPrefs.GetString($"{PrefsKeyPrefix}{key}", defaultValue);
        }
        
        /// <summary>
        /// Sets a string preference
        /// </summary>
        public static void SetString(string key, string value)
        {
            EditorPrefs.SetString($"{PrefsKeyPrefix}{key}", value);
        }
        
        /// <summary>
        /// Gets an integer preference
        /// </summary>
        public static int GetInt(string key, int defaultValue = 0)
        {
            return EditorPrefs.GetInt($"{PrefsKeyPrefix}{key}", defaultValue);
        }
        
        /// <summary>
        /// Sets an integer preference
        /// </summary>
        public static void SetInt(string key, int value)
        {
            EditorPrefs.SetInt($"{PrefsKeyPrefix}{key}", value);
        }
        
        /// <summary>
        /// Gets a boolean preference
        /// </summary>
        public static bool GetBool(string key, bool defaultValue = false)
        {
            return EditorPrefs.GetBool($"{PrefsKeyPrefix}{key}", defaultValue);
        }
        
        /// <summary>
        /// Sets a boolean preference
        /// </summary>
        public static void SetBool(string key, bool value)
        {
            EditorPrefs.SetBool($"{PrefsKeyPrefix}{key}", value);
        }
        
        /// <summary>
        /// Gets a float preference
        /// </summary>
        public static float GetFloat(string key, float defaultValue = 0f)
        {
            return EditorPrefs.GetFloat($"{PrefsKeyPrefix}{key}", defaultValue);
        }
        
        /// <summary>
        /// Sets a float preference
        /// </summary>
        public static void SetFloat(string key, float value)
        {
            EditorPrefs.SetFloat($"{PrefsKeyPrefix}{key}", value);
        }
        
        /// <summary>
        /// Clears all Datra preferences
        /// </summary>
        public static void ClearAll()
        {
            // Note: Unity doesn't provide a way to clear only specific keys,
            // so this is more of a placeholder for future implementation
            Debug.LogWarning("ClearAll is not fully implemented. Preferences must be cleared manually.");
        }
        
        /// <summary>
        /// Gets the last selected tree item path
        /// </summary>
        public static string GetLastSelectedTreePath()
        {
            return GetString("LastSelectedTreePath");
        }
        
        /// <summary>
        /// Sets the last selected tree item path
        /// </summary>
        public static void SetLastSelectedTreePath(string path)
        {
            SetString("LastSelectedTreePath", path);
        }
        
        /// <summary>
        /// Gets window position and size
        /// </summary>
        public static Rect GetWindowRect(string windowName, Rect defaultRect)
        {
            var x = GetFloat($"Window_{windowName}_x", defaultRect.x);
            var y = GetFloat($"Window_{windowName}_y", defaultRect.y);
            var width = GetFloat($"Window_{windowName}_width", defaultRect.width);
            var height = GetFloat($"Window_{windowName}_height", defaultRect.height);
            return new Rect(x, y, width, height);
        }
        
        /// <summary>
        /// Sets window position and size
        /// </summary>
        public static void SetWindowRect(string windowName, Rect rect)
        {
            SetFloat($"Window_{windowName}_x", rect.x);
            SetFloat($"Window_{windowName}_y", rect.y);
            SetFloat($"Window_{windowName}_width", rect.width);
            SetFloat($"Window_{windowName}_height", rect.height);
        }
    }
}