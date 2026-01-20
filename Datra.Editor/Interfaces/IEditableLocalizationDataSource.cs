#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Localization;

namespace Datra.Editor.Interfaces
{
    /// <summary>
    /// Interface for editable localization data sources.
    /// Follows the same patterns as IEditableDataSource but specialized for localization.
    /// </summary>
    public interface IEditableLocalizationDataSource : IEditableDataSource
    {
        /// <summary>
        /// Currently active language for editing
        /// </summary>
        LanguageCode CurrentLanguage { get; }

        /// <summary>
        /// All languages that have been loaded and can be edited
        /// </summary>
        IReadOnlyList<LanguageCode> LoadedLanguages { get; }

        /// <summary>
        /// All available languages (may not be loaded yet)
        /// </summary>
        IReadOnlyList<LanguageCode> AvailableLanguages { get; }

        /// <summary>
        /// Check if baseline is initialized for a language.
        /// </summary>
        bool IsLanguageInitialized(LanguageCode language);

        /// <summary>
        /// Get all localization keys
        /// </summary>
        IEnumerable<string> GetAllKeys();

        /// <summary>
        /// Get text for a key in the current language
        /// </summary>
        string GetText(string key);

        /// <summary>
        /// Get text for a key in a specific language
        /// </summary>
        string GetText(string key, LanguageCode language);

        /// <summary>
        /// Set text for a key in the current language.
        /// Automatically tracks the change.
        /// </summary>
        void SetText(string key, string value);

        /// <summary>
        /// Set text for a key in a specific language.
        /// Automatically tracks the change.
        /// </summary>
        void SetText(string key, string value, LanguageCode language);

        /// <summary>
        /// Add a new localization key.
        /// Automatically tracks the change.
        /// </summary>
        void AddKey(string key, string description = "", string category = "");

        /// <summary>
        /// Delete a localization key.
        /// Automatically tracks the change.
        /// </summary>
        void DeleteKey(string key);

        /// <summary>
        /// Check if a key exists
        /// </summary>
        bool ContainsKey(string key);

        /// <summary>
        /// Check if modifications exist for a specific language
        /// </summary>
        bool HasLanguageModifications(LanguageCode language);

        /// <summary>
        /// Check if a specific key is modified in the current language
        /// </summary>
        bool IsKeyModified(string key);

        /// <summary>
        /// Check if a specific key is modified in a specific language
        /// </summary>
        bool IsKeyModified(string key, LanguageCode language);

        /// <summary>
        /// Get the baseline (original) text for a key in the current language
        /// </summary>
        string? GetBaselineText(string key);

        /// <summary>
        /// Get the baseline (original) text for a key in a specific language
        /// </summary>
        string? GetBaselineText(string key, LanguageCode language);

        /// <summary>
        /// Get all modified keys in the current language
        /// </summary>
        IEnumerable<string> GetModifiedKeys();

        /// <summary>
        /// Get all modified keys in a specific language
        /// </summary>
        IEnumerable<string> GetModifiedKeys(LanguageCode language);

        /// <summary>
        /// Get all added keys
        /// </summary>
        IEnumerable<string> GetAddedKeys();

        /// <summary>
        /// Get all deleted keys
        /// </summary>
        IEnumerable<string> GetDeletedKeys();

        /// <summary>
        /// Get all languages that have modifications
        /// </summary>
        IEnumerable<LanguageCode> GetModifiedLanguages();

        /// <summary>
        /// Switch to a different language for editing.
        /// May trigger language loading if not already loaded.
        /// </summary>
        Task SwitchLanguageAsync(LanguageCode language);

        /// <summary>
        /// Initialize baseline for a specific language.
        /// Call after loading a language.
        /// </summary>
        void InitializeBaseline(LanguageCode language);

        /// <summary>
        /// Revert changes for a specific key in current language
        /// </summary>
        void RevertKey(string key);

        /// <summary>
        /// Revert all changes in current language only
        /// </summary>
        void RevertCurrentLanguage();

        /// <summary>
        /// Save only the current language
        /// </summary>
        Task SaveCurrentLanguageAsync();

        /// <summary>
        /// Raised when text changes for any key
        /// </summary>
        event Action<string, LanguageCode>? OnTextChanged;

        /// <summary>
        /// Raised when the current language changes
        /// </summary>
        event Action<LanguageCode>? OnLanguageChanged;
    }
}
