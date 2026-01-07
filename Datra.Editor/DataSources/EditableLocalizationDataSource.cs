#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;
using Datra.Localization;
using Datra.Services;

namespace Datra.Editor.DataSources
{
    /// <summary>
    /// Editable data source for localization data.
    /// Wraps LocalizationContext and provides change tracking with consistent event notification.
    ///
    /// Follows the same patterns as EditableKeyValueDataSource:
    /// - All state changes use ExecuteWithNotification
    /// - RefreshBaseline is called after save
    /// - OnModifiedStateChanged is always fired appropriately
    /// </summary>
    public class EditableLocalizationDataSource : EditableDataSourceBase, IEditableLocalizationDataSource
    {
        private readonly LocalizationContext _context;
        private readonly Dictionary<LanguageCode, LanguageBaseline> _baselines = new();
        private readonly HashSet<string> _addedKeys = new();
        private readonly HashSet<string> _deletedKeys = new();

        public event Action<string, LanguageCode>? OnTextChanged;
        public event Action<LanguageCode>? OnLanguageChanged;

        public EditableLocalizationDataSource(LocalizationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #region Properties

        public LanguageCode CurrentLanguage => _context.CurrentLanguageCode;

        public IReadOnlyList<LanguageCode> LoadedLanguages =>
            _context.GetLoadedLanguages().ToList();

        public IReadOnlyList<LanguageCode> AvailableLanguages =>
            _context.GetAvailableLanguages().ToList();

        /// <summary>
        /// Access to underlying LocalizationContext for operations not exposed by this class.
        /// </summary>
        public LocalizationContext Context => _context;

        /// <summary>
        /// Check if baseline is initialized for a language.
        /// </summary>
        public bool IsLanguageInitialized(LanguageCode language) =>
            _baselines.ContainsKey(language);

        public override bool HasModifications =>
            _addedKeys.Count > 0 ||
            _deletedKeys.Count > 0 ||
            _baselines.Values.Any(b => b.HasChanges);

        public override int Count => GetAllKeys().Count();

        #endregion

        #region IEditableDataSource Implementation

        public override IEnumerable<object> EnumerateItems()
        {
            foreach (var key in GetAllKeys())
            {
                yield return new KeyValuePair<string, string>(key, GetText(key));
            }
        }

        public override ItemState GetItemState(object key)
        {
            if (key is string strKey)
                return GetKeyState(strKey);
            return ItemState.Unchanged;
        }

        public override bool IsPropertyModified(object key, string propertyName)
        {
            if (key is string strKey && propertyName == "Text")
                return IsKeyModified(strKey);
            return false;
        }

        public override IEnumerable<string> GetModifiedProperties(object key)
        {
            if (key is string strKey && IsKeyModified(strKey))
                yield return "Text";
        }

        public override object? GetPropertyBaselineValue(object key, string propertyName)
        {
            if (key is string strKey && propertyName == "Text")
                return GetBaselineText(strKey);
            return null;
        }

        protected override void RevertInternal()
        {
            // Revert all text changes from baseline
            foreach (var kvp in _baselines)
            {
                var language = kvp.Key;
                var baseline = kvp.Value;

                foreach (var textKvp in baseline.GetAllBaseline())
                {
                    _context.SetText(textKvp.Key, textKvp.Value, language);
                }

                baseline.ClearChanges();
            }

            // Note: Added/Deleted keys are harder to revert since they may have modified
            // the underlying context. For now, just clear tracking.
            _addedKeys.Clear();
            _deletedKeys.Clear();
        }

        protected override async Task SaveInternalAsync()
        {
            // Apply deletions to context
            foreach (var key in _deletedKeys.ToList())
            {
                await _context.DeleteKeyAsync(key);
            }

            // Save all loaded languages
            foreach (var language in LoadedLanguages)
            {
                await _context.SaveLanguageAsync(language);
            }
        }

        protected override void RefreshBaselineInternal()
        {
            // Re-initialize all baselines from current context state
            foreach (var language in LoadedLanguages)
            {
                InitializeBaselineInternal(language);
            }

            _addedKeys.Clear();
            _deletedKeys.Clear();
        }

        #endregion

        #region IEditableLocalizationDataSource Implementation

        public IEnumerable<string> GetAllKeys()
        {
            return _context.GetAllKeys()
                .Where(k => !_deletedKeys.Contains(k))
                .Concat(_addedKeys.Where(k => !_context.HasKey(k)));
        }

        public string GetText(string key)
        {
            return _context.GetText(key);
        }

        public string GetText(string key, LanguageCode language)
        {
            return _context.GetText(key, language);
        }

        public void SetText(string key, string value)
        {
            SetText(key, value, CurrentLanguage);
        }

        public void SetText(string key, string value, LanguageCode language)
        {
            ExecuteWithNotification(() =>
            {
                _context.SetText(key, value, language);

                if (_baselines.TryGetValue(language, out var baseline))
                {
                    baseline.TrackChange(key, value);
                }

                OnTextChanged?.Invoke(key, language);
            });
        }

        public void AddKey(string key, string description = "", string category = "")
        {
            ExecuteWithNotification(() =>
            {
                // Add to context synchronously (we'll save async later)
                _ = _context.AddKeyAsync(key, description, category);
                _addedKeys.Add(key);
                _deletedKeys.Remove(key);

                // Track in all baselines as added
                foreach (var baseline in _baselines.Values)
                {
                    baseline.TrackAdd(key);
                }

                OnTextChanged?.Invoke(key, CurrentLanguage);
            });
        }

        public void DeleteKey(string key)
        {
            ExecuteWithNotification(() =>
            {
                if (_addedKeys.Contains(key))
                {
                    // Was added in this session - just remove tracking
                    _addedKeys.Remove(key);
                }
                else
                {
                    // Mark for deletion
                    _deletedKeys.Add(key);
                }

                // Track deletion in all baselines
                foreach (var baseline in _baselines.Values)
                {
                    baseline.TrackDelete(key);
                }

                OnTextChanged?.Invoke(key, CurrentLanguage);
            });
        }

        public bool ContainsKey(string key)
        {
            if (_deletedKeys.Contains(key))
                return false;
            return _context.HasKey(key) || _addedKeys.Contains(key);
        }

        public bool HasLanguageModifications(LanguageCode language)
        {
            if (_addedKeys.Count > 0 || _deletedKeys.Count > 0)
                return true;

            return _baselines.TryGetValue(language, out var baseline) && baseline.HasChanges;
        }

        public bool IsKeyModified(string key)
        {
            return IsKeyModified(key, CurrentLanguage);
        }

        public bool IsKeyModified(string key, LanguageCode language)
        {
            if (_addedKeys.Contains(key) || _deletedKeys.Contains(key))
                return true;

            return _baselines.TryGetValue(language, out var baseline) && baseline.IsModified(key);
        }

        public string? GetBaselineText(string key)
        {
            return GetBaselineText(key, CurrentLanguage);
        }

        public string? GetBaselineText(string key, LanguageCode language)
        {
            if (_baselines.TryGetValue(language, out var baseline))
                return baseline.GetBaselineValue(key);
            return null;
        }

        public IEnumerable<string> GetModifiedKeys()
        {
            return GetModifiedKeys(CurrentLanguage);
        }

        public IEnumerable<string> GetModifiedKeys(LanguageCode language)
        {
            if (_baselines.TryGetValue(language, out var baseline))
                return baseline.GetModifiedKeys();
            return Enumerable.Empty<string>();
        }

        public IEnumerable<string> GetAddedKeys() => _addedKeys;

        public IEnumerable<string> GetDeletedKeys() => _deletedKeys;

        public async Task SwitchLanguageAsync(LanguageCode language)
        {
            if (!LoadedLanguages.Contains(language))
            {
                await _context.LoadLanguageAsync(language);
                InitializeBaseline(language);
            }

            OnLanguageChanged?.Invoke(language);
        }

        public void InitializeBaseline(LanguageCode language)
        {
            ExecuteWithNotification(() => InitializeBaselineInternal(language));
        }

        private void InitializeBaselineInternal(LanguageCode language)
        {
            var baseline = new LanguageBaseline();
            var keys = _context.GetAllKeys();

            foreach (var key in keys)
            {
                var text = _context.GetText(key, language);
                baseline.SetBaseline(key, text);
            }

            _baselines[language] = baseline;
        }

        public void RevertKey(string key)
        {
            ExecuteWithNotification(() =>
            {
                var language = CurrentLanguage;
                if (_baselines.TryGetValue(language, out var baseline))
                {
                    var baselineValue = baseline.GetBaselineValue(key);
                    if (baselineValue != null)
                    {
                        _context.SetText(key, baselineValue, language);
                        baseline.RevertKey(key);
                    }
                }

                OnTextChanged?.Invoke(key, language);
            });
        }

        public void RevertCurrentLanguage()
        {
            ExecuteWithNotification(() =>
            {
                var language = CurrentLanguage;
                if (_baselines.TryGetValue(language, out var baseline))
                {
                    foreach (var kvp in baseline.GetAllBaseline())
                    {
                        _context.SetText(kvp.Key, kvp.Value, language);
                    }
                    baseline.ClearChanges();
                }
            });
        }

        /// <summary>
        /// Get the key from an item. For localization, the key is the locale key string.
        /// </summary>
        public override object? GetItemKey(object item)
        {
            if (item == null) return null;

            // Handle KeyValuePair<string, string> (key, text)
            if (item is KeyValuePair<string, string> kvp)
            {
                return kvp.Key;
            }

            // Handle string (direct key)
            if (item is string strKey)
            {
                return strKey;
            }

            return null;
        }

        /// <summary>
        /// Track property change (non-generic version for IEditableDataSource).
        /// For localization, propertyName is typically "Text".
        /// </summary>
        public override void TrackPropertyChange(object key, string propertyName, object? newValue, out bool isPropertyModified)
        {
            isPropertyModified = false;

            if (key is string strKey && propertyName == "Text" && newValue is string textValue)
            {
                SetText(strKey, textValue);
                isPropertyModified = IsKeyModified(strKey);
            }
        }

        public async Task SaveCurrentLanguageAsync()
        {
            bool hadModifications = HasModifications;

            await _context.SaveCurrentLanguageAsync();

            // Refresh only current language baseline
            InitializeBaselineInternal(CurrentLanguage);

            NotifyIfStateChanged(hadModifications);
        }

        #endregion

        #region Helper Methods

        private ItemState GetKeyState(string key)
        {
            if (_deletedKeys.Contains(key))
                return ItemState.Deleted;

            if (_addedKeys.Contains(key))
                return ItemState.Added;

            if (IsKeyModified(key))
                return ItemState.Modified;

            return ItemState.Unchanged;
        }

        #endregion

        #region Inner Classes

        /// <summary>
        /// Tracks baseline and changes for a single language.
        /// </summary>
        private class LanguageBaseline
        {
            private readonly Dictionary<string, string> _baseline = new();
            private readonly Dictionary<string, string> _current = new();
            private readonly HashSet<string> _addedKeys = new();
            private readonly HashSet<string> _deletedKeys = new();

            public bool HasChanges =>
                _addedKeys.Count > 0 ||
                _deletedKeys.Count > 0 ||
                _current.Any(kvp =>
                    _baseline.TryGetValue(kvp.Key, out var baseVal) && baseVal != kvp.Value);

            public void SetBaseline(string key, string value)
            {
                _baseline[key] = value;
                _current[key] = value;
            }

            public void TrackChange(string key, string newValue)
            {
                _current[key] = newValue;
            }

            public void TrackAdd(string key)
            {
                if (!_baseline.ContainsKey(key))
                {
                    _addedKeys.Add(key);
                    _deletedKeys.Remove(key);
                }
                _current[key] = string.Empty;
            }

            public void TrackDelete(string key)
            {
                _current.Remove(key);

                if (_addedKeys.Contains(key))
                {
                    _addedKeys.Remove(key);
                }
                else if (_baseline.ContainsKey(key))
                {
                    _deletedKeys.Add(key);
                }
            }

            public bool IsModified(string key)
            {
                if (_addedKeys.Contains(key) || _deletedKeys.Contains(key))
                    return true;

                if (_baseline.TryGetValue(key, out var baseVal) &&
                    _current.TryGetValue(key, out var currVal))
                {
                    return baseVal != currVal;
                }

                return false;
            }

            public string? GetBaselineValue(string key)
            {
                return _baseline.TryGetValue(key, out var value) ? value : null;
            }

            public IEnumerable<string> GetModifiedKeys()
            {
                return _current
                    .Where(kvp =>
                        _baseline.TryGetValue(kvp.Key, out var baseVal) && baseVal != kvp.Value)
                    .Select(kvp => kvp.Key);
            }

            public IReadOnlyDictionary<string, string> GetAllBaseline() => _baseline;

            public void RevertKey(string key)
            {
                if (_baseline.TryGetValue(key, out var baseValue))
                {
                    _current[key] = baseValue;
                }
            }

            public void ClearChanges()
            {
                _current.Clear();
                foreach (var kvp in _baseline)
                {
                    _current[kvp.Key] = kvp.Value;
                }
                _addedKeys.Clear();
                _deletedKeys.Clear();
            }
        }

        #endregion
    }
}
