using System.Collections.Generic;
using System.Linq;
using Datra.Data.Generators.Builders;
using Datra.Data.Generators.Models;

namespace Datra.Data.Generators.Generators
{
    internal class DataContextGenerator
    {
        public string GenerateDataContext(string namespaceName, string contextName, List<DataModelInfo> dataModels)
        {
            GeneratorLogger.Log($"Generating DataContext: {contextName} with {dataModels.Count} models");
            
            var builder = new CodeBuilder();
            
            // Add using statements
            builder.AddUsings(new[]
            {
                "System",
                "System.Collections.Generic",
                "System.Threading.Tasks",
                "Datra.Data",
                "Datra.Data.Interfaces",
                "Datra.Data.Loaders",
                "Datra.Data.Repositories"
            });
            
            // Add namespaces for data models
            var modelNamespaces = dataModels
                .Select(m => CodeBuilder.GetNamespace(m.TypeName))
                .Distinct()
                .Where(ns => ns != namespaceName);
            
            builder.AddUsings(modelNamespaces);
            builder.AddBlankLine();
            
            // Begin namespace and class
            builder.BeginNamespace(namespaceName);
            builder.BeginClass(contextName, "public partial", "BaseDataContext");
            
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
            builder.AppendLine($"public {contextName}(IRawDataProvider rawDataProvider, DataLoaderFactory loaderFactory = null)");
            builder.AppendLine("    : base(rawDataProvider, loaderFactory ?? new DataLoaderFactory())");
            builder.BeginBlock();
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
                builder.AppendLine($"    (data) => {simpleTypeName}Serializer.DeserializeCsv(data),");
                builder.AppendLine($"    (table) => {simpleTypeName}Serializer.SerializeCsv(table)");
                builder.AppendLine(");");
            }
            else
            {
                // Use standard constructor with loader
                builder.AppendLine($"{model.PropertyName} = new DataRepository<{model.KeyType}, {model.TypeName}>(");
                builder.AppendLine($"    \"{model.FilePath}\",");
                builder.AppendLine($"    RawDataProvider,");
                builder.AppendLine($"    LoaderFactory,");
                builder.AppendLine($"    (data, loader) => {simpleTypeName}Serializer.DeserializeTable(data, loader),");
                builder.AppendLine($"    (table, loader) => {simpleTypeName}Serializer.SerializeTable(table, loader)");
                builder.AppendLine(");");
            }
        }

        private void GenerateSingleRepository(CodeBuilder builder, DataModelInfo model, string simpleTypeName)
        {
            builder.AppendLine($"{model.PropertyName} = new SingleDataRepository<{model.TypeName}>(");
            builder.AppendLine($"    \"{model.FilePath}\",");
            builder.AppendLine($"    RawDataProvider,");
            builder.AppendLine($"    LoaderFactory,");
            builder.AppendLine($"    (data, loader) => {simpleTypeName}Serializer.DeserializeSingle(data, loader),");
            builder.AppendLine($"    (obj, loader) => {simpleTypeName}Serializer.SerializeSingle(obj, loader)");
            builder.AppendLine(");");
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