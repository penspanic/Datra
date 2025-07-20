using System;
using System.Threading.Tasks;
using UnityEngine;
using Datra.Interfaces;

namespace Datra.Unity.Extensions
{
    /// <summary>
    /// Unity-specific extensions for IDataContext
    /// </summary>
    public static class DataContextExtensions
    {
        /// <summary>
        /// Load all data asynchronously with Unity-specific error handling
        /// </summary>
        public static async Task LoadAllAsyncUnity(this IDataContext context)
        {
            try
            {
                await context.LoadAllAsync();
                Debug.Log("[Datra] All data loaded successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Datra] Failed to load data: {e.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Save all data asynchronously with Unity-specific error handling
        /// </summary>
        public static async Task SaveAllAsyncUnity(this IDataContext context)
        {
            try
            {
                await context.SaveAllAsync();
                Debug.Log("[Datra] All data saved successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Datra] Failed to save data: {e.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Reload specific data with Unity-specific error handling
        /// </summary>
        public static async Task ReloadAsyncUnity(this IDataContext context, string dataName)
        {
            try
            {
                await context.ReloadAsync(dataName);
                Debug.Log($"[Datra] {dataName} reloaded successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Datra] Failed to reload {dataName}: {e.Message}");
                throw;
            }
        }
    }
}