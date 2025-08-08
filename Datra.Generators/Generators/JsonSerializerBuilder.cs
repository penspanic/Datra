using System.Linq;
using Datra.Generators.Builders;
using Datra.Generators.Models;

namespace Datra.Generators.Generators
{
    internal class JsonSerializerBuilder
    {
        public void GenerateTableDeserializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine($"return serializer.DeserializeTable<{model.KeyType}, {typeName}>(data);");
        }
        
        public void GenerateTableSerializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine($"return serializer.SerializeTable<{model.KeyType}, {typeName}>(table);");
        }
        
        public void GenerateSingleDeserializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine($"return serializer.DeserializeSingle<{typeName}>(data);");
        }
        
        public void GenerateSingleSerializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine($"return serializer.SerializeSingle<{typeName}>(data);");
        }
        
        private void GenerateJsonPropertyExtraction(CodeBuilder codeBuilder, PropertyInfo prop, string varName, string propNameLower, string elementVar)
        {
            // Handle DataRef types
            if (prop.IsDataRef)
            {
                if (prop.DataRefKeyType == "string")
                {
                    codeBuilder.AppendLine($"var {varName}Value = {elementVar}[\"{prop.Name}\"]?.ToString() ?? {elementVar}[\"{propNameLower}\"]?.ToString() ?? string.Empty;");
                    codeBuilder.AppendLine($"var {varName} = new {prop.Type} {{ Value = {varName}Value }};");
                }
                else if (prop.DataRefKeyType == "int")
                {
                    codeBuilder.AppendLine($"var {varName}Value = {elementVar}[\"{prop.Name}\"]?.Value<int>() ?? {elementVar}[\"{propNameLower}\"]?.Value<int>() ?? 0;");
                    codeBuilder.AppendLine($"var {varName} = new {prop.Type} {{ Value = {varName}Value }};");
                }
                return;
            }
            
            switch (prop.Type)
            {
                case "string":
                    codeBuilder.AppendLine($"var {varName} = {elementVar}[\"{prop.Name}\"]?.ToString() ?? {elementVar}[\"{propNameLower}\"]?.ToString() ?? string.Empty;");
                    break;
                case "int":
                case "System.Int32":
                    codeBuilder.AppendLine($"var {varName} = {elementVar}[\"{prop.Name}\"]?.Value<int>() ?? {elementVar}[\"{propNameLower}\"]?.Value<int>() ?? 0;");
                    break;
                case "float":
                case "System.Single":
                    codeBuilder.AppendLine($"var {varName} = {elementVar}[\"{prop.Name}\"]?.Value<float>() ?? {elementVar}[\"{propNameLower}\"]?.Value<float>() ?? 0f;");
                    break;
                case "double":
                case "System.Double":
                    codeBuilder.AppendLine($"var {varName} = {elementVar}[\"{prop.Name}\"]?.Value<double>() ?? {elementVar}[\"{propNameLower}\"]?.Value<double>() ?? 0.0;");
                    break;
                case "bool":
                case "System.Boolean":
                    codeBuilder.AppendLine($"var {varName} = {elementVar}[\"{prop.Name}\"]?.Value<bool>() ?? {elementVar}[\"{propNameLower}\"]?.Value<bool>() ?? false;");
                    break;
                default:
                    if (prop.Type.Contains(".") && !prop.Type.StartsWith("System."))
                    {
                        // Enum handling
                        // Use full type name
                        codeBuilder.AppendLine($"var {varName}Str = {elementVar}[\"{prop.Name}\"]?.ToString() ?? {elementVar}[\"{propNameLower}\"]?.ToString();");
                        codeBuilder.AppendLine($"var {varName} = global::System.Enum.TryParse<{prop.Type}>({varName}Str, true, out var {varName}Parsed) ? {varName}Parsed : default({prop.Type});");
                    }
                    break;
            }
        }
    }
}