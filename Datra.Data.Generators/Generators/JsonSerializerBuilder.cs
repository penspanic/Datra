using System.Linq;
using Datra.Data.Generators.Builders;
using Datra.Data.Generators.Models;

namespace Datra.Data.Generators.Generators
{
    internal class JsonSerializerBuilder
    {
        public void GenerateTableDeserializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("// For immutable types, we need to parse JSON manually");
            codeBuilder.AppendLine("var array = JArray.Parse(data);");
            codeBuilder.AppendLine($"var result = new Dictionary<{model.KeyType}, {typeName}>();");
            codeBuilder.AddBlankLine();
            
            codeBuilder.AppendLine("foreach (var element in array)");
            codeBuilder.BeginBlock();
            
            // Extract each property value
            foreach (var prop in model.Properties)
            {
                var varName = CodeBuilder.ToCamelCase(prop.Name);
                var propNameLower = CodeBuilder.ToCamelCase(prop.Name);
                
                GenerateJsonPropertyExtraction(codeBuilder, prop, varName, propNameLower, "element");
            }
            
            // Create object using constructor
            codeBuilder.AppendLine($"var item = new {typeName}(");
            var constructorParams = model.Properties.Select(p => 
                $"    {CodeBuilder.ToCamelCase(p.Name)}"
            );
            codeBuilder.AppendLine(string.Join(",\n", constructorParams));
            codeBuilder.AppendLine(");");
            codeBuilder.AppendLine("result[item.Id] = item;");
            
            codeBuilder.EndBlock();
            codeBuilder.AddBlankLine();
            
            codeBuilder.AppendLine("return result;");
        }
        
        public void GenerateTableSerializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("return JsonConvert.SerializeObject(table.Values.ToList(), Formatting.Indented);");
        }
        
        public void GenerateSingleDeserializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("// For immutable types, we need to parse JSON manually");
            codeBuilder.AppendLine("var root = JObject.Parse(data);");
            codeBuilder.AddBlankLine();
            
            foreach (var prop in model.Properties)
            {
                var varName = CodeBuilder.ToCamelCase(prop.Name);
                var propNameLower = CodeBuilder.ToCamelCase(prop.Name);
                
                GenerateJsonPropertyExtraction(codeBuilder, prop, varName, propNameLower, "root");
            }
            
            codeBuilder.AppendLine($"return new {typeName}(");
            var constructorParams = model.Properties.Select(p => 
                $"    {CodeBuilder.ToCamelCase(p.Name)}"
            );
            codeBuilder.AppendLine(string.Join(",\n", constructorParams));
            codeBuilder.AppendLine(");");
        }
        
        public void GenerateSingleSerializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("return JsonConvert.SerializeObject(data, Formatting.Indented);");
        }
        
        private void GenerateJsonPropertyExtraction(CodeBuilder codeBuilder, PropertyInfo prop, string varName, string propNameLower, string elementVar)
        {
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
                        var simpleType = prop.Type.Split('.').Last();
                        codeBuilder.AppendLine($"var {varName}Str = {elementVar}[\"{prop.Name}\"]?.ToString() ?? {elementVar}[\"{propNameLower}\"]?.ToString();");
                        codeBuilder.AppendLine($"var {varName} = Enum.TryParse<{simpleType}>({varName}Str, true, out var {varName}Parsed) ? {varName}Parsed : default({simpleType});");
                    }
                    break;
            }
        }
    }
}