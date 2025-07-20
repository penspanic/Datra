using System.Linq;
using Datra.Data.Generators.Builders;
using Datra.Data.Generators.Models;

namespace Datra.Data.Generators.Generators
{
    internal class CsvSerializerBuilder
    {
        public void GenerateTableDeserializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("// Custom CSV deserializer for immutable types");
            codeBuilder.AppendLine($"var result = new Dictionary<{model.KeyType}, {typeName}>();");
            codeBuilder.AppendLine("using (var reader = new StringReader(data))");
            codeBuilder.BeginBlock();
            
            codeBuilder.AppendLine("var headerLine = reader.ReadLine();");
            codeBuilder.AppendLine("if (headerLine == null) return result;");
            codeBuilder.AddBlankLine();
            
            codeBuilder.AppendLine("var headers = headerLine.Split(',');");
            
            // Generate header index mapping
            codeBuilder.AppendLine("var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);");
            codeBuilder.AppendLine("for (int i = 0; i < headers.Length; i++)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("headerIndex[headers[i]] = i;");
            codeBuilder.EndBlock();
            codeBuilder.AddBlankLine();
            
            codeBuilder.AppendLine("string line;");
            codeBuilder.AppendLine("while ((line = reader.ReadLine()) != null)");
            codeBuilder.BeginBlock();
            
            codeBuilder.AppendLine("var values = line.Split(',');");
            codeBuilder.AppendLine("if (values.Length != headers.Length) continue;");
            codeBuilder.AddBlankLine();
            
            // Parse each property value
            foreach (var prop in model.Properties)
            {
                var varName = CodeBuilder.ToCamelCase(prop.Name);
                var parseCode = GetCsvPropertyParseCode(prop, "values", "headerIndex", varName);
                codeBuilder.AppendLine($"var {varName} = {parseCode};");
            }
            
            // Create object using constructor
            codeBuilder.AppendLine($"var item = new {typeName}(");
            for (int i = 0; i < model.Properties.Count; i++)
                codeBuilder.AppendLine($"    {CodeBuilder.ToCamelCase(model.Properties[i].Name)}{(i == model.Properties.Count - 1 ? "" : ",")}");
            codeBuilder.AppendLine(");");
            
            codeBuilder.AppendLine("result[item.Id] = item;");
            
            codeBuilder.EndBlock(); // while loop
            codeBuilder.EndBlock(); // using block
            
            codeBuilder.AppendLine("return result;");
        }
        
        public void GenerateTableSerializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("var csv = new StringBuilder();");
            codeBuilder.AppendLine("// CSV header");
            
            var headers = string.Join(",", model.Properties.Select(p => p.Name));
            codeBuilder.AppendLine($"csv.AppendLine(\"{headers}\");");
            codeBuilder.AddBlankLine();
            
            codeBuilder.AppendLine("foreach (var item in table.Values)");
            codeBuilder.BeginBlock();
            
            codeBuilder.AppendLine("var values = new string[]");
            codeBuilder.BeginBlock();
            
            foreach (var prop in model.Properties)
            {
                var serializeCode = GetCsvSerializeCode(prop, "item");
                codeBuilder.AppendLine($"{serializeCode},");
            }
            
            codeBuilder.EndBlock();
            codeBuilder.AppendLine(";");
            codeBuilder.AppendLine("csv.AppendLine(string.Join(\",\", values));");
            
            codeBuilder.EndBlock();
            
            codeBuilder.AppendLine("return csv.ToString();");
        }
        
        private string GetCsvPropertyParseCode(PropertyInfo prop, string valuesVar, string headerIndexVar, string varName)
        {
            var getValueCode = $"{headerIndexVar}.TryGetValue(\"{prop.Name}\", out var {varName}Idx) && {varName}Idx < {valuesVar}.Length ? {valuesVar}[{varName}Idx] : \"\"";
            
            switch (prop.Type)
            {
                case "string":
                    return getValueCode;
                case "int":
                case "System.Int32":
                    return $"int.TryParse({getValueCode}, out var {varName}Val) ? {varName}Val : 0";
                case "float":
                case "System.Single":
                    return $"float.TryParse({getValueCode}, NumberStyles.Float, CultureInfo.InvariantCulture, out var {varName}Val) ? {varName}Val : 0f";
                case "double":
                case "System.Double":
                    return $"double.TryParse({getValueCode}, NumberStyles.Float, CultureInfo.InvariantCulture, out var {varName}Val) ? {varName}Val : 0.0";
                case "bool":
                case "System.Boolean":
                    return $"bool.TryParse({getValueCode}, out var {varName}Val) ? {varName}Val : false";
                default:
                    // Enum handling
                    if (prop.Type.Contains("."))
                    {
                        var simpleType = prop.Type.Split('.').Last();
                        return $"Enum.TryParse<{simpleType}>({getValueCode}, true, out var {varName}Val) ? {varName}Val : default({simpleType})";
                    }
                    return $"default({prop.Type})";
            }
        }
        
        private string GetCsvSerializeCode(PropertyInfo prop, string itemVar)
        {
            switch (prop.Type)
            {
                case "string":
                    return $"{itemVar}.{prop.Name} ?? string.Empty";
                case "int":
                case "System.Int32":
                case "float":
                case "System.Single":
                case "double":
                case "System.Double":
                    return $"{itemVar}.{prop.Name}.ToString(CultureInfo.InvariantCulture)";
                case "bool":
                case "System.Boolean":
                    return $"{itemVar}.{prop.Name}.ToString()";
                default:
                    return $"{itemVar}.{prop.Name}?.ToString() ?? string.Empty";
            }
        }
    }
}