using System.Linq;
using Datra.Generators.Builders;
using Datra.Generators.Models;

namespace Datra.Generators.Generators
{
    internal class CsvSerializerBuilder
    {
        private const string DefaultArrayDelimiter = "|";
        public void GenerateTableDeserializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("// Custom CSV deserializer for immutable types");
            codeBuilder.AppendLine("config ??= Datra.Configuration.DatraConfiguration.CreateDefault();");
            codeBuilder.AppendLine($"var result = new Dictionary<{model.KeyType}, {typeName}>();");
            codeBuilder.AppendLine("using (var reader = new StringReader(data))");
            codeBuilder.BeginBlock();
            
            codeBuilder.AppendLine("var headerLine = reader.ReadLine();");
            codeBuilder.AppendLine("if (headerLine == null) return result;");
            codeBuilder.AddBlankLine();
            
            codeBuilder.AppendLine("var headers = headerLine.Split(config.CsvFieldDelimiter);");
            
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
            
            codeBuilder.AppendLine("var values = line.Split(config.CsvFieldDelimiter);");
            codeBuilder.AppendLine("if (values.Length != headers.Length) continue;");
            codeBuilder.AddBlankLine();
            
            // Parse each property value
            foreach (var prop in model.Properties)
            {
                var varName = CodeBuilder.ToCamelCase(prop.Name);
                var parseCode = GetCsvPropertyParseCode(prop, "values", "headerIndex", varName, "config");
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
            codeBuilder.AppendLine("config ??= Datra.Configuration.DatraConfiguration.CreateDefault();");
            codeBuilder.AppendLine("var csv = new StringBuilder();");
            codeBuilder.AppendLine("// CSV header");
            
            var headers = model.Properties.Select(p => p.Name);
            codeBuilder.AppendLine($"csv.AppendLine(string.Join(config.CsvFieldDelimiter.ToString(), new[] {{ \"{string.Join("\", \"", headers)}\" }}));");
            codeBuilder.AddBlankLine();
            
            codeBuilder.AppendLine("foreach (var item in table.Values)");
            codeBuilder.BeginBlock();
            
            codeBuilder.AppendLine("var values = new string[]");
            codeBuilder.BeginBlock();
            
            foreach (var prop in model.Properties)
            {
                var serializeCode = GetCsvSerializeCode(prop, "item", "config");
                codeBuilder.AppendLine($"{serializeCode},");
            }
            
            codeBuilder.EndBlock();
            codeBuilder.AppendLine(";");
            codeBuilder.AppendLine("csv.AppendLine(string.Join(config.CsvFieldDelimiter.ToString(), values));");
            
            codeBuilder.EndBlock();
            
            codeBuilder.AppendLine("return csv.ToString();");
        }
        
        private string GetCsvPropertyParseCode(PropertyInfo prop, string valuesVar, string headerIndexVar, string varName, string configVar)
        {
            var getValueCode = $"{headerIndexVar}.TryGetValue(\"{prop.Name}\", out var {varName}Idx) && {varName}Idx < {valuesVar}.Length ? {valuesVar}[{varName}Idx] : \"\"";
            
            // Handle array types
            if (prop.IsArray)
            {
                return GetArrayParseCode(prop, getValueCode, varName, configVar);
            }
            
            // Handle DataRef types
            if (prop.IsDataRef)
            {
                if (prop.DataRefKeyType == "string")
                {
                    return $"new {prop.Type} {{ Value = {getValueCode} }}";
                }
                else if (prop.DataRefKeyType == "int")
                {
                    return $"new {prop.Type} {{ Value = int.TryParse({getValueCode}, out var {varName}RefVal) ? {varName}RefVal : 0 }}";
                }
            }
            
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
                        return $"Enum.TryParse<{prop.Type}>({getValueCode}, true, out var {varName}Val) ? {varName}Val : default({prop.Type})";
                    }
                    return $"default({prop.Type})";
            }
        }
        
        private string GetArrayParseCode(PropertyInfo prop, string getValueCode, string varName, string configVar)
        {
            var arrayDelimiter = $"{configVar}.CsvArrayDelimiter";
            var elementType = prop.ElementType;
            
            // Handle DataRef array types
            if (prop.IsDataRef)
            {
                if (prop.DataRefKeyType == "string")
                {
                    return $"({getValueCode}).Split({arrayDelimiter}, StringSplitOptions.RemoveEmptyEntries).Select(x => new {elementType} {{ Value = x }}).ToArray()";
                }
                else if (prop.DataRefKeyType == "int")
                {
                    return $"({getValueCode}).Split({arrayDelimiter}, StringSplitOptions.RemoveEmptyEntries).Select(x => new {elementType} {{ Value = int.TryParse(x, out var val) ? val : 0 }}).ToArray()";
                }
            }
            
            // Handle basic type arrays
            switch (elementType)
            {
                case "string":
                case "System.String":
                    return $"({getValueCode}).Split({arrayDelimiter}, StringSplitOptions.RemoveEmptyEntries)";
                case "int":
                case "System.Int32":
                    return $"({getValueCode}).Split({arrayDelimiter}, StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var val) ? val : 0).ToArray()";
                case "float":
                case "System.Single":
                    return $"({getValueCode}).Split({arrayDelimiter}, StringSplitOptions.RemoveEmptyEntries).Select(x => float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var val) ? val : 0f).ToArray()";
                case "double":
                case "System.Double":
                    return $"({getValueCode}).Split({arrayDelimiter}, StringSplitOptions.RemoveEmptyEntries).Select(x => double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var val) ? val : 0.0).ToArray()";
                case "bool":
                case "System.Boolean":
                    return $"({getValueCode}).Split({arrayDelimiter}, StringSplitOptions.RemoveEmptyEntries).Select(x => bool.TryParse(x, out var val) ? val : false).ToArray()";
                default:
                    // Handle enum arrays
                    if (elementType.Contains("."))
                    {
                        return $"({getValueCode}).Split({arrayDelimiter}, StringSplitOptions.RemoveEmptyEntries).Select(x => Enum.TryParse<{elementType}>(x, true, out var val) ? val : default({elementType})).ToArray()";
                    }
                    return $"new {prop.Type}[0]"; // Empty array for unsupported types
            }
        }
        
        private string GetCsvSerializeCode(PropertyInfo prop, string itemVar, string configVar)
        {
            // Handle array types
            if (prop.IsArray)
            {
                return GetArraySerializeCode(prop, itemVar, configVar);
            }
            
            // Handle DataRef types
            if (prop.IsDataRef)
            {
                if (prop.DataRefKeyType == "string")
                {
                    return $"{itemVar}.{prop.Name}.Value ?? string.Empty";
                }
                else
                {
                    return $"{itemVar}.{prop.Name}.Value.ToString()";
                }
            }
            
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
                    // Handle enums - they don't need null check since they're value types
                    if (prop.Type.Contains(".") || !prop.Type.Contains("?"))
                    {
                        return $"{itemVar}.{prop.Name}.ToString()";
                    }
                    return $"{itemVar}.{prop.Name}?.ToString() ?? string.Empty";
            }
        }
        
        private string GetArraySerializeCode(PropertyInfo prop, string itemVar, string configVar)
        {
            var arrayDelimiter = "config.CsvArrayDelimiter.ToString()";
            var arrayVar = $"{itemVar}.{prop.Name}";
            
            // Handle DataRef array types
            if (prop.IsDataRef)
            {
                if (prop.DataRefKeyType == "string")
                {
                    return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar}.Select(x => x.Value ?? string.Empty))";
                }
                else
                {
                    return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar}.Select(x => x.Value.ToString()))";
                }
            }
            
            // Handle basic type arrays
            switch (prop.ElementType)
            {
                case "string":
                case "System.String":
                    return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar})";
                case "int":
                case "System.Int32":
                case "float":
                case "System.Single":
                case "double":
                case "System.Double":
                    return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar}.Select(x => x.ToString(CultureInfo.InvariantCulture)))";
                case "bool":
                case "System.Boolean":
                    return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar}.Select(x => x.ToString()))";
                default:
                    // Handle enum arrays - enums are value types and don't need null check
                    if (prop.ElementType.Contains("."))
                    {
                        return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar}.Select(x => x.ToString()))";
                    }
                    return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar}.Select(x => x?.ToString() ?? string.Empty))";
            }
        }
    }
}