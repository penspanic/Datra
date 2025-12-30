#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datra.Editor.Interfaces;
using Datra.Localization;
using Datra.Services;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Editor-only change tracker for LocalizationContext.
    /// Tracks changes per language without modifying runtime code.
    /// </summary>
    public class LocalizationChangeTracker : INotifyModifiedStateChanged
    {
        private readonly LocalizationContext _context;
        private readonly Dictionary<LanguageCode, LanguageTextTracker> _languageTrackers;

        public event Action<bool>? OnModifiedStateChanged;

        public LocalizationChangeTracker(LocalizationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _languageTrackers = new Dictionary<LanguageCode, LanguageTextTracker>();
        }

        /// <summary>
        /// Helper method to notify modified state change if it changed
        /// </summary>
        private void CheckAndNotifyModifiedStateChange(bool hadChanges)
        {
            bool hasChanges = HasModifications();
            if (hadChanges != hasChanges)
            {
                OnModifiedStateChanged?.Invoke(hasChanges);
            }
        }

        /// <summary>
        /// Check if a language tracker is already initialized
        /// </summary>
        public bool IsLanguageInitialized(LanguageCode languageCode)
        {
            return _languageTrackers.ContainsKey(languageCode);
        }

        /// <summary>
        /// Initialize tracker for a language (call after LoadLanguageAsync)
        /// </summary>
        public void InitializeLanguage(LanguageCode languageCode)
        {
            var tracker = new LanguageTextTracker();

            // Build dictionary from context
            var keys = _context.GetAllKeys();
            var data = new Dictionary<string, string>();
            foreach (var key in keys)
            {
                var text = _context.GetText(key);
                data[key] = text ?? string.Empty;
            }

            tracker.InitializeBaseline(data);
            _languageTrackers[languageCode] = tracker;
        }

        /// <summary>
        /// Track text change for current language
        /// </summary>
        public void TrackTextChange(string key, string newValue)
        {
            TrackTextChange(key, newValue, _context.CurrentLanguageCode);
        }

        /// <summary>
        /// Track text change for specific language
        /// </summary>
        public void TrackTextChange(string key, string newValue, LanguageCode languageCode)
        {
            bool hadChanges = HasModifications();

            if (_languageTrackers.TryGetValue(languageCode, out var tracker))
            {
                tracker.TrackChange(key, newValue ?? string.Empty);
            }

            CheckAndNotifyModifiedStateChange(hadChanges);
        }

        /// <summary>
        /// Track key addition
        /// </summary>
        public void TrackKeyAdd(string key)
        {
            bool hadChanges = HasModifications();

            foreach (var tracker in _languageTrackers.Values)
            {
                tracker.TrackAdd(key, string.Empty);
            }

            CheckAndNotifyModifiedStateChange(hadChanges);
        }

        /// <summary>
        /// Track key deletion
        /// </summary>
        public void TrackKeyDelete(string key)
        {
            bool hadChanges = HasModifications();

            foreach (var tracker in _languageTrackers.Values)
            {
                tracker.TrackDelete(key);
            }

            CheckAndNotifyModifiedStateChange(hadChanges);
        }

        /// <summary>
        /// Check if current language has modifications
        /// </summary>
        public bool HasModifications()
        {
            return HasModifications(_context.CurrentLanguageCode);
        }

        /// <summary>
        /// Check if a specific language has modifications
        /// </summary>
        public bool HasModifications(LanguageCode languageCode)
        {
            return _languageTrackers.TryGetValue(languageCode, out var tracker)
                && tracker.HasModifications;
        }

        /// <summary>
        /// Get modified keys for current language
        /// </summary>
        public IEnumerable<string> GetModifiedKeys()
        {
            return GetModifiedKeys(_context.CurrentLanguageCode);
        }

        /// <summary>
        /// Get modified keys for a specific language
        /// </summary>
        public IEnumerable<string> GetModifiedKeys(LanguageCode languageCode)
        {
            if (_languageTrackers.TryGetValue(languageCode, out var tracker))
                return tracker.GetModifiedKeys();
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Get added keys for current language
        /// </summary>
        public IEnumerable<string> GetAddedKeys()
        {
            return GetAddedKeys(_context.CurrentLanguageCode);
        }

        /// <summary>
        /// Get added keys for a specific language
        /// </summary>
        public IEnumerable<string> GetAddedKeys(LanguageCode languageCode)
        {
            if (_languageTrackers.TryGetValue(languageCode, out var tracker))
                return tracker.GetAddedKeys();
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Get deleted keys for current language
        /// </summary>
        public IEnumerable<string> GetDeletedKeys()
        {
            return GetDeletedKeys(_context.CurrentLanguageCode);
        }

        /// <summary>
        /// Get deleted keys for a specific language
        /// </summary>
        public IEnumerable<string> GetDeletedKeys(LanguageCode languageCode)
        {
            if (_languageTrackers.TryGetValue(languageCode, out var tracker))
                return tracker.GetDeletedKeys();
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Check if a key is modified in current language
        /// </summary>
        public bool IsModified(string key)
        {
            if (_languageTrackers.TryGetValue(_context.CurrentLanguageCode, out var tracker))
                return tracker.IsModified(key);
            return false;
        }

        /// <summary>
        /// Check if a property is modified. For localization, we only track "Text" property.
        /// </summary>
        public bool IsPropertyModified(string key, string propertyName)
        {
            if (propertyName != "Text") return false;
            return IsModified(key);
        }

        /// <summary>
        /// Track property change. For localization, we only track "Text" property.
        /// </summary>
        public void TrackPropertyChange(string key, string propertyName, object? newValue, out bool isModified)
        {
            isModified = false;
            if (propertyName != "Text") return;

            var newText = newValue as string ?? string.Empty;
            TrackTextChange(key, newText);

            // Check if it's still modified after tracking
            isModified = IsModified(key);
        }

        /// <summary>
        /// Get property baseline value. For localization, returns baseline text for "Text" property.
        /// </summary>
        public object? GetPropertyBaselineValue(string key, string propertyName)
        {
            if (propertyName != "Text") return null;
            return GetBaselineText(key);
        }

        /// <summary>
        /// Get baseline text for a key in current language
        /// </summary>
        public string? GetBaselineText(string key)
        {
            return GetBaselineText(_context.CurrentLanguageCode, key);
        }

        /// <summary>
        /// Get baseline text for a key in specific language
        /// </summary>
        public string? GetBaselineText(LanguageCode languageCode, string key)
        {
            if (_languageTrackers.TryGetValue(languageCode, out var tracker))
                return tracker.GetBaselineValue(key);
            return null;
        }

        /// <summary>
        /// Revert all changes in current language
        /// </summary>
        public void RevertAll()
        {
            RevertAll(_context.CurrentLanguageCode);
        }

        /// <summary>
        /// Revert all changes in specific language
        /// </summary>
        public void RevertAll(LanguageCode languageCode)
        {
            if (_languageTrackers.TryGetValue(languageCode, out var tracker))
            {
                var baselineData = tracker.GetBaselineData();

                // Apply baseline to context
                foreach (var kvp in baselineData)
                {
                    _context.SetText(kvp.Key, kvp.Value);
                }

                // Re-initialize tracker with baseline
                InitializeLanguage(languageCode);
            }
        }

        /// <summary>
        /// Revert a specific key in current language
        /// </summary>
        public void RevertKey(string key)
        {
            var languageCode = _context.CurrentLanguageCode;
            if (_languageTrackers.TryGetValue(languageCode, out var tracker))
            {
                var baselineValue = tracker.GetBaselineValue(key);
                if (baselineValue != null)
                {
                    _context.SetText(key, baselineValue);
                    tracker.TrackChange(key, baselineValue);
                }
            }
        }

        /// <summary>
        /// Update baseline to current state for current language (call after save)
        /// </summary>
        public void UpdateBaseline()
        {
            UpdateBaseline(_context.CurrentLanguageCode);
        }

        /// <summary>
        /// Update baseline to current state for specific language (call after save)
        /// </summary>
        public void UpdateBaseline(LanguageCode languageCode)
        {
            InitializeLanguage(languageCode);
        }

        /// <summary>
        /// Synchronize tracker with context
        /// </summary>
        public void SyncWithContext()
        {
            SyncWithContext(_context.CurrentLanguageCode);
        }

        /// <summary>
        /// Synchronize tracker with context for specific language
        /// </summary>
        public void SyncWithContext(LanguageCode languageCode)
        {
            if (_languageTrackers.TryGetValue(languageCode, out var tracker))
            {
                var keys = _context.GetAllKeys();
                var data = new Dictionary<string, string>();
                foreach (var key in keys)
                {
                    data[key] = _context.GetText(key) ?? string.Empty;
                }

                tracker.SyncWithData(data);
            }
        }

        /// <summary>
        /// Simple key-value change tracker for localization text.
        /// Internal class - not exposed outside LocalizationChangeTracker.
        /// </summary>
        private class LanguageTextTracker
        {
            private readonly Dictionary<string, string> _baseline = new();
            private readonly Dictionary<string, string> _current = new();
            private readonly HashSet<string> _addedKeys = new();
            private readonly HashSet<string> _deletedKeys = new();

            public bool HasModifications =>
                _addedKeys.Count > 0 ||
                _deletedKeys.Count > 0 ||
                _current.Any(kvp => !_baseline.TryGetValue(kvp.Key, out var baseVal) || baseVal != kvp.Value);

            public void InitializeBaseline(IReadOnlyDictionary<string, string> data)
            {
                _baseline.Clear();
                _current.Clear();
                _addedKeys.Clear();
                _deletedKeys.Clear();

                foreach (var kvp in data)
                {
                    _baseline[kvp.Key] = kvp.Value;
                    _current[kvp.Key] = kvp.Value;
                }
            }

            public void TrackChange(string key, string newValue)
            {
                if (!_baseline.ContainsKey(key))
                {
                    TrackAdd(key, newValue);
                    return;
                }

                _current[key] = newValue;
            }

            public void TrackAdd(string key, string value)
            {
                _current[key] = value;

                if (!_baseline.ContainsKey(key))
                {
                    _addedKeys.Add(key);
                    _deletedKeys.Remove(key);
                }
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

                if (_baseline.TryGetValue(key, out var baseVal) && _current.TryGetValue(key, out var currVal))
                    return baseVal != currVal;

                return false;
            }

            public IEnumerable<string> GetModifiedKeys()
            {
                return _current
                    .Where(kvp => _baseline.TryGetValue(kvp.Key, out var baseVal) && baseVal != kvp.Value)
                    .Select(kvp => kvp.Key);
            }

            public IEnumerable<string> GetAddedKeys() => _addedKeys;
            public IEnumerable<string> GetDeletedKeys() => _deletedKeys;

            public string? GetBaselineValue(string key)
            {
                return _baseline.TryGetValue(key, out var value) ? value : null;
            }

            public Dictionary<string, string> GetBaselineData()
            {
                return new Dictionary<string, string>(_baseline);
            }

            public void SyncWithData(IReadOnlyDictionary<string, string> data)
            {
                foreach (var kvp in data)
                {
                    TrackChange(kvp.Key, kvp.Value);
                }

                // Detect deletions
                var dataKeys = new HashSet<string>(data.Keys);
                var currentKeys = _current.Keys.ToList();

                foreach (var key in currentKeys)
                {
                    if (!dataKeys.Contains(key))
                    {
                        TrackDelete(key);
                    }
                }
            }
        }
    }
}
