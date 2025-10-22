using System;
using System.Collections.Generic;
using System.Linq;
using Datra.Localization;
using Datra.Services;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Editor-only change tracker for LocalizationContext.
    /// Tracks changes per language without modifying runtime code.
    /// </summary>
    public class LocalizationChangeTracker : IRepositoryChangeTracker
    {
        private readonly LocalizationContext _context;
        private readonly Dictionary<LanguageCode, RepositoryChangeTracker<string, string>> _languageTrackers;

        public LocalizationChangeTracker(LocalizationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _languageTrackers = new Dictionary<LanguageCode, RepositoryChangeTracker<string, string>>();
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
        /// IMPORTANT: Only call this when loading a language for the first time.
        /// This will clear any existing tracking data for the language.
        /// </summary>
        public void InitializeLanguage(LanguageCode languageCode)
        {
            var tracker = new RepositoryChangeTracker<string, string>();

            // Build dictionary from context
            var keys = _context.GetAllKeys();
            var data = new Dictionary<string, string>();
            foreach (var key in keys)
            {
                var text = _context.GetText(key);
                // Store actual text, even if it's a [Missing: key] message
                data[key] = text ?? string.Empty;
            }

            tracker.InitializeBaseline(data);
            _languageTrackers[languageCode] = tracker;
        }

        /// <summary>
        /// Track text change (call after SetText)
        /// </summary>
        public void TrackTextChange(string key, string newValue)
        {
            var languageCode = _context.CurrentLanguageCode;
            if (_languageTrackers.TryGetValue(languageCode, out var tracker))
            {
                tracker.TrackChange(key, newValue ?? string.Empty);
            }
        }

        /// <summary>
        /// Track key addition (call after AddKeyAsync)
        /// </summary>
        public void TrackKeyAdd(string key)
        {
            // Track in all loaded languages
            foreach (var kvp in _languageTrackers)
            {
                var languageCode = kvp.Key;
                var tracker = kvp.Value;

                // Get text for this language
                var currentLang = _context.CurrentLanguageCode;
                // TODO: Would need to switch languages to get proper text
                // For now, just track as empty
                var text = string.Empty;

                tracker.TrackAdd(key, text);
            }
        }

        /// <summary>
        /// Track key deletion (call after removing a key)
        /// </summary>
        public void TrackKeyDelete(string key)
        {
            // Track in all loaded languages
            foreach (var tracker in _languageTrackers.Values)
            {
                tracker.TrackDelete(key);
            }
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
        /// Get baseline text for a key in current language
        /// </summary>
        public string GetBaselineText(string key)
        {
            return GetBaselineText(_context.CurrentLanguageCode, key);
        }

        /// <summary>
        /// Get baseline text for a key in specific language
        /// </summary>
        public string GetBaselineText(LanguageCode languageCode, string key)
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
                    // Re-track to update tracker state
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
        /// Synchronize tracker with context (useful for detecting external changes)
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

                tracker.SyncWithRepository(data);
            }
        }

        #region IRepositoryChangeTracker Implementation

        /// <summary>
        /// Get whether current language has modifications (IRepositoryChangeTracker)
        /// </summary>
        bool IRepositoryChangeTracker.HasModifications => HasModifications();

        /// <summary>
        /// Get baseline value for a key (IRepositoryChangeTracker)
        /// </summary>
        object IRepositoryChangeTracker.GetBaselineValue(object key)
        {
            if (key is string keyStr)
                return GetBaselineText(keyStr);
            return null;
        }

        /// <summary>
        /// Track a change (IRepositoryChangeTracker)
        /// </summary>
        void IRepositoryChangeTracker.TrackChange(object key, object value)
        {
            if (key is string keyStr && value is string valueStr)
                TrackTextChange(keyStr, valueStr);
        }

        /// <summary>
        /// Track property change - not applicable for localization (IRepositoryChangeTracker)
        /// </summary>
        void IRepositoryChangeTracker.TrackPropertyChange(object key, string propertyName, object newValue, out bool isModified)
        {
            // Localization doesn't have properties, just track as text change
            if (key is string keyStr && newValue is string valueStr)
            {
                TrackTextChange(keyStr, valueStr);
                isModified = IsModified(keyStr);
            }
            else
            {
                isModified = false;
            }
        }

        /// <summary>
        /// Track addition (IRepositoryChangeTracker)
        /// </summary>
        void IRepositoryChangeTracker.TrackAdd(object key, object value)
        {
            if (key is string keyStr)
                TrackKeyAdd(keyStr);
        }

        /// <summary>
        /// Track deletion (IRepositoryChangeTracker)
        /// </summary>
        void IRepositoryChangeTracker.TrackDelete(object key)
        {
            if (key is string keyStr)
                TrackKeyDelete(keyStr);
        }

        /// <summary>
        /// Check if key is modified (IRepositoryChangeTracker)
        /// </summary>
        bool IRepositoryChangeTracker.IsModified(object key)
        {
            if (key is string keyStr)
                return IsModified(keyStr);
            return false;
        }

        /// <summary>
        /// Check if property is modified - not applicable (IRepositoryChangeTracker)
        /// </summary>
        bool IRepositoryChangeTracker.IsPropertyModified(object key, string propertyName)
        {
            // Localization doesn't have properties
            return false;
        }

        /// <summary>
        /// Check if key is added (IRepositoryChangeTracker)
        /// </summary>
        bool IRepositoryChangeTracker.IsAdded(object key)
        {
            if (key is string keyStr)
                return GetAddedKeys().Contains(keyStr);
            return false;
        }

        /// <summary>
        /// Check if key is deleted (IRepositoryChangeTracker)
        /// </summary>
        bool IRepositoryChangeTracker.IsDeleted(object key)
        {
            if (key is string keyStr)
                return GetDeletedKeys().Contains(keyStr);
            return false;
        }

        /// <summary>
        /// Get modified properties - not applicable (IRepositoryChangeTracker)
        /// </summary>
        IEnumerable<string> IRepositoryChangeTracker.GetModifiedProperties(object key)
        {
            // Localization doesn't have properties, return empty
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Get property baseline value - not applicable (IRepositoryChangeTracker)
        /// </summary>
        object IRepositoryChangeTracker.GetPropertyBaselineValue(object key, string propertyName)
        {
            // Localization doesn't have properties
            return null;
        }

        /// <summary>
        /// Initialize baseline (IRepositoryChangeTracker)
        /// </summary>
        void IRepositoryChangeTracker.InitializeBaseline(object repositoryData)
        {
            // Not used for localization - use InitializeLanguage instead
        }

        /// <summary>
        /// Update baseline (IRepositoryChangeTracker)
        /// </summary>
        void IRepositoryChangeTracker.UpdateBaseline(object repositoryData)
        {
            // Update baseline for current language
            UpdateBaseline();
        }

        /// <summary>
        /// Revert all changes (IRepositoryChangeTracker)
        /// </summary>
        void IRepositoryChangeTracker.RevertAll()
        {
            RevertAll();
        }

        #endregion
    }
}
