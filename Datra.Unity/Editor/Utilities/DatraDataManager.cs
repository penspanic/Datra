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

        // Change tracking storage
        private readonly Dictionary<Type, object> changeTrackers = new Dictionary<Type, object>();

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
        /// Register a change tracker for a data type
        /// </summary>
        public void RegisterChangeTracker<TKey, TData>(Type dataType, RepositoryChangeTracker<TKey, TData> tracker)
            where TKey : notnull
            where TData : class
        {
            changeTrackers[dataType] = tracker;
        }

        /// <summary>
        /// Register a change tracker for a data type (non-generic version for reflection-free usage)
        /// </summary>
        public void RegisterChangeTracker(Type dataType, IRepositoryChangeTracker tracker, Type keyType, Type valueType)
        {
            changeTrackers[dataType] = tracker;
        }

        /// <summary>
        /// Register a localization change tracker
        /// </summary>
        public void RegisterLocalizationChangeTracker(Type dataType, LocalizationChangeTracker tracker)
        {
            changeTrackers[dataType] = tracker;
        }

        /// <summary>
        /// Get change tracker for a data type
        /// </summary>
        public RepositoryChangeTracker<TKey, TData> GetChangeTracker<TKey, TData>(Type dataType)
            where TKey : notnull
            where TData : class
        {
            if (changeTrackers.TryGetValue(dataType, out var tracker))
                return tracker as RepositoryChangeTracker<TKey, TData>;
            return null;
        }

        /// <summary>
        /// Get localization change tracker
        /// </summary>
        public LocalizationChangeTracker GetLocalizationChangeTracker(Type dataType)
        {
            if (changeTrackers.TryGetValue(dataType, out var tracker))
                return tracker as LocalizationChangeTracker;
            return null;
        }

        /// <summary>
        /// Check if a type has modifications based on its change tracker
        /// </summary>
        public bool HasModificationsFromTracker(Type dataType)
        {
            if (!changeTrackers.TryGetValue(dataType, out var tracker))
                return false;

            // Use interface
            if (tracker is IRepositoryChangeTracker changeTracker)
            {
                return changeTracker.HasModifications;
            }

            return false;
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
        public async Task<bool> SaveAllAsync(Dictionary<Type, IDataRepository> repositories, bool forceSave = false)
        {
            if (dataContext == null) return false;
            
            try
            {
                var savedTypes = new List<Type>();
                var failedTypes = new List<(Type type, string error)>();

                // Determine which types to save
                var typesToSave = forceSave
                    ? repositories.Keys.ToList()
                    : modifiedTypes.ToList();

                // Save types
                foreach (var type in typesToSave)
                {
                    if (repositories.TryGetValue(type, out var repository))
                    {
                        try
                        {
                            if (repository is IDataRepository dataRepository)
                            {
                                await dataRepository.SaveAsync();
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
                
                // Clear modified states and update trackers for successfully saved types
                foreach (var type in savedTypes)
                {
                    ClearModifiedState(type);

                    // Update change tracker baseline
                    if (repositories.TryGetValue(type, out var repository))
                    {
                        UpdateChangeTrackerBaseline(type, repository);
                    }
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
        public async Task<bool> SaveAsync(Type dataType, IDataRepository repository, bool forceSave = false)
        {
            if (dataContext == null || repository == null) return false;

            // Check if save is needed
            if (!forceSave && !modifiedTypes.Contains(dataType))
            {
                OnOperationCompleted?.Invoke($"{dataType.Name} has no changes to save.");
                return true;
            }

            try
            {
                await repository.SaveAsync();
                ClearModifiedState(dataType);

                // Update change tracker baseline
                UpdateChangeTrackerBaseline(dataType, repository);

                var message = forceSave
                    ? $"{dataType.Name} force saved successfully!"
                    : $"{dataType.Name} saved successfully!";
                OnOperationCompleted?.Invoke(message);
                return true;
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
        /// Update change tracker baseline after successful save
        /// </summary>
        private void UpdateChangeTrackerBaseline(Type dataType, object repository)
        {
            if (!changeTrackers.TryGetValue(dataType, out var tracker))
                return;

            try
            {
                // Check if it's IKeyValueDataRepository<TKey, TData>
                var keyValueRepoInterface = repository.GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name == "IKeyValueDataRepository`2");

                if (keyValueRepoInterface != null)
                {
                    // Use interface to get data
                    var keyValueRepo = repository as dynamic;
                    var currentData = keyValueRepo.GetAll();

                    // Call UpdateBaseline on the tracker
                    var changeTracker = tracker as IRepositoryChangeTracker;
                    changeTracker?.UpdateBaseline(currentData);
                    return;
                }

                // Check if it's ISingleDataRepository<TData>
                var singleRepoInterface = repository.GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name == "ISingleDataRepository`1");

                if (singleRepoInterface != null)
                {
                    var valueType = singleRepoInterface.GetGenericArguments()[0];

                    // Use interface to get data
                    var singleRepo = repository as dynamic;
                    var data = singleRepo.Get();

                    // Create a dictionary with single item using "single" as key
                    var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
                    var dict = Activator.CreateInstance(dictType) as System.Collections.IDictionary;

                    if (dict != null && data != null)
                    {
                        dict.Add("single", data);

                        // Call UpdateBaseline on the tracker
                        var changeTracker = tracker as IRepositoryChangeTracker;
                        changeTracker?.UpdateBaseline(dict);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to update change tracker baseline for {dataType.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if user wants to save before closing/switching
        /// </summary>
        public Task<bool> CheckUnsavedChangesAsync(string action = "continue")
        {
            if (modifiedTypes.Count == 0) return Task.FromResult(true);
            
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
                    return Task.FromResult(false);
                case 1: // Cancel
                    return Task.FromResult(false);
                case 2: // Don't Save
                    return Task.FromResult(true);
                default:
                    return Task.FromResult(false);
            }
        }
    }
}