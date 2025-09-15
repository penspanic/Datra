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
            codeBuilder.AppendLine("config ??= global::Datra.Configuration.DatraConfigurationValue.CreateDefault();");
            codeBuilder.AppendLine($"var result = new global::System.Collections.Generic.Dictionary<{model.KeyType}, {typeName}>();");
            codeBuilder.AppendLine("using (var reader = new global::System.IO.StringReader(data))");
            codeBuilder.BeginBlock();

            codeBuilder.AppendLine("var headerLine = reader.ReadLine();");
            codeBuilder.AppendLine("if (headerLine == null) return result;");
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("var headers = headerLine.Split(config.CsvFieldDelimiter);");

            // Generate header index mapping for ALL columns (both regular and metadata)
            codeBuilder.AppendLine("var headerIndex = new global::System.Collections.Generic.Dictionary<string, int>(global::System.StringComparer.OrdinalIgnoreCase);");
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

            // Store ALL column indices and values in CsvMetadata
            codeBuilder.AppendLine("// Store all column information for perfect serialization");
            codeBuilder.AppendLine("for (int i = 0; i < headers.Length; i++)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var header = headers[i];");
            codeBuilder.AppendLine("var cellValue = i < values.Length ? values[i] : string.Empty;");
            codeBuilder.AppendLine("// For duplicate column names (especially '~'), append index to make unique key");
            codeBuilder.AppendLine("var metadataKey = header;");
            codeBuilder.AppendLine("if (header == \"~\" || item.CsvMetadata.ContainsKey(header))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("metadataKey = $\"{header}_{i}\";");
            codeBuilder.EndBlock();
            codeBuilder.AppendLine("if (header.StartsWith(\"~\"))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// Metadata column - store the value and original column name");
            codeBuilder.AppendLine("item.CsvMetadata[metadataKey] = (i, header, cellValue);");
            codeBuilder.EndBlock();
            codeBuilder.AppendLine("else");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// Regular column - store just the index and column name (value comes from property)");
            codeBuilder.AppendLine("item.CsvMetadata[metadataKey] = (i, header, null);");
            codeBuilder.EndBlock();
            codeBuilder.EndBlock();

            codeBuilder.AppendLine("result[item.Id] = item;");
            
            codeBuilder.EndBlock(); // while loop
            codeBuilder.EndBlock(); // using block
            
            codeBuilder.AppendLine("return result;");
        }
        
        public void GenerateTableSerializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("config ??= global::Datra.Configuration.DatraConfigurationValue.CreateDefault();");
            codeBuilder.AppendLine("var csv = new global::System.Text.StringBuilder();");
            codeBuilder.AddBlankLine();

            // Build complete column list from first item's CsvMetadata
            codeBuilder.AppendLine("if (table.Count > 0)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var firstItem = table.Values.First();");
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("// Check if CsvMetadata is populated (from deserialization)");
            codeBuilder.AppendLine("if (firstItem.CsvMetadata != null && firstItem.CsvMetadata.Count > 0)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// Use original column order from CsvMetadata");
            codeBuilder.AppendLine("var columnList = firstItem.CsvMetadata.OrderBy(kvp => kvp.Value.columnIndex).Select(kvp => kvp.Value.columnName).ToList();");
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("// Write header");
            codeBuilder.AppendLine("csv.AppendLine(string.Join(config.CsvFieldDelimiter.ToString(), columnList));");
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("// Write data rows");
            codeBuilder.AppendLine("foreach (var item in table.Values)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var values = new string[columnList.Count];");
            codeBuilder.AppendLine("for (int i = 0; i < columnList.Count; i++)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var columnName = columnList[i];");
            codeBuilder.AppendLine("// Find the metadata entry for this column at this index");
            codeBuilder.AppendLine("var metaEntry = item.CsvMetadata.FirstOrDefault(kvp => kvp.Value.columnIndex == i);");
            codeBuilder.AppendLine("if (columnName.StartsWith(\"~\"))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// Metadata column - get stored value from the entry at this index");
            codeBuilder.AppendLine("if (metaEntry.Key != null)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("values[i] = metaEntry.Value.value ?? string.Empty;");
            codeBuilder.EndBlock();
            codeBuilder.AppendLine("else");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("values[i] = string.Empty;");
            codeBuilder.EndBlock();
            codeBuilder.EndBlock();
            codeBuilder.AppendLine("else");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// Regular property column - get property value");
            codeBuilder.AppendLine("switch (columnName)");
            codeBuilder.BeginBlock();
            foreach (var prop in model.Properties)
            {
                var serializeCode = GetCsvSerializeCode(prop, "item", "config");
                codeBuilder.AppendLine($"case \"{prop.Name}\":");
                codeBuilder.AppendLine($"    values[i] = {serializeCode};");
                codeBuilder.AppendLine("    break;");
            }
            codeBuilder.AppendLine("default:");
            codeBuilder.AppendLine("    values[i] = string.Empty;");
            codeBuilder.AppendLine("    break;");
            codeBuilder.EndBlock(); // switch
            codeBuilder.EndBlock(); // else
            codeBuilder.EndBlock(); // for loop
            codeBuilder.AppendLine("csv.AppendLine(string.Join(config.CsvFieldDelimiter.ToString(), values));");
            codeBuilder.EndBlock(); // foreach item
            codeBuilder.EndBlock(); // if CsvMetadata populated
            codeBuilder.AppendLine("else");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// CsvMetadata not populated - use default property-based serialization");

            // Collect all unique metadata columns from all items
            codeBuilder.AppendLine("var allMetadataColumns = new global::System.Collections.Generic.SortedDictionary<int, string>();");
            codeBuilder.AppendLine("foreach (var item in table.Values)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("foreach (var meta in item.CsvMetadata)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("if (!allMetadataColumns.ContainsKey(meta.Value.columnIndex))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("allMetadataColumns[meta.Value.columnIndex] = meta.Value.columnName;");
            codeBuilder.EndBlock();
            codeBuilder.EndBlock();
            codeBuilder.EndBlock();
            codeBuilder.AddBlankLine();

            // Build header line with property columns and metadata columns in correct positions
            codeBuilder.AppendLine("var headerList = new global::System.Collections.Generic.List<(int index, string name)>();");
            var propIndex = 0;
            foreach (var prop in model.Properties)
            {
                codeBuilder.AppendLine($"headerList.Add(({propIndex}, \"{prop.Name}\"));");
                propIndex++;
            }
            codeBuilder.AppendLine("foreach (var metaCol in allMetadataColumns)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("headerList.Add((metaCol.Key, metaCol.Value));");
            codeBuilder.EndBlock();
            codeBuilder.AppendLine("headerList.Sort((a, b) => a.index.CompareTo(b.index));");
            codeBuilder.AppendLine("csv.AppendLine(string.Join(config.CsvFieldDelimiter.ToString(), headerList.Select(h => h.name)));");
            codeBuilder.AddBlankLine();
            
            codeBuilder.AppendLine("foreach (var item in table.Values)");
            codeBuilder.BeginBlock();

            // Build value array with correct size including metadata columns
            codeBuilder.AppendLine("var valueDict = new global::System.Collections.Generic.Dictionary<int, string>();");

            // Add property values
            propIndex = 0;
            foreach (var prop in model.Properties)
            {
                var serializeCode = GetCsvSerializeCode(prop, "item", "config");
                codeBuilder.AppendLine($"valueDict[{propIndex}] = {serializeCode};");
                propIndex++;
            }

            // Add metadata values
            codeBuilder.AppendLine("foreach (var meta in item.CsvMetadata)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("if (meta.Value.value != null)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("valueDict[meta.Value.columnIndex] = meta.Value.value;");
            codeBuilder.EndBlock();
            codeBuilder.EndBlock();

            // Create final value list with all columns in correct order
            codeBuilder.AppendLine("var valueList = new global::System.Collections.Generic.List<string>();");
            codeBuilder.AppendLine("foreach (var header in headerList.OrderBy(h => h.index))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("valueList.Add(valueDict.ContainsKey(header.index) ? valueDict[header.index] : string.Empty);");
            codeBuilder.EndBlock();

            codeBuilder.AppendLine("csv.AppendLine(string.Join(config.CsvFieldDelimiter.ToString(), valueList));");

            codeBuilder.EndBlock(); // foreach
            codeBuilder.EndBlock(); // else (CsvMetadata not populated)
            codeBuilder.EndBlock(); // if table.Count > 0

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
            
            // Handle enums using enhanced metadata
            if (prop.IsEnum)
            {
                // Use Type which already has global:: prefix from ToDisplayString(FullyQualifiedFormat)
                return $"global::System.Enum.TryParse<{prop.Type}>({getValueCode}, true, out var {varName}Val) ? {varName}Val : default({prop.Type})";
            }

            // Handle basic types using CleanTypeName to avoid issues with fully qualified names
            var cleanType = prop.CleanTypeName ?? prop.Type;
            switch (cleanType)
            {
                case "string":
                case "System.String":
                    return getValueCode;
                case "int":
                case "System.Int32":
                    return $"int.TryParse({getValueCode}, out var {varName}Val) ? {varName}Val : 0";
                case "float":
                case "System.Single":
                    return $"float.TryParse({getValueCode}, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var {varName}Val) ? {varName}Val : 0f";
                case "double":
                case "System.Double":
                    return $"double.TryParse({getValueCode}, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var {varName}Val) ? {varName}Val : 0.0";
                case "bool":
                case "System.Boolean":
                    return $"bool.TryParse({getValueCode}, out var {varName}Val) ? {varName}Val : false";
                default:
                    // Fallback for unknown types
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
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select(x => new {elementType} {{ Value = x }}).ToArray()";
                }
                else if (prop.DataRefKeyType == "int")
                {
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select(x => new {elementType} {{ Value = int.TryParse(x, out var val) ? val : 0 }}).ToArray()";
                }
            }

            // Handle enum arrays using enhanced metadata
            if (prop.ElementIsEnum)
            {
                // Use ElementType which already has global:: prefix from ToDisplayString(FullyQualifiedFormat)
                return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select(x => global::System.Enum.TryParse<{elementType}>(x, true, out var val) ? val : default({elementType})).ToArray()";
            }

            // Handle basic type arrays
            // Use CleanElementType to avoid issues with fully qualified names
            var cleanType = prop.CleanElementType ?? elementType;
            switch (cleanType)
            {
                case "string":
                case "System.String":
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries)";
                case "int":
                case "System.Int32":
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var val) ? val : 0).ToArray()";
                case "float":
                case "System.Single":
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select(x => float.TryParse(x, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0f).ToArray()";
                case "double":
                case "System.Double":
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select(x => double.TryParse(x, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0.0).ToArray()";
                case "bool":
                case "System.Boolean":
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select(x => bool.TryParse(x, out var val) ? val : false).ToArray()";
                default:
                    // Fallback for unknown types - return empty array
                    return $"new {elementType}[0]";
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
                    return $"{itemVar}.{prop.Name}.ToString(global::System.Globalization.CultureInfo.InvariantCulture)";
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

            // Handle enum arrays using enhanced metadata
            if (prop.ElementIsEnum)
            {
                // Enums are value types and don't need null check on individual elements
                return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar}.Select(x => x.ToString()))";
            }

            // Handle basic type arrays using CleanElementType
            var cleanType = prop.CleanElementType ?? prop.ElementType;
            switch (cleanType)
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
                    return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar}.Select(x => x.ToString(global::System.Globalization.CultureInfo.InvariantCulture)))";
                case "bool":
                case "System.Boolean":
                    return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar}.Select(x => x.ToString()))";
                default:
                    // Fallback for unknown types
                    return $"{arrayVar} == null ? string.Empty : string.Join({arrayDelimiter}, {arrayVar}.Select(x => x?.ToString() ?? string.Empty))";
            }
        }
    }
}