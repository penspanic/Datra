using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Datra.Generators.Builders;
using Datra.Generators.Models;

namespace Datra.Generators.Generators
{
    internal class DataModelGenerator
    {
        private readonly GeneratorExecutionContext _context;
        
        public DataModelGenerator(GeneratorExecutionContext context)
        {
            _context = context;
        }
        
        private static bool IsPrimitiveType(string typeName)
        {
            // Primitive types should not have global:: prefix
            return typeName switch
            {
                "string" => true,
                "int" => true,
                "float" => true,
                "double" => true,
                "bool" => true,
                "byte" => true,
                "sbyte" => true,
                "short" => true,
                "ushort" => true,
                "uint" => true,
                "long" => true,
                "ulong" => true,
                "char" => true,
                "decimal" => true,
                "object" => true,
                _ => false
            };
        }
        
        public string GenerateDataModelFile(DataModelInfo model)
        {
            var codeBuilder = new CodeBuilder();
            var simpleTypeName = CodeBuilder.GetSimpleTypeName(model.TypeName);
            var namespaceName = CodeBuilder.GetNamespace(model.TypeName);

            // Disable nullable reference types to avoid CS8632 warnings in Unity
            codeBuilder.AppendLine("#nullable disable");

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
                "Datra.Interfaces",
                "Datra.DataTypes"
            });
            
            codeBuilder.AddBlankLine();
            
            // Begin namespace
            codeBuilder.BeginNamespace(namespaceName);
            
            // Generate partial class with constructors and Ref property
            codeBuilder.BeginClass(simpleTypeName, "public partial");
            GenerateConstructors(codeBuilder, model, simpleTypeName);
            
            // Generate Ref property for ITableData classes
            if (model.IsTableData)
            {
                GenerateRefProperty(codeBuilder, model, simpleTypeName);
            }
            
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
            // Add CsvMetadata property for CSV format to store ~ columns with their index
            if (CodeBuilder.IsCsvFormat(model.Format, model.FilePath))
            {
                codeBuilder.AppendLine("[global::Datra.Attributes.DatraIgnore]");
                codeBuilder.AppendLine("public global::System.Collections.Generic.Dictionary<string, (int columnIndex, string columnName, string value)> CsvMetadata { get; set; } = new global::System.Collections.Generic.Dictionary<string, (int columnIndex, string columnName, string value)>();");
                codeBuilder.AddBlankLine();
            }

            // Default constructor
            codeBuilder.BeginMethod($"public {typeName}()");

            // Initialize only serializable properties (excludes computed FixedLocale properties)
            foreach (var prop in model.GetConstructorProperties())
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
                else if (prop.IsNestedType)
                {
                    // Nested struct/class - initialize appropriately
                    if (prop.IsNestedStruct)
                    {
                        // Struct - use default or new
                        codeBuilder.AppendLine($"{prop.Name} = new {prop.Type}();");
                    }
                    else
                    {
                        // Class - create new instance
                        codeBuilder.AppendLine($"{prop.Name} = new {prop.Type}();");
                    }
                }
                else if (prop.Type.Contains(".") && !prop.Type.StartsWith("System."))
                {
                    // Other nested types not detected by IsNestedType
                    codeBuilder.AppendLine($"{prop.Name} = new {prop.Type}();");
                }
            }

            codeBuilder.EndMethod();
            codeBuilder.AddBlankLine();

            // Parameterized constructor (only constructor properties)
            var parameters = model.GetConstructorProperties()
                .Select(p => $"{p.Type} {CodeBuilder.ToCamelCase(p.Name)}");

            codeBuilder.BeginMethod($"public {typeName}({string.Join(", ", parameters)})");

            foreach (var prop in model.GetConstructorProperties())
            {
                var paramName = CodeBuilder.ToCamelCase(prop.Name);
                codeBuilder.AppendLine($"this.{prop.Name} = {paramName};");
            }

            codeBuilder.EndMethod();
        }
        
        private void GenerateRefProperty(CodeBuilder codeBuilder, DataModelInfo model, string simpleTypeName)
        {
            codeBuilder.AddBlankLine();
            
            // Determine the DataRef type based on KeyType
            string dataRefType;
            if (model.KeyType == "int")
            {
                dataRefType = $"global::Datra.DataTypes.IntDataRef<{model.TypeName}>";
            }
            else if (model.KeyType == "string")
            {
                dataRefType = $"global::Datra.DataTypes.StringDataRef<{model.TypeName}>";
            }
            else
            {
                // Skip if KeyType is not supported
                GeneratorLogger.Log($"Skipping Ref property generation for {simpleTypeName}: Unsupported KeyType '{model.KeyType}'");
                return;
            }
            
            // Generate Ref property
            codeBuilder.AppendLine($"public {dataRefType} Ref => new {dataRefType}(this.Id);");
        }
        
        private void GenerateTableSerializerMethods(CodeBuilder codeBuilder, DataModelInfo model, string simpleTypeName)
        {
            var isCsvFormat = CodeBuilder.IsCsvFormat(model.Format, model.FilePath);
            GeneratorLogger.Log($"GenerateTableSerializerMethods for {simpleTypeName}: model.Format='{model.Format}', isCsvFormat={isCsvFormat}, KeyType='{model.KeyType}'");
            
            // Validate KeyType
            if (string.IsNullOrEmpty(model.KeyType))
            {
                GeneratorLogger.LogError($"KeyType is null or empty for table data type {simpleTypeName}");
                model.KeyType = "string"; // Default fallback
            }
            
            // Deserialize method
            codeBuilder.BeginMethod($"public static global::System.Collections.Generic.Dictionary<{model.KeyType}, {simpleTypeName}> DeserializeTable(string data, global::Datra.Serializers.IDataSerializer serializer)");
            
            // Always use serializer for DeserializeTable method
            codeBuilder.AppendLine($"return serializer.DeserializeTable<{model.KeyType}, {simpleTypeName}>(data);");
            
            codeBuilder.EndMethod();
            codeBuilder.AddBlankLine();
            
            // Serialize method
            codeBuilder.BeginMethod($"public static string SerializeTable(global::System.Collections.Generic.Dictionary<{model.KeyType}, {simpleTypeName}> table, global::Datra.Serializers.IDataSerializer serializer)");
            
            // Always use serializer for SerializeTable method
            codeBuilder.AppendLine($"return serializer.SerializeTable<{model.KeyType}, {simpleTypeName}>(table);");
            
            codeBuilder.EndMethod();
            
            // Generate CSV-specific methods without loader parameter
            if (isCsvFormat)
            {
                codeBuilder.AddBlankLine();

                // CSV Deserialize method without serializer
                codeBuilder.BeginMethod($"public static global::System.Collections.Generic.Dictionary<{model.KeyType}, {simpleTypeName}> DeserializeCsv(string data, global::Datra.Configuration.DatraConfigurationValue config = null, global::Datra.Interfaces.ISerializationLogger logger = null)");
                var csvBuilder2 = new CsvSerializerBuilder();
                csvBuilder2.GenerateTableDeserializer(codeBuilder, model, simpleTypeName);
                codeBuilder.EndMethod();

                codeBuilder.AddBlankLine();

                // CSV Serialize method without serializer
                codeBuilder.BeginMethod($"public static string SerializeCsv(global::System.Collections.Generic.Dictionary<{model.KeyType}, {simpleTypeName}> table, global::Datra.Configuration.DatraConfigurationValue config = null, global::Datra.Interfaces.ISerializationLogger logger = null)");
                var csvBuilder3 = new CsvSerializerBuilder();
                csvBuilder3.GenerateTableSerializer(codeBuilder, model, simpleTypeName);
                codeBuilder.EndMethod();
            }

            // Generate YAML-specific methods without serializer parameter
            var isYamlFormat = CodeBuilder.IsYamlFormat(model.Format, model.FilePath);
            if (isYamlFormat)
            {
                codeBuilder.AddBlankLine();

                // YAML Deserialize method without serializer
                codeBuilder.BeginMethod($"public static global::System.Collections.Generic.Dictionary<{model.KeyType}, {simpleTypeName}> DeserializeYaml(string data)");
                var yamlBuilder = new YamlSerializerBuilder();
                yamlBuilder.GenerateTableDeserializer(codeBuilder, model, simpleTypeName);
                codeBuilder.EndMethod();

                codeBuilder.AddBlankLine();

                // YAML Serialize method without serializer
                codeBuilder.BeginMethod($"public static string SerializeYaml(global::System.Collections.Generic.Dictionary<{model.KeyType}, {simpleTypeName}> table)");
                var yamlBuilder2 = new YamlSerializerBuilder();
                yamlBuilder2.GenerateTableSerializer(codeBuilder, model, simpleTypeName);
                codeBuilder.EndMethod();
            }

            // Generate DeserializeSingleItem for multi-file mode
            if (model.IsMultiFile)
            {
                codeBuilder.AddBlankLine();
                codeBuilder.AppendLine("/// <summary>");
                codeBuilder.AppendLine("/// Deserialize a single item (for multi-file mode)");
                codeBuilder.AppendLine("/// </summary>");
                codeBuilder.BeginMethod($"public static {simpleTypeName} DeserializeSingleItem(string data, global::Datra.Serializers.IDataSerializer serializer)");
                codeBuilder.AppendLine($"return serializer.DeserializeSingle<{simpleTypeName}>(data);");
                codeBuilder.EndMethod();
            }
        }
        
        private void GenerateSingleSerializerMethods(CodeBuilder codeBuilder, DataModelInfo model, string simpleTypeName)
        {
            var format = CodeBuilder.GetDataFormat(model.Format);

            // Deserialize method
            codeBuilder.BeginMethod($"public static {simpleTypeName} DeserializeSingle(string data, global::Datra.Serializers.IDataSerializer serializer)");

            switch (format)
            {
                case "Json":
                    var jsonBuilder = new JsonSerializerBuilder();
                    jsonBuilder.GenerateSingleDeserializer(codeBuilder, model, simpleTypeName);
                    break;
                case "Yaml":
                    var yamlBuilder = new YamlSerializerBuilder();
                    yamlBuilder.GenerateSingleDeserializer(codeBuilder, model, simpleTypeName);
                    break;
                default:
                    codeBuilder.AppendLine($"return serializer.DeserializeSingle<{simpleTypeName}>(data);");
                    break;
            }

            codeBuilder.EndMethod();
            codeBuilder.AddBlankLine();

            // Serialize method
            codeBuilder.BeginMethod($"public static string SerializeSingle({simpleTypeName} data, global::Datra.Serializers.IDataSerializer serializer)");

            switch (format)
            {
                case "Json":
                    var jsonBuilder2 = new JsonSerializerBuilder();
                    jsonBuilder2.GenerateSingleSerializer(codeBuilder, model, simpleTypeName);
                    break;
                case "Yaml":
                    var yamlBuilder2 = new YamlSerializerBuilder();
                    yamlBuilder2.GenerateSingleSerializer(codeBuilder, model, simpleTypeName);
                    break;
                default:
                    codeBuilder.AppendLine($"return serializer.SerializeSingle<{simpleTypeName}>(data);");
                    break;
            }

            codeBuilder.EndMethod();

            // Generate YAML-specific methods without serializer parameter
            var isYamlFormat = CodeBuilder.IsYamlFormat(model.Format, model.FilePath);
            if (isYamlFormat)
            {
                codeBuilder.AddBlankLine();

                // YAML Deserialize method without serializer
                codeBuilder.BeginMethod($"public static {simpleTypeName} DeserializeYaml(string data)");
                var yamlBuilder3 = new YamlSerializerBuilder();
                yamlBuilder3.GenerateSingleDeserializer(codeBuilder, model, simpleTypeName);
                codeBuilder.EndMethod();

                codeBuilder.AddBlankLine();

                // YAML Serialize method without serializer
                codeBuilder.BeginMethod($"public static string SerializeYaml({simpleTypeName} data)");
                var yamlBuilder4 = new YamlSerializerBuilder();
                yamlBuilder4.GenerateSingleSerializer(codeBuilder, model, simpleTypeName);
                codeBuilder.EndMethod();
            }
        }
    }
}