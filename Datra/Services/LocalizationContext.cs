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
            public string Text { get; set; }
            public string Context { get; set; }
        }
        
        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory _serializerFactory;
        private readonly DatraConfigurationValue _config;
        private readonly Dictionary<LanguageCode, Dictionary<string, LocalizationEntry>> _languageData;
        private DataRepository<string, LocalizationKeyData>? _keyRepository;
        private Dictionary<string, object> _languageRepositories; // Will be IDataRepository<string, LocalizationData> at runtime
        private LanguageCode _currentLanguageCode;
        private List<LanguageCode> _availableLanguages;
        
        /// <summary>
        /// Gets the current language as ISO code string (implements ILocalizationContext)
        /// </summary>
        public string CurrentLanguage => _currentLanguageCode.ToIsoCode();
        
        /// <summary>
        /// Gets the current language code enum
        /// </summary>
        public LanguageCode CurrentLanguageCode => _currentLanguageCode;
        
        public DataRepository<string, LocalizationKeyData> KeyRepository => _keyRepository ?? throw new InvalidOperationException("KeyRepository is not set. Make sure to call SetKeyRepository from generated code.");
        
        /// <summary>
        /// Creates a new LocalizationContext
        /// </summary>
        public LocalizationContext(IRawDataProvider rawDataProvider, DataSerializerFactory? serializerFactory = null, DatraConfigurationValue? config = null)
        {
            _rawDataProvider = rawDataProvider ?? throw new ArgumentNullException(nameof(rawDataProvider));
            _serializerFactory = serializerFactory ?? new DataSerializerFactory();
            _config = config ?? DatraConfigurationValue.CreateDefault();
            _languageData = new Dictionary<LanguageCode, Dictionary<string, LocalizationEntry>>();
            _languageRepositories = new Dictionary<string, object>();
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
        public void SetKeyRepository(DataRepository<string, LocalizationKeyData> keyRepository)
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
        public LocalizationKeyData GetKeyData(string key)
        {
            if (_keyRepository == null || string.IsNullOrEmpty(key))
                return null;
                
            return _keyRepository.GetValueOrDefault(key);
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
        /// Gets localized text for the specified key
        /// </summary>
        public string GetText(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            
            if (!_languageData.ContainsKey(_currentLanguageCode))
                return $"[{key}]";
            
            var languageDict = _languageData[_currentLanguageCode];
            return languageDict.TryGetValue(key, out var entry) ? entry.Text : $"[Missing: {key}]";
        }
        
        /// <summary>
        /// Sets localized text for the specified key
        /// </summary>
        public void SetText(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;
            
            if (!_languageData.ContainsKey(_currentLanguageCode))
            {
                _languageData[_currentLanguageCode] = new Dictionary<string, LocalizationEntry>();
            }
            
            // Preserve existing context if available
            var context = "";
            if (_languageData[_currentLanguageCode].TryGetValue(key, out var existingEntry))
            {
                context = existingEntry.Context;
            }
            
            _languageData[_currentLanguageCode][key] = new LocalizationEntry { Text = value, Context = context };
        }
        
        /// <summary>
        /// Saves the current language data to file
        /// </summary>
        public async Task SaveCurrentLanguageAsync()
        {
            if (!_languageData.ContainsKey(_currentLanguageCode))
                return;
            
            var dataPath = System.IO.Path.Combine(_config.LocalizationDataPath, _currentLanguageCode.GetFileName());
            var csvContent = BuildCsvContent(_languageData[_currentLanguageCode]);
            
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
    }
}