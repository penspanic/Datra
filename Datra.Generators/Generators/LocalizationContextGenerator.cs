using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datra.Generators.Builders;
using Datra.Generators.Models;

namespace Datra.Generators.Generators
{
    internal class LocalizationContextGenerator
    {
        public string GenerateLocalizationContext(List<DataModelInfo> localizationModels)
        {
            GeneratorLogger.Log($"Generating LocalizationContext with {localizationModels.Count} models");
            
            var builder = new CodeBuilder();
            
            // Add using statements
            builder.AddUsings(new[]
            {
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Threading.Tasks",
                "Datra",
                "Datra.Attributes",
                "Datra.Interfaces",
                "Datra.Serializers",
                "Datra.Repositories"
            });
            
            // Add namespaces for localization models
            var modelNamespaces = localizationModels
                .Select(m => CodeBuilder.GetNamespace(m.TypeName))
                .Distinct()
                .Where(ns => !string.IsNullOrEmpty(ns))
                .ToList();
            
            builder.AddUsings(modelNamespaces);
            builder.AddBlankLine();
            
            // Begin namespace and class
            builder.BeginNamespace("Datra.Generated");
            builder.BeginClass("LocalizationContext", "public partial", "ILocalizationContext");
            
            // Private fields
            builder.AppendLine("private readonly IRawDataProvider _rawDataProvider;");
            builder.AppendLine("private readonly DataSerializerFactory _serializerFactory;");
            builder.AppendLine("private readonly Dictionary<string, IDataRepository<string, LocalizationData>> _languageRepositories;");
            builder.AppendLine("private IDataRepository<string, LocalizationKeyData> _keyRepository;");
            builder.AppendLine("private Dictionary<string, LocalizationData> _currentLanguageData;");
            builder.AppendLine("private string _currentLanguage;");
            builder.AddBlankLine();
            
            // CurrentLanguage property
            builder.AppendLine("public string CurrentLanguage => _currentLanguage;");
            builder.AddBlankLine();
            
            // Constructor
            GenerateConstructor(builder);
            builder.AddBlankLine();
            
            // InitializeAsync method
            GenerateInitializeAsync(builder);
            builder.AddBlankLine();
            
            // LoadLanguageAsync method
            GenerateLoadLanguageAsync(builder);
            builder.AddBlankLine();
            
            // GetText methods
            GenerateGetTextMethods(builder);
            builder.AddBlankLine();
            
            // HasKey method
            GenerateHasKeyMethod(builder);
            builder.AddBlankLine();
            
            // GetAvailableLanguages method
            GenerateGetAvailableLanguagesMethod(builder);
            
            builder.EndClass();
            builder.EndNamespace();
            
            return builder.ToString();
        }
        
        private void GenerateConstructor(CodeBuilder builder)
        {
            builder.AppendLine("public LocalizationContext(IRawDataProvider rawDataProvider, DataSerializerFactory serializerFactory = null)");
            builder.BeginBlock();
            builder.AppendLine("_rawDataProvider = rawDataProvider ?? throw new ArgumentNullException(nameof(rawDataProvider));");
            builder.AppendLine("_serializerFactory = serializerFactory ?? new DataSerializerFactory();");
            builder.AppendLine("_languageRepositories = new Dictionary<string, IDataRepository<string, LocalizationData>>();");
            builder.AppendLine("_currentLanguageData = new Dictionary<string, LocalizationData>();");
            builder.EndBlock();
        }
        
        private void GenerateInitializeAsync(CodeBuilder builder)
        {
            builder.AppendLine("public async Task InitializeAsync()");
            builder.BeginBlock();
            
            // Load localization keys
            builder.AppendLine("// Load localization key definitions");
            builder.AppendLine("var keyDataPath = \"Localizations/LocalizationKeys.csv\";");
            builder.AppendLine("if (_rawDataProvider.Exists(keyDataPath))");
            builder.BeginBlock();
            builder.AppendLine("var keySerializer = _serializerFactory.GetSerializer(DataFormat.Csv);");
            builder.AppendLine("var keyRawData = await _rawDataProvider.LoadTextAsync(keyDataPath);");
            builder.AppendLine("var keyDataList = keySerializer.Deserialize<LocalizationKeyData[]>(keyRawData);");
            builder.AppendLine("_keyRepository = new DataRepository<string, LocalizationKeyData>();");
            builder.AppendLine("foreach (var item in keyDataList)");
            builder.BeginBlock();
            builder.AppendLine("_keyRepository.Add(item);");
            builder.EndBlock();
            builder.EndBlock();
            
            builder.EndBlock();
        }
        
        private void GenerateLoadLanguageAsync(CodeBuilder builder)
        {
            builder.AppendLine("public async Task LoadLanguageAsync(string languageCode)");
            builder.BeginBlock();
            
            builder.AppendLine("if (string.IsNullOrEmpty(languageCode))");
            builder.AppendLine("    throw new ArgumentNullException(nameof(languageCode));");
            builder.AddBlankLine();
            
            builder.AppendLine("// Check if language repository already loaded");
            builder.AppendLine("if (!_languageRepositories.ContainsKey(languageCode))");
            builder.BeginBlock();
            
            builder.AppendLine($"var dataPath = $\"Localizations/{{languageCode}}.csv\";");
            builder.AppendLine("if (!_rawDataProvider.Exists(dataPath))");
            builder.BeginBlock();
            builder.AppendLine($"throw new InvalidOperationException($\"Localization file for language '{{languageCode}}' not found at {{dataPath}}\");");
            builder.EndBlock();
            
            builder.AppendLine("var serializer = _serializerFactory.GetSerializer(DataFormat.Csv);");
            builder.AppendLine("var rawData = await _rawDataProvider.LoadTextAsync(dataPath);");
            builder.AppendLine("var dataList = serializer.Deserialize<LocalizationData[]>(rawData);");
            builder.AppendLine("var repository = new DataRepository<string, LocalizationData>();");
            builder.AppendLine("foreach (var item in dataList)");
            builder.BeginBlock();
            builder.AppendLine("repository.Add(item);");
            builder.EndBlock();
            builder.AppendLine("_languageRepositories[languageCode] = repository;");
            
            builder.EndBlock();
            
            builder.AppendLine("// Update current language data");
            builder.AppendLine("_currentLanguage = languageCode;");
            builder.AppendLine("_currentLanguageData = new Dictionary<string, LocalizationData>();");
            builder.AppendLine("foreach (var item in _languageRepositories[languageCode].GetValues())");
            builder.BeginBlock();
            builder.AppendLine("_currentLanguageData[item.Id] = item;");
            builder.EndBlock();
            
            builder.EndBlock();
        }
        
        private void GenerateGetTextMethods(CodeBuilder builder)
        {
            // GetText(string key)
            builder.AppendLine("public string GetText(string key)");
            builder.BeginBlock();
            builder.AppendLine("if (string.IsNullOrEmpty(key))");
            builder.AppendLine("    return string.Empty;");
            builder.AddBlankLine();
            builder.AppendLine("if (_currentLanguageData == null || _currentLanguageData.Count == 0)");
            builder.AppendLine("    return $\"[{key}]\";");
            builder.AddBlankLine();
            builder.AppendLine("if (_currentLanguageData.TryGetValue(key, out var data))");
            builder.AppendLine("    return data.Text ?? $\"[{key}]\";");
            builder.AddBlankLine();
            builder.AppendLine("return $\"[Missing: {key}]\";");
            builder.EndBlock();
            
            builder.AddBlankLine();
            
            // GetText(LocalizationKey key)
            builder.AppendLine("public string GetText(LocalizationKey key)");
            builder.BeginBlock();
            builder.AppendLine("return GetText(key.ToString());");
            builder.EndBlock();
        }
        
        private void GenerateHasKeyMethod(CodeBuilder builder)
        {
            builder.AppendLine("public bool HasKey(string key)");
            builder.BeginBlock();
            builder.AppendLine("if (string.IsNullOrEmpty(key))");
            builder.AppendLine("    return false;");
            builder.AddBlankLine();
            builder.AppendLine("if (_currentLanguageData == null || _currentLanguageData.Count == 0)");
            builder.AppendLine("    return false;");
            builder.AddBlankLine();
            builder.AppendLine("return _currentLanguageData.ContainsKey(key);");
            builder.EndBlock();
        }
        
        private void GenerateGetAvailableLanguagesMethod(CodeBuilder builder)
        {
            builder.AppendLine("public IEnumerable<string> GetAvailableLanguages()");
            builder.BeginBlock();
            builder.AppendLine("return _languageRepositories.Keys;");
            builder.EndBlock();
        }
        
    }
}