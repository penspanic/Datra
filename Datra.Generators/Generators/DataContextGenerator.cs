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

        private void GenerateLoadAllAsync(CodeBuilder builder, List<DataModelInfo> dataModels)
        {
            builder.BeginMethod("public override async Task LoadAllAsync()");
            builder.AppendLine("var tasks = new List<Task>();");
            
            foreach (var model in dataModels)
            {
                if (model.IsTableData)
                {
                    builder.AppendLine($"if ({model.PropertyName} != null) tasks.Add(((DataRepository<{model.KeyType}, {model.TypeName}>){model.PropertyName}).LoadAsync());");
                }
                else
                {
                    builder.AppendLine($"if ({model.PropertyName} != null) tasks.Add(((SingleDataRepository<{model.TypeName}>){model.PropertyName}).LoadAsync());");
                }
            }
            
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