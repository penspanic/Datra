using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Datra.Generators.Builders;
using Datra.Generators.Models;

namespace Datra.Generators.Generators
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
                "Datra.Interfaces"
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
                if (prop.IsArray)
                {
                    // Initialize arrays with empty array
                    // Remove [] from the type since we're adding [0] at the end
                    var arrayElementType = prop.Type.EndsWith("[]") ? prop.Type.Substring(0, prop.Type.Length - 2) : prop.Type;
                    codeBuilder.AppendLine($"{prop.Name} = new {arrayElementType}[0];");
                }
                else if (prop.Type == "string")
                {
                    codeBuilder.AppendLine($"{prop.Name} = string.Empty;");
                }
                else if (prop.IsDataRef)
                {
                    // DataRef is a struct, initialize with default
                    codeBuilder.AppendLine($"{prop.Name} = default;");
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
                codeBuilder.AppendLine($"this.{prop.Name} = {paramName};");
            }
            
            codeBuilder.EndMethod();
        }
        
        private void GenerateTableSerializerMethods(CodeBuilder codeBuilder, DataModelInfo model, string simpleTypeName)
        {
            var format = CodeBuilder.GetDataFormat(model.Format);
            GeneratorLogger.Log($"GenerateTableSerializerMethods for {simpleTypeName}: model.Format='{model.Format}', format='{format}'");
            
            // Deserialize method
            codeBuilder.BeginMethod($"public static Dictionary<{model.KeyType}, {simpleTypeName}> DeserializeTable(string data, Datra.Serializers.IDataSerializer serializer)");
            
            // Always use serializer for DeserializeTable method
            codeBuilder.AppendLine($"return serializer.DeserializeTable<{model.KeyType}, {simpleTypeName}>(data);");
            
            codeBuilder.EndMethod();
            codeBuilder.AddBlankLine();
            
            // Serialize method
            codeBuilder.BeginMethod($"public static string SerializeTable(Dictionary<{model.KeyType}, {simpleTypeName}> table, Datra.Serializers.IDataSerializer serializer)");
            
            // Always use serializer for SerializeTable method
            codeBuilder.AppendLine($"return serializer.SerializeTable<{model.KeyType}, {simpleTypeName}>(table);");
            
            codeBuilder.EndMethod();
            
            // Generate CSV-specific methods without loader parameter
            if (format == "Csv")
            {
                codeBuilder.AddBlankLine();
                
                // CSV Deserialize method without serializer
                codeBuilder.BeginMethod($"public static Dictionary<{model.KeyType}, {simpleTypeName}> DeserializeCsv(string data, Datra.Configuration.DatraConfiguration config = null)");
                var csvBuilder2 = new CsvSerializerBuilder();
                csvBuilder2.GenerateTableDeserializer(codeBuilder, model, simpleTypeName);
                codeBuilder.EndMethod();
                
                codeBuilder.AddBlankLine();
                
                // CSV Serialize method without serializer
                codeBuilder.BeginMethod($"public static string SerializeCsv(Dictionary<{model.KeyType}, {simpleTypeName}> table, Datra.Configuration.DatraConfiguration config = null)");
                var csvBuilder3 = new CsvSerializerBuilder();
                csvBuilder3.GenerateTableSerializer(codeBuilder, model, simpleTypeName);
                codeBuilder.EndMethod();
            }
        }
        
        private void GenerateSingleSerializerMethods(CodeBuilder codeBuilder, DataModelInfo model, string simpleTypeName)
        {
            var format = CodeBuilder.GetDataFormat(model.Format);
            
            // Deserialize method
            codeBuilder.BeginMethod($"public static {simpleTypeName} DeserializeSingle(string data, Datra.Serializers.IDataSerializer serializer)");
            
            switch (format)
            {
                case "Json":
                    var jsonBuilder = new JsonSerializerBuilder();
                    jsonBuilder.GenerateSingleDeserializer(codeBuilder, model, simpleTypeName);
                    break;
                case "Yaml":
                    codeBuilder.AppendLine("// YAML implementation requires YamlDotNet package");
                    codeBuilder.AppendLine($"return serializer.DeserializeSingle<{simpleTypeName}>(data);");
                    break;
                default:
                    codeBuilder.AppendLine($"return serializer.DeserializeSingle<{simpleTypeName}>(data);");
                    break;
            }
            
            codeBuilder.EndMethod();
            codeBuilder.AddBlankLine();
            
            // Serialize method
            codeBuilder.BeginMethod($"public static string SerializeSingle({simpleTypeName} data, Datra.Serializers.IDataSerializer serializer)");
            
            switch (format)
            {
                case "Json":
                    var jsonBuilder = new JsonSerializerBuilder();
                    jsonBuilder.GenerateSingleSerializer(codeBuilder, model, simpleTypeName);
                    break;
                case "Yaml":
                    codeBuilder.AppendLine("// YAML implementation requires YamlDotNet package");
                    codeBuilder.AppendLine($"return serializer.SerializeSingle<{simpleTypeName}>(data);");
                    break;
                default:
                    codeBuilder.AppendLine($"return serializer.SerializeSingle<{simpleTypeName}>(data);");
                    break;
            }
            
            codeBuilder.EndMethod();
        }
    }
}