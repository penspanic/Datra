#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Localization;
using Datra.Services;

namespace Datra.Editor.Interfaces
{
    /// <summary>
    /// Service for localization editing operations.
    /// Wraps LocalizationContext with editor-specific functionality like change tracking.
    /// </summary>
    public interface ILocaleEditorService
    {
        /// <summary>
        /// The underlying localization context
        /// </summary>
        LocalizationContext Context { get; }

        /// <summary>
        /// Whether localization is available in the current project
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// List of available languages
        /// </summary>
        IReadOnlyList<LanguageCode> AvailableLanguages { get; }

        /// <summary>
        /// Languages that have been loaded into memory
        /// </summary>
        IReadOnlyList<LanguageCode> LoadedLanguages { get; }

        /// <summary>
        /// Currently active language
        /// </summary>
        LanguageCode CurrentLanguage { get; }

        /// <summary>
        /// Switch to a different language
        /// </summary>
        Task SwitchLanguageAsync(LanguageCode language);

        /// <summary>
        /// Load all available languages for editing
        /// </summary>
        Task LoadAllLanguagesAsync();

        /// <summary>
        /// Get translation for a key in the current language
        /// </summary>
        string GetText(string key);

        /// <summary>
        /// Get translation for a key in a specific language
        /// </summary>
        string GetText(string key, LanguageCode language);

        /// <summary>
        /// Set translation for a key in a specific language
        /// </summary>
        void SetText(string key, string value, LanguageCode language);

        /// <summary>
        /// Check if there are unsaved localization changes
        /// </summary>
        bool HasUnsavedChanges();

        /// <summary>
        /// Check if a specific language has unsaved changes
        /// </summary>
        bool HasUnsavedChanges(LanguageCode language);

        /// <summary>
        /// Save all localization changes
        /// </summary>
        /// <param name="forceSave">If true, save even if no changes detected</param>
        Task<bool> SaveAsync(bool forceSave = false);

        /// <summary>
        /// Save changes for a specific language
        /// </summary>
        Task<bool> SaveAsync(LanguageCode language, bool forceSave = false);

        /// <summary>
        /// Initialize change tracking baseline for a language
        /// </summary>
        void InitializeBaseline(LanguageCode language);

        /// <summary>
        /// Initialize change tracking baseline for all loaded languages
        /// </summary>
        void InitializeAllBaselines();

        /// <summary>
        /// Raised when a translation changes
        /// </summary>
        event Action<string, LanguageCode>? OnTextChanged;

        /// <summary>
        /// Raised when the current language changes
        /// </summary>
        event Action<LanguageCode>? OnLanguageChanged;

        /// <summary>
        /// Raised when modified state changes
        /// </summary>
        event Action<bool>? OnModifiedStateChanged;
    }
}
