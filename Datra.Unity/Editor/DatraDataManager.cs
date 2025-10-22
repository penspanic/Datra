using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Services;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor
{
    /// <summary>
    /// Central data manager for Datra Editor.
    /// Manages repositories, change trackers, provides unified event system, and handles save/reload operations.
    /// </summary>
    public class DatraDataManager
    {
        // Core data
        private readonly IDataContext _dataContext;
        private readonly LocalizationContext _localizationContext;
        private readonly Dictionary<Type, IDataRepository> _repositories;
        private readonly Dictionary<Type, IRepositoryChangeTracker> _changeTrackers;
        private readonly LocalizationChangeTracker _localizationChangeTracker;

        // Unified Event System
        /// <summary>
        /// Fired when any data changes (for view refresh)
        /// </summary>
        public event Action<Type> OnDataChanged;

        /// <summary>
        /// Fired when localization text changes
        /// </summary>
        public event Action<string, LanguageCode> OnLocalizationChanged;

        /// <summary>
        /// Fired when modified state changes (for navigation panel indicators)
        /// </summary>
        public event Action<Type, bool> OnModifiedStateChanged;

        /// <summary>
        /// Fired when save/reload operations complete successfully
        /// </summary>
        public event Action<string> OnOperationCompleted;

        /// <summary>
        /// Fired when save/reload operations fail
        /// </summary>
        public event Action<string> OnOperationFailed;

        // Properties
        public IDataContext DataContext => _dataContext;
        public LocalizationContext LocalizationContext => _localizationContext;
        public LocalizationChangeTracker LocalizationChangeTracker => _localizationChangeTracker;
        public IReadOnlyDictionary<Type, IDataRepository> Repositories => _repositories;
        public IReadOnlyDictionary<Type, IRepositoryChangeTracker> ChangeTrackers => _changeTrackers;

        public DatraDataManager(
            IDataContext dataContext,
            Dictionary<Type, IDataRepository> repositories,
            Dictionary<Type, IRepositoryChangeTracker> changeTrackers,
            LocalizationContext localizationContext,
            LocalizationChangeTracker localizationChangeTracker)
        {
            _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
            _repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
            _changeTrackers = changeTrackers ?? throw new ArgumentNullException(nameof(changeTrackers));
            _localizationContext = localizationContext ?? throw new ArgumentNullException(nameof(localizationContext));
            _localizationChangeTracker = localizationChangeTracker ?? throw new ArgumentNullException(nameof(localizationChangeTracker));

            // Subscribe to internal events
            SubscribeToInternalEvents();
        }

        private void SubscribeToInternalEvents()
        {
            // Subscribe to LocalizationContext events using the public subscription method
            if (_localizationContext != null && _localizationChangeTracker != null)
            {
                _localizationContext.SubscribeToEditorEvents(
                    onTextChanged: (key, language) =>
                    {
                        // Update change tracker for the specific language
                        if (_localizationChangeTracker.IsLanguageInitialized(language))
                        {
                            var newText = _localizationContext.GetText(key, language);
                            _localizationChangeTracker.TrackTextChange(key, newText, language);
                        }

                        // Notify listeners - fire BOTH events
                        OnLocalizationChanged?.Invoke(key, language);
                        OnDataChanged?.Invoke(typeof(LocalizationContext)); // This triggers view refresh!

                        // Modified state change is already fired by ChangeTracker's OnModifiedStateChanged
                    },
                    onKeyAdded: (key) =>
                    {
                        // Track key addition in all initialized languages
                        _localizationChangeTracker.TrackKeyAdd(key);

                        // Notify listeners
                        OnLocalizationChanged?.Invoke(key, _localizationContext.CurrentLanguageCode);

                        // Modified state change is already fired by ChangeTracker's OnModifiedStateChanged
                    },
                    onKeyDeleted: (key) =>
                    {
                        // Track key deletion in all initialized languages
                        _localizationChangeTracker.TrackKeyDelete(key);

                        // Notify listeners
                        OnLocalizationChanged?.Invoke(key, _localizationContext.CurrentLanguageCode);

                        // Modified state change is already fired by ChangeTracker's OnModifiedStateChanged
                    }
                );
            }

            // Subscribe to ChangeTracker events
            foreach (var kvp in _changeTrackers)
            {
                var dataType = kvp.Key;
                var tracker = kvp.Value;

                if (tracker is INotifyModifiedStateChanged notifyTracker)
                {
                    notifyTracker.OnModifiedStateChanged += (hasChanges) =>
                    {
                        OnModifiedStateChanged?.Invoke(dataType, hasChanges);
                    };
                }
            }

            // Subscribe to LocalizationChangeTracker events
            if (_localizationChangeTracker is INotifyModifiedStateChanged locNotifyTracker)
            {
                locNotifyTracker.OnModifiedStateChanged += (hasChanges) =>
                {
                    OnModifiedStateChanged?.Invoke(typeof(LocalizationContext), hasChanges);
                };
            }
        }

        // Repository access
        public IDataRepository GetRepository(Type type)
        {
            _repositories.TryGetValue(type, out var repository);
            return repository;
        }

        public IRepositoryChangeTracker GetChangeTracker(Type type)
        {
            _changeTrackers.TryGetValue(type, out var tracker);
            return tracker;
        }

        // State queries
        public bool HasUnsavedChanges(Type dataType)
        {
            if (dataType == typeof(LocalizationContext))
                return HasUnsavedLocalizationChanges();

            var tracker = GetChangeTracker(dataType);
            return tracker?.HasModifications ?? false;
        }

        public bool HasUnsavedLocalizationChanges()
        {
            return _localizationChangeTracker?.HasModifications() ?? false;
        }

        // Notification API - Views call these when data changes
        public void NotifyPropertyChanged(Type dataType, object itemKey, string propName, object value)
        {
            // Update ChangeTracker
            var tracker = GetChangeTracker(dataType);
            tracker?.TrackPropertyChange(itemKey, propName, value, out _);

            // Emit events
            OnDataChanged?.Invoke(dataType);

            // Modified state is automatically handled by ChangeTracker's event
        }

        public void NotifyItemAdded(Type dataType, object item)
        {
            OnDataChanged?.Invoke(dataType);

            var hasChanges = HasUnsavedChanges(dataType);
            OnModifiedStateChanged?.Invoke(dataType, hasChanges);
        }

        public void NotifyItemDeleted(Type dataType, object item)
        {
            OnDataChanged?.Invoke(dataType);

            var hasChanges = HasUnsavedChanges(dataType);
            OnModifiedStateChanged?.Invoke(dataType, hasChanges);
        }

        // Save/Reload Operations

        /// <summary>
        /// Save all modified data
        /// </summary>
        public async Task<bool> SaveAllAsync(bool forceSave = false)
        {
            try
            {
                var savedTypes = new List<Type>();
                var failedTypes = new List<(Type type, string error)>();

                // Determine which types to save
                var typesToSave = forceSave
                    ? _repositories.Keys.ToList()
                    : _repositories.Keys.Where(t => HasUnsavedChanges(t)).ToList();

                if (typesToSave.Count == 0)
                {
                    OnOperationCompleted?.Invoke("No modified data to save.");
                    return true;
                }

                // Save types
                foreach (var type in typesToSave)
                {
                    if (_repositories.TryGetValue(type, out var repository))
                    {
                        try
                        {
                            await repository.SaveAsync();
                            savedTypes.Add(type);

                            // Update change tracker baseline
                            UpdateChangeTrackerBaseline(type, repository);

                            // Emit modified state change
                            OnModifiedStateChanged?.Invoke(type, false);
                        }
                        catch (Exception repoEx)
                        {
                            failedTypes.Add((type, repoEx.Message));
                            Debug.LogError($"Failed to save {type.Name}: {repoEx.Message}");
                        }
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

                return true;
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
        public async Task<bool> SaveAsync(Type dataType, bool forceSave = false)
        {
            if (!_repositories.TryGetValue(dataType, out var repository))
            {
                OnOperationFailed?.Invoke($"Repository not found for {dataType.Name}");
                return false;
            }

            // Check if save is needed
            if (!forceSave && !HasUnsavedChanges(dataType))
            {
                OnOperationCompleted?.Invoke($"{dataType.Name} has no changes to save.");
                return true;
            }

            try
            {
                await repository.SaveAsync();

                // Update change tracker baseline
                UpdateChangeTrackerBaseline(dataType, repository);

                // Emit modified state change
                OnModifiedStateChanged?.Invoke(dataType, false);

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
            var hasModified = _repositories.Keys.Any(t => HasUnsavedChanges(t));

            if (checkModified && hasModified)
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
                    if (!await SaveAllAsync())
                    {
                        OnOperationFailed?.Invoke("Failed to save before reloading.");
                        return false;
                    }
                }
            }

            try
            {
                await _dataContext.LoadAllAsync();

                // Re-initialize all change trackers
                foreach (var kvp in _repositories)
                {
                    UpdateChangeTrackerBaseline(kvp.Key, kvp.Value);
                    OnModifiedStateChanged?.Invoke(kvp.Key, false);
                }

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
        private void UpdateChangeTrackerBaseline(Type dataType, IDataRepository repository)
        {
            if (!_changeTrackers.TryGetValue(dataType, out var tracker))
                return;

            try
            {
                // Check if it's LocalizationContext
                if (dataType == typeof(LocalizationContext) && tracker is LocalizationChangeTracker locTracker)
                {
                    locTracker.UpdateBaseline();
                    return;
                }

                // Check if it's IKeyValueDataRepository<TKey, TData>
                var keyValueRepoInterface = repository.GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                                       i.GetGenericTypeDefinition().Name == "IKeyValueDataRepository`2");

                if (keyValueRepoInterface != null)
                {
                    // Use interface to get data
                    var keyValueRepo = repository as dynamic;
                    var currentData = keyValueRepo.GetAll();

                    // Call UpdateBaseline on the tracker
                    tracker.UpdateBaseline(currentData);
                    return;
                }

                // Check if it's ISingleDataRepository<TData>
                var singleRepoInterface = repository.GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                                       i.GetGenericTypeDefinition().Name == "ISingleDataRepository`1");

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
                        tracker.UpdateBaseline(dict);
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
            var hasModified = _repositories.Keys.Any(t => HasUnsavedChanges(t));
            if (!hasModified) return Task.FromResult(true);

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
                    return Task.FromResult(false); // Caller should handle save
                case 1: // Cancel
                    return Task.FromResult(false);
                case 2: // Don't Save
                    return Task.FromResult(true);
                default:
                    return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// Interface for change trackers that can notify when modified state changes
    /// </summary>
    public interface INotifyModifiedStateChanged
    {
        event Action<bool> OnModifiedStateChanged;
    }
}
