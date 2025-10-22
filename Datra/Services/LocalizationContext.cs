#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Configuration;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Models;
using Datra.Serializers;
using Datra.Repositories;

namespace Datra.Services
{
    /// <summary>
    /// Context for managing localization data
    /// </summary>
    public class LocalizationContext : ILocalizationContext
    {
        private class LocalizationEntry
        {
            public string Text { get; set; } = string.Empty;
            public string Context { get; set; } = string.Empty;
        }

        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory _serializerFactory;
        private readonly DatraConfigurationValue _config;
        private readonly ITranslationProvider _translationProvider;
        private readonly Dictionary<LanguageCode, Dictionary<string, LocalizationEntry>> _languageData;
        private KeyValueDataRepository<string, LocalizationKeyData>? _keyRepository;
        private LanguageCode _currentLanguageCode;
        private List<LanguageCode> _availableLanguages;

        // Events for editor
        /// <summary>
        /// Fired when localization text is changed. Internal for editor use only.
        /// </summary>
        internal event Action<string, LanguageCode>? OnTextChanged;

        /// <summary>
        /// Fired when a new localization key is added. Internal for editor use only.
        /// </summary>
        internal event Action<string>? OnKeyAdded;

        /// <summary>
        /// Fired when a localization key is deleted. Internal for editor use only.
        /// </summary>
        internal event Action<string>? OnKeyDeleted;
        
        /// <summary>
        /// Gets the current language as ISO code string (implements ILocalizationContext)
        /// </summary>
        public string CurrentLanguage => _currentLanguageCode.ToIsoCode();
        
        /// <summary>
        /// Gets the current language code enum
        /// </summary>
        public LanguageCode CurrentLanguageCode => _currentLanguageCode;
        
        public KeyValueDataRepository<string, LocalizationKeyData> KeyRepository => _keyRepository ?? throw new InvalidOperationException("KeyRepository is not set. Make sure to call SetKeyRepository from generated code.");

        /// <summary>
        /// Creates a new LocalizationContext
        /// </summary>
        /// <param name="rawDataProvider">Provider for loading/saving localization data</param>
        /// <param name="serializerFactory">Factory for creating serializers (optional, uses default if null)</param>
        /// <param name="config">Configuration values (optional, uses default if null)</param>
        /// <param name="translationProvider">Translation provider for auto-translate features (optional, uses DummyTranslationProvider if null)</param>
        public LocalizationContext(
            IRawDataProvider rawDataProvider,
            DataSerializerFactory? serializerFactory = null,
            DatraConfigurationValue? config = null,
            ITranslationProvider? translationProvider = null)
        {
            _rawDataProvider = rawDataProvider ?? throw new ArgumentNullException(nameof(rawDataProvider));
            _serializerFactory = serializerFactory ?? new DataSerializerFactory();
            _config = config ?? DatraConfigurationValue.CreateDefault();
            _translationProvider = translationProvider ?? new DummyTranslationProvider();
            _languageData = new Dictionary<LanguageCode, Dictionary<string, LocalizationEntry>>();
            _availableLanguages = new List<LanguageCode>();
            // Parse default language from config
            _currentLanguageCode = LanguageCodeExtensions.TryParse(_config.DefaultLanguage) ?? LanguageCode.En;
        }

        /// <summary>
        /// Initializes the localization context by loading master keys
        /// </summary>
        public async Task InitializeAsync()
        {
            // Load LocalizationKeys.csv
            await LoadMasterKeysAsync();

            // Detect available languages
            DetectAvailableLanguages();
        }

        /// <summary>
        /// Sets the key repository (called from generated code)
        /// </summary>
        public void SetKeyRepository(KeyValueDataRepository<string, LocalizationKeyData> keyRepository)
        {
            _keyRepository = keyRepository;
        }
        
        /// <summary>
        /// Gets all localization keys
        /// </summary>
        public IEnumerable<string> GetAllKeys()
        {
            if (_keyRepository == null)
                return Enumerable.Empty<string>();
            return _keyRepository.Keys;
        }
        
        /// <summary>
        /// Gets key information for a specific key
        /// </summary>
        public LocalizationKeyData? GetKeyData(string key)
        {
            if (_keyRepository == null || string.IsNullOrEmpty(key))
                return null;

            return _keyRepository.GetValueOrDefault(key);
        }

        /// <summary>
        /// Checks if a localization key is marked as fixed (non-editable key)
        /// </summary>
        /// <param name="key">The localization key to check</param>
        /// <returns>True if the key is fixed, false otherwise</returns>
        public bool IsFixedKey(string key)
        {
            var keyData = GetKeyData(key);
            return keyData != null && keyData.IsFixedKey;
        }
        
        private async Task LoadMasterKeysAsync()
        {
            if (_keyRepository != null)
                await _keyRepository.LoadAsync();
        }
        
        private void DetectAvailableLanguages()
        {
            // Detect available language files
            _availableLanguages.Clear();
            
            // Check for all defined language codes
            foreach (LanguageCode langCode in Enum.GetValues(typeof(LanguageCode)))
            {
                // Use LocalizationDataPath from config with ISO code
                var languageFilePath = System.IO.Path.Combine(_config.LocalizationDataPath, langCode.GetFileName());
                if (_rawDataProvider.Exists(languageFilePath))
                {
                    _availableLanguages.Add(langCode);
                }
            }
        }
        
        /// <summary>
        /// Loads localization data for the specified language
        /// </summary>
        public async Task LoadLanguageAsync(LanguageCode languageCode)
        {
            // No null check needed for enum
            
            // Check if already loaded
            if (_languageData.ContainsKey(languageCode))
            {
                _currentLanguageCode = languageCode;
                return;
            }
            
            // Load language data using config path with ISO code
            var dataPath = System.IO.Path.Combine(_config.LocalizationDataPath, languageCode.GetFileName());
            if (!_rawDataProvider.Exists(dataPath))
            {
                throw new InvalidOperationException($"Localization file for language '{languageCode.ToIsoCode()}' not found at {dataPath}");
            }
            
            // For now, use a simple CSV parsing approach
            var rawData = await _rawDataProvider.LoadTextAsync(dataPath);
            var languageDict = ParseCsvData(rawData);
            
            _languageData[languageCode] = languageDict;
            _currentLanguageCode = languageCode;
        }
        
        /// <summary>
        /// Loads localization data for the specified language (string overload for backward compatibility)
        /// </summary>
        public async Task LoadLanguageAsync(string languageCodeString)
        {
            var languageCode = LanguageCodeExtensions.TryParse(languageCodeString);
            if (!languageCode.HasValue)
            {
                throw new ArgumentException($"Invalid language code: {languageCodeString}");
            }
            
            await LoadLanguageAsync(languageCode.Value);
        }
        
        /// <summary>
        /// Gets localized text for the specified key in the current language
        /// </summary>
        public string GetText(string key)
        {
            return GetText(key, _currentLanguageCode);
        }

        /// <summary>
        /// Gets localized text for the specified key in a specific language
        /// </summary>
        public string GetText(string key, LanguageCode language)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (!_languageData.ContainsKey(language))
                return $"[{key}]";

            var languageDict = _languageData[language];
            return languageDict.TryGetValue(key, out var entry) ? entry.Text : $"[Missing: {key}]";
        }
        
        /// <summary>
        /// Sets localized text for the specified key in the current language.
        /// Note: This method allows updating locale values even for fixed keys.
        /// Fixed keys only prevent modification of the key itself, not its values.
        /// </summary>
        public void SetText(string key, string value)
        {
            SetText(key, value, _currentLanguageCode);
        }

        /// <summary>
        /// Sets localized text for the specified key in a specific language.
        /// Note: This method allows updating locale values even for fixed keys.
        /// Fixed keys only prevent modification of the key itself, not its values.
        /// </summary>
        public void SetText(string key, string value, LanguageCode language)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (!_languageData.ContainsKey(language))
            {
                _languageData[language] = new Dictionary<string, LocalizationEntry>();
            }

            // Preserve existing context if available
            var context = "";
            if (_languageData[language].TryGetValue(key, out var existingEntry))
            {
                context = existingEntry.Context;
            }

            _languageData[language][key] = new LocalizationEntry { Text = value, Context = context };

            // Fire event for editor
            System.Diagnostics.Debug.WriteLine($"[LocalizationContext.SetText] key={key}, language={language}, value={value}, subscribers={OnTextChanged?.GetInvocationList()?.Length ?? 0}");
            OnTextChanged?.Invoke(key, language);
        }
        
        /// <summary>
        /// Saves the current language data to file
        /// </summary>
        public async Task SaveCurrentLanguageAsync()
        {
            await SaveLanguageAsync(_currentLanguageCode);
        }

        /// <summary>
        /// Saves a specific language data to file
        /// </summary>
        public async Task SaveLanguageAsync(LanguageCode language)
        {
            if (!_languageData.ContainsKey(language))
                return;

            var dataPath = System.IO.Path.Combine(_config.LocalizationDataPath, language.GetFileName());
            var csvContent = BuildCsvContent(_languageData[language]);

            await _rawDataProvider.SaveTextAsync(dataPath, csvContent);
        }
        
        /// <summary>
        /// Builds CSV content from language dictionary
        /// </summary>
        private string BuildCsvContent(Dictionary<string, LocalizationEntry> languageDict)
        {
            var lines = new List<string>();
            lines.Add("Id,Text,Context");
            
            foreach (var kvp in languageDict.OrderBy(x => x.Key))
            {
                var id = kvp.Key;
                var entry = kvp.Value;
                
                // Escape CSV values if needed
                var text = entry.Text ?? "";
                var context = entry.Context ?? "";
                
                if (text.Contains(",") || text.Contains("\"") || text.Contains("\n"))
                {
                    text = "\"" + text.Replace("\"", "\"\"") + "\"";
                }
                
                if (context.Contains(",") || context.Contains("\"") || context.Contains("\n"))
                {
                    context = "\"" + context.Replace("\"", "\"\"") + "\"";
                }
                
                lines.Add($"{id},{text},{context}");
            }
            
            return string.Join("\n", lines);
        }
        
        /// <summary>
        /// Checks if a localization key exists
        /// </summary>
        public bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            
            if (!_languageData.ContainsKey(_currentLanguageCode))
                return false;
            
            return _languageData[_currentLanguageCode].ContainsKey(key);
        }
        
        /// <summary>
        /// Gets all available languages
        /// </summary>
        public IEnumerable<LanguageCode> GetAvailableLanguages()
        {
            return _availableLanguages;
        }

        /// <summary>
        /// Gets all available languages as ISO codes
        /// </summary>
        public IEnumerable<string> GetAvailableLanguageIsoCodes()
        {
            return _availableLanguages.Select(l => l.ToIsoCode());
        }

        /// <summary>
        /// Gets all currently loaded languages
        /// </summary>
        public IEnumerable<LanguageCode> GetLoadedLanguages()
        {
            return _languageData.Keys;
        }

        /// <summary>
        /// Loads all available languages into memory.
        /// Useful for editor scenarios where all languages need to be accessed without switching.
        /// </summary>
        public async Task LoadAllAvailableLanguagesAsync()
        {
            // Preserve the current language code
            var originalLanguageCode = _currentLanguageCode;

            foreach (var languageCode in _availableLanguages)
            {
                if (!_languageData.ContainsKey(languageCode))
                {
                    await LoadLanguageAsync(languageCode);
                }
            }

            // Restore the original language code after loading all languages
            _currentLanguageCode = originalLanguageCode;
        }
        
        /// <summary>
        /// Simple CSV parser for localization data
        /// Expected format: Id,Text,Context
        /// </summary>
        private Dictionary<string, LocalizationEntry> ParseCsvData(string csvContent)
        {
            var result = new Dictionary<string, LocalizationEntry>();
            var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length <= 1)
                return result; // No data rows
            
            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = ParseCsvLine(lines[i]);
                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    // parts[0] = Id, parts[1] = Text, parts[2] = Context (optional)
                    var entry = new LocalizationEntry
                    {
                        Text = parts[1],
                        Context = parts.Length > 2 ? parts[2] : ""
                    };
                    result[parts[0]] = entry;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Parse a single CSV line handling quoted values
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var inQuotes = false;
            var currentValue = new System.Text.StringBuilder();
            
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        currentValue.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(currentValue.ToString().Trim());
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }
            
            // Add last value
            values.Add(currentValue.ToString().Trim());

            return values.ToArray();
        }

        /// <summary>
        /// Deletes a localization key from all languages
        /// </summary>
        /// <param name="key">The key to delete</param>
        /// <exception cref="ArgumentException">If key is null or empty</exception>
        /// <exception cref="InvalidOperationException">If key is a fixed key</exception>
        public async Task DeleteKeyAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            // Check if fixed key
            var keyData = GetKeyData(key);
            if (keyData != null && keyData.IsFixedKey)
            {
                throw new InvalidOperationException(
                    $"Cannot delete fixed localization key: {key}");
            }

            // Remove from key repository
            if (_keyRepository != null)
            {
                _keyRepository.Remove(key);
                await _keyRepository.SaveAsync();
            }

            // Remove from all language data
            foreach (var langData in _languageData.Values)
            {
                langData.Remove(key);
            }

            // Save all language files that are currently loaded
            foreach (var languageCode in _languageData.Keys)
            {
                var previousLanguage = _currentLanguageCode;
                _currentLanguageCode = languageCode;
                await SaveCurrentLanguageAsync();
                _currentLanguageCode = previousLanguage;
            }

            // Fire event for editor
            OnKeyDeleted?.Invoke(key);
        }

        /// <summary>
        /// Adds a new localization key
        /// </summary>
        /// <param name="key">The key to add</param>
        /// <param name="description">Description of the key</param>
        /// <param name="category">Category for grouping</param>
        /// <param name="isFixedKey">Whether this is a fixed key</param>
        /// <exception cref="ArgumentException">If key is null or empty</exception>
        /// <exception cref="InvalidOperationException">If key already exists</exception>
        public async Task AddKeyAsync(string key, string description = "", string category = "", bool isFixedKey = false)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            // Check if key already exists
            var existingKey = GetKeyData(key);
            if (existingKey != null)
            {
                throw new InvalidOperationException($"Key '{key}' already exists");
            }

            // Add to key repository
            if (_keyRepository != null)
            {
                var newKeyData = new LocalizationKeyData
                {
                    Id = key,
                    Description = description,
                    Category = category,
                    IsFixedKey = isFixedKey
                };

                _keyRepository.Add(newKeyData);
                await _keyRepository.SaveAsync();
            }

            // Add empty entries to all loaded languages
            foreach (var languageCode in _languageData.Keys)
            {
                if (!_languageData[languageCode].ContainsKey(key))
                {
                    _languageData[languageCode][key] = new LocalizationEntry
                    {
                        Text = "",
                        Context = ""
                    };
                }
            }

            // Save current language
            if (_languageData.ContainsKey(_currentLanguageCode))
            {
                await SaveCurrentLanguageAsync();
            }

            // Fire event for editor
            OnKeyAdded?.Invoke(key);
        }

        /// <summary>
        /// Subscribe to editor events (for Unity Editor assembly use only)
        /// </summary>
        public void SubscribeToEditorEvents(
            Action<string, LanguageCode> onTextChanged,
            Action<string> onKeyAdded,
            Action<string> onKeyDeleted)
        {
            if (onTextChanged != null)
                OnTextChanged += onTextChanged;
            if (onKeyAdded != null)
                OnKeyAdded += onKeyAdded;
            if (onKeyDeleted != null)
                OnKeyDeleted += onKeyDeleted;
        }

        /// <summary>
        /// Translates text from source language to target language using the configured translation provider
        /// </summary>
        /// <param name="text">The text to translate</param>
        /// <param name="sourceLanguage">The source language code</param>
        /// <param name="targetLanguage">The target language code</param>
        /// <returns>The translated text</returns>
        public async Task<string> TranslateTextAsync(string text, LanguageCode sourceLanguage, LanguageCode targetLanguage)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (!_translationProvider.SupportsLanguagePair(sourceLanguage, targetLanguage))
            {
                throw new InvalidOperationException(
                    $"Translation provider does not support translation from {sourceLanguage.ToIsoCode()} to {targetLanguage.ToIsoCode()}");
            }

            return await _translationProvider.TranslateAsync(text, sourceLanguage, targetLanguage);
        }

        /// <summary>
        /// Auto-translates a key from a source language to the current language
        /// </summary>
        /// <param name="key">The localization key to translate</param>
        /// <param name="sourceLanguage">The source language to translate from</param>
        /// <returns>True if translation was successful and applied, false otherwise</returns>
        public async Task<bool> AutoTranslateKeyAsync(string key, LanguageCode sourceLanguage)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            // Get source text
            string? sourceText = null;
            if (_languageData.TryGetValue(sourceLanguage, out var sourceDict))
            {
                if (sourceDict.TryGetValue(key, out var entry))
                {
                    sourceText = entry.Text;
                }
            }

            if (string.IsNullOrEmpty(sourceText))
                return false;

            // Translate
            var translatedText = await TranslateTextAsync(sourceText, sourceLanguage, _currentLanguageCode);

            // Apply translation to current language
            SetText(key, translatedText);

            return true;
        }
    }
}
