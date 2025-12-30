using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Services;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor
{
    /// <summary>
    /// Central data manager for Datra Editor.
    /// Manages repositories, data sources, provides unified event system, and handles save/reload operations.
    /// </summary>
    public class DatraDataManager
    {
        // Core data
        private readonly IDataContext _dataContext;
        private readonly LocalizationContext _localizationContext;
        private readonly Dictionary<Type, IDataRepository> _repositories;
        private readonly Dictionary<Type, IEditableDataSource> _dataSources;
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
        public IReadOnlyDictionary<Type, IEditableDataSource> DataSources => _dataSources;

        public DatraDataManager(
            IDataContext dataContext,
            Dictionary<Type, IDataRepository> repositories,
            Dictionary<Type, IEditableDataSource> dataSources,
            LocalizationContext localizationContext,
            LocalizationChangeTracker localizationChangeTracker)
        {
            _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
            _repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
            _dataSources = dataSources ?? new Dictionary<Type, IEditableDataSource>();
            // Localization is optional (EnableLocalization = false in DatraConfiguration)
            _localizationContext = localizationContext;
            _localizationChangeTracker = localizationChangeTracker;

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

            // Subscribe to DataSource events
            foreach (var kvp in _dataSources)
            {
                var dataType = kvp.Key;
                var dataSource = kvp.Value;

                dataSource.OnModifiedStateChanged += (hasChanges) =>
                {
                    OnModifiedStateChanged?.Invoke(dataType, hasChanges);
                };
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

        public IEditableDataSource GetDataSource(Type type)
        {
            _dataSources.TryGetValue(type, out var dataSource);
            return dataSource;
        }

        // State queries
        public bool HasUnsavedChanges(Type dataType)
        {
            if (dataType == typeof(LocalizationContext))
                return HasUnsavedLocalizationChanges();

            // Check dataSource for modifications
            if (_dataSources.TryGetValue(dataType, out var dataSource))
            {
                return dataSource.HasModifications;
            }

            return false;
        }

        public bool HasUnsavedLocalizationChanges()
        {
            return _localizationChangeTracker?.HasModifications() ?? false;
        }

        // Notification API - Views call these when data changes
        public void NotifyPropertyChanged(Type dataType, object itemKey, string propName, object value)
        {
            // DataSource tracks changes internally when TrackPropertyChange is called
            // This method is for notifying other listeners
            OnDataChanged?.Invoke(dataType);
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
                    if (_dataSources.TryGetValue(type, out var dataSource))
                    {
                        try
                        {
                            await dataSource.SaveAsync();
                            savedTypes.Add(type);

                            // Emit modified state change
                            OnModifiedStateChanged?.Invoke(type, false);
                        }
                        catch (Exception repoEx)
                        {
                            failedTypes.Add((type, repoEx.Message));
                            Debug.LogError($"Failed to save {type.Name}: {repoEx.Message}");
                        }
                    }
                    else if (_repositories.TryGetValue(type, out var repository))
                    {
                        // Fallback for types without dataSource
                        try
                        {
                            await repository.SaveAsync();
                            savedTypes.Add(type);
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
            // Check if save is needed
            if (!forceSave && !HasUnsavedChanges(dataType))
            {
                OnOperationCompleted?.Invoke($"{dataType.Name} has no changes to save.");
                return true;
            }

            try
            {
                // Prefer dataSource for saving (handles transactional changes)
                if (_dataSources.TryGetValue(dataType, out var dataSource))
                {
                    await dataSource.SaveAsync();
                }
                else if (_repositories.TryGetValue(dataType, out var repository))
                {
                    // Fallback to repository
                    await repository.SaveAsync();
                }
                else
                {
                    OnOperationFailed?.Invoke($"Repository not found for {dataType.Name}");
                    return false;
                }

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

                // Refresh all data sources to get new baseline from repository
                foreach (var kvp in _dataSources)
                {
                    var dataType = kvp.Key;
                    var dataSource = kvp.Value;

                    // Call RefreshBaseline if available
                    var refreshMethod = dataSource.GetType().GetMethod("RefreshBaseline");
                    refreshMethod?.Invoke(dataSource, null);

                    OnModifiedStateChanged?.Invoke(dataType, false);
                }

                // Update localization tracker baseline
                if (_localizationChangeTracker != null)
                {
                    _localizationChangeTracker.UpdateBaseline();
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
}
