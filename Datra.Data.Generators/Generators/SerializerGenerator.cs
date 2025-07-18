using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Datra.Data.Generators.Builders;
using Datra.Data.Generators.Models;

namespace Datra.Data.Generators.Generators
{
    internal class SerializerGenerator
    {
        private readonly GeneratorExecutionContext _context;
        
        public SerializerGenerator(GeneratorExecutionContext context)
        {
            _context = context;
        }
        
        public string GenerateSerializerFile(DataModelInfo model)
        {
            var codeBuilder = new CodeBuilder();
            var simpleTypeName = CodeBuilder.GetSimpleTypeName(model.TypeName);
            var namespaceName = CodeBuilder.GetNamespace(model.TypeName);
            
            // Add using statements
            codeBuilder.AddUsings(new[]
            {
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.IO",
                "System.Text",
                "Newtonsoft.Json",
                "Newtonsoft.Json.Linq",
                "System.Globalization",
                "Datra.Data.Interfaces"
            });
            
            codeBuilder.AddBlankLine();
            
            // Begin namespace
            codeBuilder.BeginNamespace(namespaceName);
            
            // Generate partial class with constructors
            codeBuilder.BeginClass(simpleTypeName, "public partial");
            GenerateConstructors(codeBuilder, model, simpleTypeName);
            codeBuilder.EndClass();
            
            codeBuilder.AddBlankLine();
            
            // Generate serializer class
            codeBuilder.BeginClass($"{simpleTypeName}Serializer", "public static");
            
            if (model.IsTableData)
            {
                GenerateTableSerializerMethods(codeBuilder, model, simpleTypeName);
            }
            else
            {
                GenerateSingleSerializerMethods(codeBuilder, model, simpleTypeName);
            }
            
            codeBuilder.EndClass();
            
            // End namespace
            codeBuilder.EndNamespace();
            
            return codeBuilder.ToString();
        }
        
        private void GenerateConstructors(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            // Default constructor
            codeBuilder.BeginMethod($"public {typeName}()");
            
            foreach (var prop in model.Properties)
            {
                if (prop.Type == "string")
                {
                    codeBuilder.AppendLine($"{prop.Name} = string.Empty;");
                }
                else if (prop.Type.Contains(".") && !prop.Type.StartsWith("System."))
                {
                    // Nested classes or other types
                    codeBuilder.AppendLine($"{prop.Name} = new {prop.Type.Split('.').Last()}();");
                }
            }
            
            codeBuilder.EndMethod();
            codeBuilder.AddBlankLine();
            
            // Parameterized constructor
            var parameters = model.Properties.Select(p => 
                $"{p.Type} {CodeBuilder.ToCamelCase(p.Name)}"
            );
            
            codeBuilder.BeginMethod($"public {typeName}({string.Join(", ", parameters)})");
            
            foreach (var prop in model.Properties)
            {
                var paramName = CodeBuilder.ToCamelCase(prop.Name);
                codeBuilder.AppendLine($"{prop.Name} = {paramName};");
            }
            
            codeBuilder.EndMethod();
        }
        
        private void GenerateTableSerializerMethods(CodeBuilder codeBuilder, DataModelInfo model, string simpleTypeName)
        {
            var format = CodeBuilder.GetDataFormat(model.Format);
            
            // Deserialize method
            codeBuilder.BeginMethod($"public static Dictionary<{model.KeyType}, {simpleTypeName}> DeserializeTable(string data, Datra.Data.Loaders.IDataLoader loader)");
            
            switch (format)
            {
                case "Csv":
                    var csvBuilder = new CsvSerializerBuilder();
                    csvBuilder.GenerateTableDeserializer(codeBuilder, model, simpleTypeName);
                    break;
                case "Json":
                    var jsonBuilder = new JsonSerializerBuilder();
                    jsonBuilder.GenerateTableDeserializer(codeBuilder, model, simpleTypeName);
                    break;
                case "Yaml":
                    codeBuilder.AppendLine("// YAML implementation requires YamlDotNet package");
                    codeBuilder.AppendLine($"return loader.LoadTable<{model.KeyType}, {simpleTypeName}>(data);");
                    break;
                default:
                    codeBuilder.AppendLine($"return loader.LoadTable<{model.KeyType}, {simpleTypeName}>(data);");
                    break;
            }
            
            codeBuilder.EndMethod();
            codeBuilder.AddBlankLine();
            
            // Serialize method
            codeBuilder.BeginMethod($"public static string SerializeTable(Dictionary<{model.KeyType}, {simpleTypeName}> table, Datra.Data.Loaders.IDataLoader loader)");
            
            switch (format)
            {
                case "Csv":
                    var csvBuilder = new CsvSerializerBuilder();
                    csvBuilder.GenerateTableSerializer(codeBuilder, model, simpleTypeName);
                    break;
                case "Json":
                    var jsonBuilder = new JsonSerializerBuilder();
                    jsonBuilder.GenerateTableSerializer(codeBuilder, model, simpleTypeName);
                    break;
                case "Yaml":
                    codeBuilder.AppendLine("// YAML implementation requires YamlDotNet package");
                    codeBuilder.AppendLine($"return loader.SaveTable<{model.KeyType}, {simpleTypeName}>(table);");
                    break;
                default:
                    codeBuilder.AppendLine($"return loader.SaveTable<{model.KeyType}, {simpleTypeName}>(table);");
                    break;
            }
            
            codeBuilder.EndMethod();
        }
        
        private void GenerateSingleSerializerMethods(CodeBuilder codeBuilder, DataModelInfo model, string simpleTypeName)
        {
            var format = CodeBuilder.GetDataFormat(model.Format);
            
            // Deserialize method
            codeBuilder.BeginMethod($"public static {simpleTypeName} DeserializeSingle(string data, Datra.Data.Loaders.IDataLoader loader)");
            
            switch (format)
            {
                case "Json":
                    var jsonBuilder = new JsonSerializerBuilder();
                    jsonBuilder.GenerateSingleDeserializer(codeBuilder, model, simpleTypeName);
                    break;
                case "Yaml":
                    codeBuilder.AppendLine("// YAML implementation requires YamlDotNet package");
                    codeBuilder.AppendLine($"return loader.LoadSingle<{simpleTypeName}>(data);");
                    break;
                default:
                    codeBuilder.AppendLine($"return loader.LoadSingle<{simpleTypeName}>(data);");
                    break;
            }
            
            codeBuilder.EndMethod();
            codeBuilder.AddBlankLine();
            
            // Serialize method
            codeBuilder.BeginMethod($"public static string SerializeSingle({simpleTypeName} data, Datra.Data.Loaders.IDataLoader loader)");
            
            switch (format)
            {
                case "Json":
                    var jsonBuilder = new JsonSerializerBuilder();
                    jsonBuilder.GenerateSingleSerializer(codeBuilder, model, simpleTypeName);
                    break;
                case "Yaml":
                    codeBuilder.AppendLine("// YAML implementation requires YamlDotNet package");
                    codeBuilder.AppendLine($"return loader.SaveSingle<{simpleTypeName}>(data);");
                    break;
                default:
                    codeBuilder.AppendLine($"return loader.SaveSingle<{simpleTypeName}>(data);");
                    break;
            }
            
            codeBuilder.EndMethod();
        }
    }
}