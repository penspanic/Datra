#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;
using Datra.Localization;
using Datra.Services;

namespace Datra.Editor.Services
{
    /// <summary>
    /// Default implementation of ILocaleEditorService.
    /// Wraps LocalizationContext with change tracking functionality.
    /// </summary>
    public class LocaleEditorService : ILocaleEditorService
    {
        private readonly LocalizationContext _context;
        private readonly Dictionary<LanguageCode, string> _baselineHashes = new();
        private bool _isAvailable;

        public LocalizationContext Context => _context;
        public bool IsAvailable => _isAvailable;
        public IReadOnlyList<LanguageCode> AvailableLanguages => _context.GetAvailableLanguages().ToList();
        public IReadOnlyList<LanguageCode> LoadedLanguages => _context.GetLoadedLanguages().ToList();
        public LanguageCode CurrentLanguage => _context.CurrentLanguageCode;

        public event Action<string, LanguageCode>? OnTextChanged;
        public event Action<LanguageCode>? OnLanguageChanged;
        public event Action<bool>? OnModifiedStateChanged;

        private bool _previousModifiedState = false;

        public LocaleEditorService(LocalizationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _isAvailable = true;

            // Subscribe to context events
            _context.SubscribeToEditorEvents(
                onTextChanged: (key, lang) =>
                {
                    OnTextChanged?.Invoke(key, lang);
                    CheckModifiedStateChanged();
                },
                onKeyAdded: _ => CheckModifiedStateChanged(),
                onKeyDeleted: _ => CheckModifiedStateChanged()
            );
        }

        public async Task SwitchLanguageAsync(LanguageCode language)
        {
            await _context.LoadLanguageAsync(language);
            OnLanguageChanged?.Invoke(language);
        }

        public async Task LoadAllLanguagesAsync()
        {
            await _context.LoadAllAvailableLanguagesAsync();
        }

        public string GetText(string key)
        {
            return _context.GetText(key);
        }

        public string GetText(string key, LanguageCode language)
        {
            return _context.GetText(key, language);
        }

        public void SetText(string key, string value, LanguageCode language)
        {
            _context.SetText(key, value, language);
        }

        public bool HasUnsavedChanges()
        {
            foreach (var language in LoadedLanguages)
            {
                if (HasUnsavedChanges(language))
                    return true;
            }
            return false;
        }

        public bool HasUnsavedChanges(LanguageCode language)
        {
            if (!_baselineHashes.TryGetValue(language, out var baselineHash))
                return false;

            var currentHash = ComputeLanguageHash(language);
            return currentHash != baselineHash;
        }

        public async Task<bool> SaveAsync(bool forceSave = false)
        {
            if (!forceSave && !HasUnsavedChanges())
                return true;

            try
            {
                foreach (var language in LoadedLanguages)
                {
                    if (forceSave || HasUnsavedChanges(language))
                    {
                        await _context.SaveLanguageAsync(language);
                        InitializeBaseline(language);
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SaveAsync(LanguageCode language, bool forceSave = false)
        {
            if (!forceSave && !HasUnsavedChanges(language))
                return true;

            try
            {
                await _context.SaveLanguageAsync(language);
                InitializeBaseline(language);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void InitializeBaseline(LanguageCode language)
        {
            var hash = ComputeLanguageHash(language);
            _baselineHashes[language] = hash;
            CheckModifiedStateChanged();
        }

        public void InitializeAllBaselines()
        {
            foreach (var language in LoadedLanguages)
            {
                InitializeBaseline(language);
            }
        }

        private string ComputeLanguageHash(LanguageCode language)
        {
            // Build a string representation of all locale entries for this language
            var entries = new List<string>();

            foreach (var key in _context.GetAllKeys().OrderBy(k => k))
            {
                var text = _context.GetText(key, language);
                entries.Add($"{key}={text}");
            }

            var combined = string.Join("\n", entries);

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(combined);
            var hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }

        private void CheckModifiedStateChanged()
        {
            var currentState = HasUnsavedChanges();
            if (currentState != _previousModifiedState)
            {
                _previousModifiedState = currentState;
                OnModifiedStateChanged?.Invoke(currentState);
            }
        }
    }
}
