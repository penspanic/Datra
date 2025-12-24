using System.Collections.Generic;
using System.Linq;
using Datra.Generators.Builders;
using Datra.Generators.Models;

namespace Datra.Generators.Generators
{
    internal class DataContextGenerator
    {
        private string _localizationKeysPath = "Localizations/LocalizationKeys.csv";
        private string _localizationDataPath = "Localizations/";
        private string _defaultLanguage = "English";
        private bool _enableLocalization = false;
        private bool _enableDebugLogging = false;
        
        public string GenerateDataContext(string namespaceName, string contextName, List<DataModelInfo> dataModels, 
            string localizationKeysPath = null, string localizationDataPath = null, string defaultLanguage = null,
            bool enableLocalization = false, bool enableDebugLogging = false)
        {
            _localizationKeysPath = localizationKeysPath ?? "Localizations/LocalizationKeys.csv";
            _localizationDataPath = localizationDataPath ?? "Localizations/";
            _defaultLanguage = defaultLanguage ?? "English";
            _enableLocalization = enableLocalization;
            _enableDebugLogging = enableDebugLogging;
            GeneratorLogger.Log($"Generating DataContext: {contextName} with {dataModels.Count} models, LocalizationKeysPath: {_localizationKeysPath}, EnableLocalization: {_enableLocalization}");
            
            // Log all models for debugging
            foreach (var model in dataModels)
            {
                GeneratorLogger.Log($"  - Model: {model.TypeName}, Property: {model.PropertyName}, IsTable: {model.IsTableData}");
            }
            
            var builder = new CodeBuilder();

            // Disable nullable reference types to avoid CS8632 warnings in Unity
            builder.AppendLine("#nullable disable");

            // Add using statements
            builder.AddUsings(new[]
            {
                "System",
                "System.Collections.Generic",
                "System.Threading.Tasks",
                "Datra",
                "Datra.Configuration",
                "Datra.DataTypes",
                "Datra.Interfaces",
                "Datra.Serializers",
                "Datra.Repositories"
            });
            
            // Add namespaces for data models
            // Since we're using a dedicated namespace (Datra.Generated), 
            // we need to include all model namespaces
            var modelNamespaces = dataModels
                .Select(m => CodeBuilder.GetNamespace(m.TypeName))
                .Distinct()
                .Where(ns => !string.IsNullOrEmpty(ns))
                .ToList(); // Force evaluation
            
            GeneratorLogger.Log($"Adding {modelNamespaces.Count} model namespaces:");
            foreach (var ns in modelNamespaces)
            {
                GeneratorLogger.Log($"  - {ns}");
            }
            
            builder.AddUsings(modelNamespaces);
            builder.AddBlankLine();
            
            // Begin namespace and class
            builder.BeginNamespace(namespaceName);
            builder.BeginClass(contextName, "public partial", "BaseDataContext");
            
            // Add field for configuration
            builder.AppendLine("private readonly DatraConfigurationValue _config;");
            builder.AppendLine("private readonly global::Datra.Interfaces.ISerializationLogger _logger;");
            
            // Add LocalizationContext property only if enabled
            if (_enableLocalization)
            {
                builder.AppendLine("public Datra.Services.LocalizationContext Localization { get; private set; }");
            }
            builder.AddBlankLine();
            
            // Constructor
            GenerateConstructor(builder, contextName, namespaceName);
            builder.AddBlankLine();
            
            // InitializeRepositories method
            GenerateInitializeRepositories(builder, dataModels);
            builder.AddBlankLine();
            
            // InitializeDataTypeInfos method
            GenerateInitializeDataTypeInfos(builder, dataModels);
            builder.AddBlankLine();
            
            // LoadAllAsync method
            GenerateLoadAllAsync(builder, dataModels);
            builder.AddBlankLine();
            
            // Properties
            GenerateProperties(builder, dataModels);
            
            builder.EndClass();
            builder.EndNamespace();
            
            var result = builder.ToString();
            GeneratorLogger.Log($"DataContext generation completed. Size: {result.Length} characters");
            
            return result;
        }

        private void GenerateConstructor(CodeBuilder builder, string contextName, string namespaceName)
        {
            builder.AppendLine($"public {contextName}(IRawDataProvider rawDataProvider, DataSerializerFactory serializerFactory = null, DatraConfigurationValue config = null, global::Datra.Interfaces.ISerializationLogger logger = null)");
            builder.AppendLine("    : base(rawDataProvider, serializerFactory ?? new DataSerializerFactory())");
            builder.BeginBlock();
            
            // Create default config if not provided
            builder.AppendLine($"_config = config ?? new DatraConfigurationValue(");
            builder.AppendLine($"    enableLocalization: {(_enableLocalization ? "true" : "false")},");
            builder.AppendLine($"    localizationKeyDataPath: \"{_localizationKeysPath}\",");
            builder.AppendLine($"    localizationDataPath: \"{_localizationDataPath}\",");
            builder.AppendLine($"    defaultLanguage: \"{_defaultLanguage}\",");
            builder.AppendLine($"    dataContextName: \"{contextName}\",");
            builder.AppendLine($"    generatedNamespace: \"{namespaceName}\",");
            builder.AppendLine($"    enableDebugLogging: {(_enableDebugLogging ? "true" : "false")}");
            builder.AppendLine(");");
            builder.AppendLine("_logger = logger ?? global::Datra.Logging.NullSerializationLogger.Instance;");

            if (_enableLocalization)
            {
                builder.AppendLine("Localization = new Datra.Services.LocalizationContext(rawDataProvider, serializerFactory ?? new DataSerializerFactory(), _config);");
            }
            builder.AppendLine("InitializeRepositories();");
            builder.EndBlock();
        }

        private void GenerateInitializeRepositories(CodeBuilder builder, List<DataModelInfo> dataModels)
        {
            builder.BeginMethod("protected override void InitializeRepositories()");

            foreach (var model in dataModels)
            {
                var simpleTypeName = CodeBuilder.GetSimpleTypeName(model.TypeName);
                var isCsvFormat = CodeBuilder.GetDataFormat(model.Format) == "Csv";

                if (model.IsAssetData)
                {
                    GenerateAssetRepository(builder, model, simpleTypeName);
                }
                else if (model.IsTableData)
                {
                    GenerateTableRepository(builder, model, simpleTypeName, isCsvFormat);
                }
                else
                {
                    GenerateSingleRepository(builder, model, simpleTypeName);
                }
            }

            builder.EndMethod();
        }

        private void GenerateTableRepository(CodeBuilder builder, DataModelInfo model, string simpleTypeName, bool isCsvFormat)
        {
            // Multi-file mode: use MultiFileKeyValueDataRepository
            if (model.IsMultiFile)
            {
                builder.AppendLine($"{model.PropertyName} = new MultiFileKeyValueDataRepository<{model.KeyType}, {model.TypeName}>(");
                builder.AppendLine($"    \"{model.FilePath}\",");
                builder.AppendLine($"    \"{model.FilePattern}\",");
                builder.AppendLine($"    RawDataProvider,");
                builder.AppendLine($"    SerializerFactory,");
                builder.AppendLine($"    (data, serializer) => {simpleTypeName}Serializer.DeserializeSingleItem(data, serializer)");
                builder.AppendLine(");");
                builder.AppendLine($"RegisterRepository(\"{model.PropertyName}\", {model.PropertyName});");
                return;
            }

            if (isCsvFormat)
            {
                // Use CSV-specific constructor
                builder.AppendLine($"{model.PropertyName} = new KeyValueDataRepository<{model.KeyType}, {model.TypeName}>(");
                builder.AppendLine($"    \"{model.FilePath}\",");
                builder.AppendLine($"    RawDataProvider,");
                builder.AppendLine($"    (data) => {simpleTypeName}Serializer.DeserializeCsv(data, _config, _logger),");
                builder.AppendLine($"    (table) => {simpleTypeName}Serializer.SerializeCsv(table, _config, _logger)");
                builder.AppendLine(");");
                builder.AppendLine($"RegisterRepository(\"{model.PropertyName}\", {model.PropertyName});");
            }
            else
            {
                // Use standard constructor with serializer
                builder.AppendLine($"{model.PropertyName} = new KeyValueDataRepository<{model.KeyType}, {model.TypeName}>(");
                builder.AppendLine($"    \"{model.FilePath}\",");
                builder.AppendLine($"    RawDataProvider,");
                builder.AppendLine($"    SerializerFactory,");
                builder.AppendLine($"    (data, serializer) => {simpleTypeName}Serializer.DeserializeTable(data, serializer),");
                builder.AppendLine($"    (table, serializer) => {simpleTypeName}Serializer.SerializeTable(table, serializer)");
                builder.AppendLine(");");
                builder.AppendLine($"RegisterRepository(\"{model.PropertyName}\", {model.PropertyName});");
            }
        }

        private void GenerateSingleRepository(CodeBuilder builder, DataModelInfo model, string simpleTypeName)
        {
            builder.AppendLine($"{model.PropertyName} = new SingleDataRepository<{model.TypeName}>(");
            builder.AppendLine($"    \"{model.FilePath}\",");
            builder.AppendLine($"    RawDataProvider,");
            builder.AppendLine($"    SerializerFactory,");
            builder.AppendLine($"    (data, serializer) => {simpleTypeName}Serializer.DeserializeSingle(data, serializer),");
            builder.AppendLine($"    (obj, serializer) => {simpleTypeName}Serializer.SerializeSingle(obj, serializer)");
            builder.AppendLine(");");
            builder.AppendLine($"RegisterSingleRepository(\"{model.PropertyName}\", {model.PropertyName});");
        }

        private void GenerateAssetRepository(CodeBuilder builder, DataModelInfo model, string simpleTypeName)
        {
            builder.AppendLine($"{model.PropertyName} = new AssetRepository<{model.TypeName}>(");
            builder.AppendLine($"    \"{model.FilePath}\",");
            builder.AppendLine($"    \"{model.FilePattern}\",");
            builder.AppendLine($"    RawDataProvider,");
            builder.AppendLine($"    SerializerFactory,");
            builder.AppendLine($"    (data, serializer) => {simpleTypeName}Serializer.DeserializeSingle(data, serializer),");
            builder.AppendLine($"    (obj, serializer) => {simpleTypeName}Serializer.SerializeSingle(obj, serializer)");
            builder.AppendLine(");");
            builder.AppendLine($"RegisterAssetRepository(\"{model.PropertyName}\", {model.PropertyName});");
        }

        private void GenerateInitializeDataTypeInfos(CodeBuilder builder, List<DataModelInfo> dataModels)
        {
            builder.BeginMethod("protected override void InitializeDataTypeInfos()");
            
            foreach (var model in dataModels)
            {
                builder.AppendLine($"RegisterDataTypeInfo(new DataTypeInfo(");
                builder.AppendLine($"    typeName: \"{model.TypeName}\",");
                builder.AppendLine($"    dataType: typeof({model.TypeName}),");
                builder.AppendLine($"    filePath: \"{model.FilePath}\",");
                builder.AppendLine($"    propertyName: \"{model.PropertyName}\",");
                builder.AppendLine($"    isSingleData: {(model.IsTableData ? "false" : "true")}");
                builder.AppendLine("));");
            }
            
            builder.EndMethod();
        }

        private void GenerateLoadAllAsync(CodeBuilder builder, List<DataModelInfo> dataModels)
        {
            builder.BeginMethod("public override async Task LoadAllAsync()");
            
            // Initialize LocalizationContext first only if enabled
            if (_enableLocalization)
            {
                builder.AppendLine("// Initialize LocalizationContext");
                builder.AppendLine("var keyRepository = new KeyValueDataRepository<string, Datra.Models.LocalizationKeyData>(");
                builder.AppendLine("    _config.LocalizationKeyDataPath,");
                builder.AppendLine("    RawDataProvider,");
                builder.AppendLine("    (data) => LocalizationKeyDataSerializer.DeserializeCsv(data, _config, _logger),");
                builder.AppendLine("    (table) => LocalizationKeyDataSerializer.SerializeCsv(table, _config, _logger)");
                builder.AppendLine(");");
                builder.AppendLine("Localization.SetKeyRepository(keyRepository);");
                builder.AppendLine("await Localization.InitializeAsync();");
                builder.AddBlankLine();
            }
            
            // Define local generic function to load and update
            builder.AppendLine("// Local function to load repository and update DataTypeInfo");
            builder.AppendLine("async Task LoadAndUpdateAsync<TRepo>(TRepo repo, string propertyName) where TRepo : IDataRepository");
            builder.BeginBlock();
            builder.AppendLine("if (repo == null) throw new Exception(\"Repo is null! type: {typeof(TRepo)}\");");
            builder.AddBlankLine();
            builder.AppendLine("await repo.LoadAsync();");
            builder.AddBlankLine();
            builder.AppendLine("// Get loaded file path and update DataTypeInfo");
            builder.AppendLine("var loadedPath = repo.GetLoadedFilePath();");
            builder.AppendLine("if (string.IsNullOrEmpty(loadedPath)) throw new Exception(\"loadedPath is null or empty! type: {typeof(TRepo)}\");");
            builder.AppendLine("UpdateDataTypeInfoAfterLoad(propertyName, loadedPath);");
            builder.EndBlock();
            builder.AddBlankLine();
            
            // Create tasks for loading all repositories
            builder.AppendLine("// Load all repositories in parallel");
            builder.AppendLine("var tasks = new List<Task>();");
            builder.AddBlankLine();
            
            foreach (var model in dataModels)
            {
                builder.AppendLine($"tasks.Add(LoadAndUpdateAsync({model.PropertyName}, \"{model.PropertyName}\"));");
            }
            
            builder.AddBlankLine();
            builder.AppendLine("await Task.WhenAll(tasks);");
            
            builder.EndMethod();
        }

        private void GenerateProperties(CodeBuilder builder, List<DataModelInfo> dataModels)
        {
            foreach (var model in dataModels)
            {
                if (model.IsAssetData)
                {
                    builder.AppendLine($"public IAssetRepository<{model.TypeName}> {model.PropertyName} {{ get; private set; }}");
                }
                else if (model.IsTableData)
                {
                    builder.AppendLine($"public IDataRepository<{model.KeyType}, {model.TypeName}> {model.PropertyName} {{ get; private set; }}");
                }
                else
                {
                    builder.AppendLine($"public ISingleDataRepository<{model.TypeName}> {model.PropertyName} {{ get; private set; }}");
                }
            }
        }
    }
}