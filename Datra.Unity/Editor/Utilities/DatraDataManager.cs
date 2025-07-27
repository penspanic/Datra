using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Datra.Interfaces;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Manages data operations (save/reload) for Datra editor windows
    /// </summary>
    public class DatraDataManager
    {
        private readonly IDataContext dataContext;
        private readonly HashSet<Type> modifiedTypes = new HashSet<Type>();
        
        public event Action<Type> OnDataModified;
        public event Action<bool> OnModifiedStateChanged;
        public event Action<string> OnOperationCompleted;
        public event Action<string> OnOperationFailed;
        
        public IReadOnlyCollection<Type> ModifiedTypes => modifiedTypes;
        public bool HasModifiedData => modifiedTypes.Count > 0;
        
        public DatraDataManager(IDataContext context)
        {
            dataContext = context ?? throw new ArgumentNullException(nameof(context));
        }
        
        /// <summary>
        /// Mark a data type as modified
        /// </summary>
        public void MarkAsModified(Type dataType)
        {
            if (!modifiedTypes.Contains(dataType))
            {
                modifiedTypes.Add(dataType);
                OnDataModified?.Invoke(dataType);
                OnModifiedStateChanged?.Invoke(true);
            }
        }
        
        /// <summary>
        /// Clear modified state for a data type
        /// </summary>
        public void ClearModifiedState(Type dataType)
        {
            if (modifiedTypes.Remove(dataType))
            {
                OnModifiedStateChanged?.Invoke(modifiedTypes.Count > 0);
            }
        }
        
        /// <summary>
        /// Clear all modified states
        /// </summary>
        public void ClearAllModifiedStates()
        {
            modifiedTypes.Clear();
            OnModifiedStateChanged?.Invoke(false);
        }
        
        /// <summary>
        /// Save all modified data
        /// </summary>
        public async Task<bool> SaveAllAsync(Dictionary<Type, object> repositories)
        {
            if (dataContext == null) return false;
            
            try
            {
                var savedTypes = new List<Type>();
                var failedTypes = new List<(Type type, string error)>();
                
                // Save only modified types
                foreach (var type in modifiedTypes)
                {
                    if (repositories.TryGetValue(type, out var repository))
                    {
                        try
                        {
                            if (await SaveRepositoryAsync(repository, type))
                            {
                                savedTypes.Add(type);
                            }
                            else
                            {
                                failedTypes.Add((type, "No save method found"));
                            }
                        }
                        catch (Exception repoEx)
                        {
                            failedTypes.Add((type, repoEx.Message));
                            Debug.LogError($"Failed to save {type.Name}: {repoEx.Message}");
                        }
                    }
                }
                
                // Clear modified states for successfully saved types
                foreach (var type in savedTypes)
                {
                    ClearModifiedState(type);
                }
                
                // Report results
                if (failedTypes.Count > 0)
                {
                    var errorMessage = $"Failed to save {failedTypes.Count} type(s):\n";
                    foreach (var (type, error) in failedTypes)
                    {
                        errorMessage += $"- {type.Name}: {error}\n";
                    }
                    OnOperationFailed?.Invoke(errorMessage);
                    return false;
                }
                else if (savedTypes.Count > 0)
                {
                    OnOperationCompleted?.Invoke($"Saved {savedTypes.Count} data file(s) successfully!");
                    return true;
                }
                else
                {
                    OnOperationCompleted?.Invoke("No modified data to save.");
                    return true;
                }
            }
            catch (Exception e)
            {
                OnOperationFailed?.Invoke($"Failed to save data: {e.Message}");
                Debug.LogError($"Failed to save data: {e}");
                return false;
            }
        }
        
        /// <summary>
        /// Save a specific data type
        /// </summary>
        public async Task<bool> SaveAsync(Type dataType, object repository)
        {
            if (dataContext == null || repository == null) return false;
            
            try
            {
                if (await SaveRepositoryAsync(repository, dataType))
                {
                    ClearModifiedState(dataType);
                    OnOperationCompleted?.Invoke($"{dataType.Name} saved successfully!");
                    return true;
                }
                else
                {
                    OnOperationFailed?.Invoke($"Failed to save {dataType.Name}: No save method found");
                    return false;
                }
            }
            catch (Exception e)
            {
                OnOperationFailed?.Invoke($"Failed to save {dataType.Name}: {e.Message}");
                Debug.LogError($"Failed to save {dataType.Name}: {e}");
                return false;
            }
        }
        
        /// <summary>
        /// Reload all data
        /// </summary>
        public async Task<bool> ReloadAllAsync(bool checkModified = true)
        {
            if (dataContext == null) return false;
            
            if (checkModified && modifiedTypes.Count > 0)
            {
                var result = EditorUtility.DisplayDialogComplex(
                    "Unsaved Changes",
                    "You have unsaved changes. What would you like to do?",
                    "Save and Reload",
                    "Cancel",
                    "Discard and Reload"
                );
                
                if (result == 1) return false; // Cancel
                if (result == 0) // Save first
                {
                    // We need repositories to save, so this should be handled by the caller
                    OnOperationFailed?.Invoke("Please save your changes before reloading.");
                    return false;
                }
            }
            
            try
            {
                await dataContext.LoadAllAsync();
                ClearAllModifiedStates();
                OnOperationCompleted?.Invoke("Data reloaded successfully!");
                return true;
            }
            catch (Exception e)
            {
                OnOperationFailed?.Invoke($"Failed to reload data: {e.Message}");
                Debug.LogError($"Failed to reload data: {e}");
                return false;
            }
        }
        
        /// <summary>
        /// Helper method to save a repository
        /// </summary>
        private async Task<bool> SaveRepositoryAsync(object repository, Type dataType)
        {
            // Try to find SaveAsync method on the repository
            var saveMethod = repository.GetType().GetMethod("SaveAsync");
            if (saveMethod != null)
            {
                var task = saveMethod.Invoke(repository, null) as Task;
                if (task != null)
                {
                    await task;
                    return true;
                }
            }
            
            // Fallback: Try to save through context with specific type
            var contextType = dataContext.GetType();
            var saveSpecificMethod = contextType.GetMethod("SaveAsync", new[] { typeof(Type) });
            if (saveSpecificMethod != null)
            {
                var task = saveSpecificMethod.Invoke(dataContext, new object[] { dataType }) as Task;
                if (task != null)
                {
                    await task;
                    return true;
                }
            }
            
            // Last resort: Try generic SaveAsync method
            var genericSaveMethod = contextType.GetMethods()
                .FirstOrDefault(m => m.Name == "SaveAsync" && m.IsGenericMethodDefinition);
            if (genericSaveMethod != null)
            {
                var task = genericSaveMethod.MakeGenericMethod(dataType).Invoke(dataContext, null) as Task;
                if (task != null)
                {
                    await task;
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if user wants to save before closing/switching
        /// </summary>
        public async Task<bool> CheckUnsavedChangesAsync(string action = "continue")
        {
            if (modifiedTypes.Count == 0) return true;
            
            var result = EditorUtility.DisplayDialogComplex(
                "Unsaved Changes",
                $"You have unsaved changes. Save before {action}?",
                "Save",
                "Cancel",
                "Don't Save"
            );
            
            switch (result)
            {
                case 0: // Save
                    // This needs to be handled by the caller since we need repositories
                    return false;
                case 1: // Cancel
                    return false;
                case 2: // Don't Save
                    return true;
                default:
                    return false;
            }
        }
    }
}