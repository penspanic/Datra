#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Configuration;
using Datra.Helpers;
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
            if (_config.UseSingleFileLocalization)
            {
                // Single-file mode: load all languages from one file
                await LoadSingleFileLocalizationAsync();
            }
            else
            {
                // Multi-file mode: load master keys and detect available languages
                await LoadMasterKeysAsync();
                DetectAvailableLanguages();
            }
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
            // In single-file mode without KeyRepository, get keys from language data
            if (_keyRepository == null)
            {
                var keys = new HashSet<string>();
                foreach (var langData in _languageData.Values)
                {
                    foreach (var key in langData.Keys)
                    {
                        keys.Add(key);
                    }
                }
                return keys;
            }
            return _keyRepository.Keys;
        }
        
        /// <summary>
        /// Gets key information for a specific key
        /// </summary>
        public LocalizationKeyData? GetKeyData(string key)
        {
            if (_keyRepository == null || string.IsNullOrEmpty(key))
                return null;

            return _keyRepository.TryGetLoaded(key);
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
                await _keyRepository.InitializeAsync();
        }

        /// <summary>
        /// Loads localization data from a single horizontal CSV file
        /// </summary>
        private async Task LoadSingleFileLocalizationAsync()
        {
            var filePath = _config.SingleLocalizationFilePath;
            if (!_rawDataProvider.Exists(filePath))
            {
                throw new InvalidOperationException($"Single localization file not found at: {filePath}");
            }

            var rawData = await _rawDataProvider.LoadTextAsync(filePath);
            ParseHorizontalCsvData(rawData);
        }

        /// <summary>
        /// Parses horizontal CSV format where each row is a key and columns are languages
        /// Format: Key,~Description,ko,en,ja,zh-TW
        /// Columns starting with ~ are metadata and skipped
        /// </summary>
        private void ParseHorizontalCsvData(string csvContent)
        {
            var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return;

            // Parse header to find key column and language columns
            var headers = CsvParsingHelper.ParseCsvLine(lines[0]);
            var keyColumnIndex = -1;
            var languageColumnIndices = new Dictionary<LanguageCode, int>();

            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i].Trim();

                // Check if this is the key column
                if (header.Equals(_config.LocalizationKeyColumn, StringComparison.OrdinalIgnoreCase))
                {
                    keyColumnIndex = i;
                    continue;
                }

                // Skip metadata columns (starting with ~)
                if (header.StartsWith("~"))
                    continue;

                // Try to parse as language code
                var langCode = LanguageCodeExtensions.TryParse(header);
                if (langCode.HasValue)
                {
                    languageColumnIndices[langCode.Value] = i;
                    _availableLanguages.Add(langCode.Value);

                    // Initialize language dictionary
                    if (!_languageData.ContainsKey(langCode.Value))
                    {
                        _languageData[langCode.Value] = new Dictionary<string, LocalizationEntry>();
                    }
                }
            }

            if (keyColumnIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Key column '{_config.LocalizationKeyColumn}' not found in localization file header");
            }

            // Determine start row (skip type declaration row if present)
            int startRow = 1;
            if (lines.Length > 1)
            {
                var secondRowValues = CsvParsingHelper.ParseCsvLine(lines[1]);
                // Check if second row looks like a type declaration (common pattern: int, string, etc.)
                if (secondRowValues.Length > 0 && IsTypeDeclarationRow(secondRowValues))
                {
                    startRow = 2;
                }
            }

            // Parse data rows
            for (int i = startRow; i < lines.Length; i++)
            {
                var values = CsvParsingHelper.ParseCsvLine(lines[i]);
                if (values.Length <= keyColumnIndex)
                    continue;

                var key = values[keyColumnIndex].Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                // Add text for each language
                foreach (var kvp in languageColumnIndices)
                {
                    var langCode = kvp.Key;
                    var colIndex = kvp.Value;

                    var text = colIndex < values.Length ? values[colIndex] : "";
                    _languageData[langCode][key] = new LocalizationEntry
                    {
                        Text = text,
                        Context = ""
                    };
                }
            }
        }

        /// <summary>
        /// Checks if a row looks like a type declaration (e.g., "int", "string", etc.)
        /// </summary>
        private bool IsTypeDeclarationRow(string[] values)
        {
            var typeKeywords = new[] { "int", "string", "float", "bool", "double", "long" };
            int typeCount = 0;

            foreach (var value in values)
            {
                var trimmed = value.Trim().ToLowerInvariant();
                if (typeKeywords.Contains(trimmed) || trimmed.StartsWith("~"))
                {
                    typeCount++;
                }
            }

            // If more than half the columns look like types, it's probably a type row
            return typeCount > values.Length / 2;
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
            // In single-file mode, all languages are already loaded during initialization
            if (_config.UseSingleFileLocalization)
            {
                if (_languageData.ContainsKey(languageCode))
                {
                    _currentLanguageCode = languageCode;
                    return;
                }
                throw new InvalidOperationException(
                    $"Language '{languageCode.ToIsoCode()}' not found in single localization file");
            }

            // Multi-file mode: load from separate file
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
            if (_config.UseSingleFileLocalization)
            {
                await SaveSingleFileLocalizationAsync();
            }
            else
            {
                await SaveLanguageAsync(_currentLanguageCode);
            }
        }

        /// <summary>
        /// Saves a specific language data to file
        /// </summary>
        public async Task SaveLanguageAsync(LanguageCode language)
        {
            if (_config.UseSingleFileLocalization)
            {
                // In single-file mode, save all languages together
                await SaveSingleFileLocalizationAsync();
                return;
            }

            if (!_languageData.ContainsKey(language))
                return;

            var dataPath = System.IO.Path.Combine(_config.LocalizationDataPath, language.GetFileName());
            var csvContent = BuildCsvContent(_languageData[language]);

            await _rawDataProvider.SaveTextAsync(dataPath, csvContent);
        }

        /// <summary>
        /// Saves all localization data to a single horizontal CSV file
        /// </summary>
        private async Task SaveSingleFileLocalizationAsync()
        {
            var csvContent = BuildHorizontalCsvContent();
            await _rawDataProvider.SaveTextAsync(_config.SingleLocalizationFilePath, csvContent);
        }

        /// <summary>
        /// Builds horizontal CSV content with all languages
        /// </summary>
        private string BuildHorizontalCsvContent()
        {
            var lines = new List<string>();

            // Build header: Key,~Description,lang1,lang2,...
            var languageCodes = _availableLanguages.OrderBy(l => l.ToIsoCode()).ToList();
            var headerParts = new List<string> { _config.LocalizationKeyColumn };
            foreach (var lang in languageCodes)
            {
                headerParts.Add(lang.ToIsoCode());
            }
            lines.Add(string.Join(",", headerParts));

            // Collect all unique keys across all languages
            var allKeys = new HashSet<string>();
            foreach (var langData in _languageData.Values)
            {
                foreach (var key in langData.Keys)
                {
                    allKeys.Add(key);
                }
            }

            // Build data rows
            foreach (var key in allKeys.OrderBy(k => k))
            {
                var rowParts = new List<string> { CsvParsingHelper.EscapeCsvField(key) };

                foreach (var lang in languageCodes)
                {
                    var text = "";
                    if (_languageData.TryGetValue(lang, out var langDict) &&
                        langDict.TryGetValue(key, out var entry))
                    {
                        text = entry.Text ?? "";
                    }
                    rowParts.Add(CsvParsingHelper.EscapeCsvField(text));
                }

                lines.Add(string.Join(",", rowParts));
            }

            return string.Join("\n", lines);
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
                var parts = CsvParsingHelper.ParseCsvLine(lines[i]);
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

            await DeleteKeyInternalAsync(key);
        }

        /// <summary>
        /// Force-deletes a localization key from all languages, including fixed keys.
        /// This is used for syncing FixedLocale keys when data items are removed.
        /// </summary>
        /// <param name="key">The key to delete</param>
        /// <exception cref="ArgumentException">If key is null or empty</exception>
        public async Task ForceDeleteKeyAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            await DeleteKeyInternalAsync(key);
        }

        private async Task DeleteKeyInternalAsync(string key)
        {
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
        /// Adds multiple localization keys in a batch (saves once at the end).
        /// Much faster than calling AddKeyAsync multiple times.
        /// </summary>
        /// <param name="keys">List of keys to add with their metadata</param>
        /// <returns>Number of keys successfully added</returns>
        public async Task<int> AddKeysBatchAsync(IEnumerable<(string key, string description, string category, bool isFixedKey)> keys)
        {
            int addedCount = 0;

            foreach (var (key, description, category, isFixedKey) in keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                // Skip if key already exists
                if (GetKeyData(key) != null)
                    continue;

                // Add to key repository (no save yet)
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

                addedCount++;
            }

            // Save once at the end
            if (addedCount > 0)
            {
                if (_keyRepository != null)
                    await _keyRepository.SaveAsync();

                await SaveAllLoadedLanguagesAsync();

                // Fire events
                foreach (var (key, _, _, _) in keys)
                {
                    if (!string.IsNullOrEmpty(key))
                        OnKeyAdded?.Invoke(key);
                }
            }

            return addedCount;
        }

        /// <summary>
        /// Force-deletes multiple localization keys in a batch (saves once at the end).
        /// Much faster than calling ForceDeleteKeyAsync multiple times.
        /// </summary>
        /// <param name="keys">List of keys to delete</param>
        /// <returns>Number of keys successfully deleted</returns>
        public async Task<int> ForceDeleteKeysBatchAsync(IEnumerable<string> keys)
        {
            int deletedCount = 0;
            var deletedKeys = new List<string>();

            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                // Remove from key repository (no save yet)
                if (_keyRepository != null)
                {
                    _keyRepository.Remove(key);
                }

                // Remove from all language data
                foreach (var langData in _languageData.Values)
                {
                    langData.Remove(key);
                }

                deletedKeys.Add(key);
                deletedCount++;
            }

            // Save once at the end
            if (deletedCount > 0)
            {
                if (_keyRepository != null)
                    await _keyRepository.SaveAsync();

                await SaveAllLoadedLanguagesAsync();

                // Fire events
                foreach (var key in deletedKeys)
                {
                    OnKeyDeleted?.Invoke(key);
                }
            }

            return deletedCount;
        }

        /// <summary>
        /// Saves all loaded language files
        /// </summary>
        private async Task SaveAllLoadedLanguagesAsync()
        {
            var previousLanguage = _currentLanguageCode;

            foreach (var languageCode in _languageData.Keys)
            {
                _currentLanguageCode = languageCode;
                await SaveCurrentLanguageAsync();
            }

            _currentLanguageCode = previousLanguage;
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
