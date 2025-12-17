using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Localization;
using Datra.Services;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Services
{
    /// <summary>
    /// Service implementation for localization editing.
    /// Wraps LocalizationContext with editor-specific functionality and change tracking.
    /// </summary>
    public class LocalizationEditorService : ILocalizationEditorService
    {
        private readonly LocalizationContext _context;
        private readonly LocalizationChangeTracker _changeTracker;

        public LocalizationContext Context => _context;
        public bool IsAvailable => _context != null;
        public LanguageCode CurrentLanguage => _context?.CurrentLanguageCode ?? default;

        public IReadOnlyList<LanguageCode> AvailableLanguages =>
            _context?.GetAvailableLanguages()?.ToList() ?? new List<LanguageCode>();

        public IReadOnlyList<LanguageCode> LoadedLanguages =>
            _context?.GetLoadedLanguages()?.ToList() ?? new List<LanguageCode>();

        public event Action<string, LanguageCode> OnTranslationChanged;
        public event Action<LanguageCode> OnLanguageChanged;
        public event Action<bool> OnModifiedStateChanged;

        public LocalizationEditorService(
            LocalizationContext context,
            LocalizationChangeTracker changeTracker = null)
        {
            _context = context;
            _changeTracker = changeTracker ?? (context != null ? new LocalizationChangeTracker(context) : null);

            if (_context != null)
            {
                SubscribeToContextEvents();
            }

            if (_changeTracker is INotifyModifiedStateChanged notifyTracker)
            {
                notifyTracker.OnModifiedStateChanged += (hasChanges) =>
                {
                    OnModifiedStateChanged?.Invoke(hasChanges);
                };
            }
        }

        private void SubscribeToContextEvents()
        {
            _context.SubscribeToEditorEvents(
                onTextChanged: (key, language) =>
                {
                    if (_changeTracker?.IsLanguageInitialized(language) == true)
                    {
                        var newText = _context.GetText(key, language);
                        _changeTracker.TrackTextChange(key, newText, language);
                    }
                    OnTranslationChanged?.Invoke(key, language);
                },
                onKeyAdded: (key) =>
                {
                    _changeTracker?.TrackKeyAdd(key);
                    OnTranslationChanged?.Invoke(key, CurrentLanguage);
                },
                onKeyDeleted: (key) =>
                {
                    _changeTracker?.TrackKeyDelete(key);
                    OnTranslationChanged?.Invoke(key, CurrentLanguage);
                }
            );
        }

        public async Task SwitchLanguageAsync(LanguageCode language)
        {
            if (_context == null) return;

            await _context.LoadLanguageAsync(language);
            OnLanguageChanged?.Invoke(language);
        }

        public async Task LoadAllLanguagesAsync()
        {
            if (_context == null) return;

            await _context.LoadAllAvailableLanguagesAsync();

            // Initialize change trackers for all loaded languages
            foreach (var language in LoadedLanguages)
            {
                InitializeBaseline(language);
            }
        }

        public string GetTranslation(string key)
        {
            return _context?.GetText(key) ?? string.Empty;
        }

        public string GetTranslation(string key, LanguageCode language)
        {
            return _context?.GetText(key, language) ?? string.Empty;
        }

        public void SetTranslation(string key, string value, LanguageCode language)
        {
            _context?.SetText(key, value, language);
        }

        public bool HasUnsavedChanges()
        {
            return _changeTracker?.HasModifications() ?? false;
        }

        public bool HasUnsavedChanges(LanguageCode language)
        {
            return _changeTracker?.HasModifications(language) ?? false;
        }

        public async Task<bool> SaveAsync(bool forceSave = false)
        {
            if (_context == null) return false;

            if (!forceSave && !HasUnsavedChanges())
            {
                return true;
            }

            try
            {
                // Save all loaded languages that have changes
                foreach (var language in LoadedLanguages)
                {
                    if (forceSave || HasUnsavedChanges(language))
                    {
                        await _context.SaveLanguageAsync(language);
                        _changeTracker?.UpdateBaseline(language);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SaveAsync(LanguageCode language, bool forceSave = false)
        {
            if (_context == null) return false;

            if (!forceSave && !HasUnsavedChanges(language))
            {
                return true;
            }

            try
            {
                await _context.SaveLanguageAsync(language);
                _changeTracker?.UpdateBaseline(language);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void InitializeBaseline(LanguageCode language)
        {
            if (_changeTracker != null && !_changeTracker.IsLanguageInitialized(language))
            {
                _changeTracker.InitializeLanguage(language);
            }
        }

        /// <summary>
        /// Get the underlying change tracker (for advanced usage)
        /// </summary>
        public LocalizationChangeTracker GetChangeTracker() => _changeTracker;
    }
}
