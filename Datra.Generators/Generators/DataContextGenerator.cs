using System.Collections.Generic;
using System.Linq;
using Datra.Generators.Builders;
using Datra.Generators.Models;

namespace Datra.Generators.Generators
{
    internal class DataContextGenerator
    {
        public string GenerateDataContext(string namespaceName, string contextName, List<DataModelInfo> dataModels)
        {
            GeneratorLogger.Log($"Generating DataContext: {contextName} with {dataModels.Count} models");
            
            // Log all models for debugging
            foreach (var model in dataModels)
            {
                GeneratorLogger.Log($"  - Model: {model.TypeName}, Property: {model.PropertyName}, IsTable: {model.IsTableData}");
            }
            
            var builder = new CodeBuilder();
            
            // Add using statements
            builder.AddUsings(new[]
            {
                "System",
                "System.Collections.Generic",
                "System.Threading.Tasks",
                "Datra",
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
            builder.AppendLine("private readonly Datra.Configuration.DatraConfiguration _config;");
            builder.AddBlankLine();
            
            // Constructor
            GenerateConstructor(builder, contextName);
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

        private void GenerateConstructor(CodeBuilder builder, string contextName)
        {
            builder.AppendLine($"public {contextName}(IRawDataProvider rawDataProvider, DataSerializerFactory serializerFactory = null, Datra.Configuration.DatraConfiguration config = null)");
            builder.AppendLine("    : base(rawDataProvider, serializerFactory ?? new DataSerializerFactory())");
            builder.BeginBlock();
            builder.AppendLine("_config = config ?? Datra.Configuration.DatraConfiguration.CreateDefault();");
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
                
                if (model.IsTableData)
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
            if (isCsvFormat)
            {
                // Use CSV-specific constructor
                builder.AppendLine($"{model.PropertyName} = new DataRepository<{model.KeyType}, {model.TypeName}>(");
                builder.AppendLine($"    \"{model.FilePath}\",");
                builder.AppendLine($"    RawDataProvider,");
                builder.AppendLine($"    (data) => {simpleTypeName}Serializer.DeserializeCsv(data, _config),");
                builder.AppendLine($"    (table) => {simpleTypeName}Serializer.SerializeCsv(table, _config)");
                builder.AppendLine(");");
                builder.AppendLine($"RegisterRepository(\"{model.PropertyName}\", {model.PropertyName});");
            }
            else
            {
                // Use standard constructor with serializer
                builder.AppendLine($"{model.PropertyName} = new DataRepository<{model.KeyType}, {model.TypeName}>(");
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
            
            // Define local generic function to load and update
            builder.AppendLine("// Local function to load repository and update DataTypeInfo");
            builder.AppendLine("async Task LoadAndUpdateAsync<TRepo>(TRepo repo, string propertyName) where TRepo : class");
            builder.BeginBlock();
            builder.AppendLine("if (repo == null) return;");
            builder.AddBlankLine();
            builder.AppendLine("// Try to call LoadAsync if it exists");
            builder.AppendLine("var loadMethod = repo.GetType().GetMethod(\"LoadAsync\");");
            builder.AppendLine("if (loadMethod != null)");
            builder.BeginBlock();
            builder.AppendLine("await (Task)loadMethod.Invoke(repo, null);");
            builder.EndBlock();
            builder.AddBlankLine();
            builder.AppendLine("// Get loaded file path and update DataTypeInfo");
            builder.AppendLine("var getPathMethod = repo.GetType().GetMethod(\"GetLoadedFilePath\");");
            builder.AppendLine("if (getPathMethod != null)");
            builder.BeginBlock();
            builder.AppendLine("var loadedPath = (string)getPathMethod.Invoke(repo, null);");
            builder.AppendLine("if (!string.IsNullOrEmpty(loadedPath))");
            builder.BeginBlock();
            builder.AppendLine("UpdateDataTypeInfoAfterLoad(propertyName, loadedPath);");
            builder.EndBlock();
            builder.EndBlock();
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
                if (model.IsTableData)
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