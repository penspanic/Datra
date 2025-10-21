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
            codeBuilder.AppendLine("logger ??= global::Datra.Logging.NullSerializationLogger.Instance;");
            codeBuilder.AppendLine($"var result = new global::System.Collections.Generic.Dictionary<{model.KeyType}, {typeName}>();");
            codeBuilder.AppendLine("var errorCount = 0;");
            codeBuilder.AppendLine("var successCount = 0;");
            codeBuilder.AppendLine("var lineNumber = 0;");
            codeBuilder.AddBlankLine();
            codeBuilder.AppendLine($"logger.LogDeserializationStart(\"{model.FilePath}\", \"CSV\");");
            codeBuilder.AppendLine("using (var reader = new global::System.IO.StringReader(data))");
            codeBuilder.BeginBlock();

            codeBuilder.AppendLine("var headerLine = reader.ReadLine();");
            codeBuilder.AppendLine("if (headerLine == null) return result;");
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("var headers = global::Datra.Helpers.CsvParsingHelper.ParseCsvLine(headerLine, config.CsvFieldDelimiter);");

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
            codeBuilder.AppendLine("lineNumber++;");
            codeBuilder.AppendLine("string currentRecordId = null;");
            codeBuilder.AppendLine("try");
            codeBuilder.BeginBlock();

            codeBuilder.AppendLine("var values = global::Datra.Helpers.CsvParsingHelper.ParseCsvLine(line, config.CsvFieldDelimiter);");
            codeBuilder.AppendLine("if (values.Length != headers.Length)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var context = new global::Datra.Interfaces.SerializationErrorContext");
            codeBuilder.AppendLine("{");
            codeBuilder.AppendLine($"    FileName = \"{model.FilePath}\",");
            codeBuilder.AppendLine("    Format = \"CSV\",");
            codeBuilder.AppendLine("    LineNumber = lineNumber + 1,");
            codeBuilder.AppendLine("    Message = $\"Column count mismatch. Expected {headers.Length}, got {values.Length}\"");
            codeBuilder.AppendLine("};");
            codeBuilder.AppendLine("logger.LogValidationError(context);");
            codeBuilder.AppendLine("errorCount++;");
            codeBuilder.AppendLine("continue;");
            codeBuilder.EndBlock();
            codeBuilder.AddBlankLine();
            
            // File name constant for logging
            codeBuilder.AppendLine($"const string fileName = \"{model.FilePath}\";");
            codeBuilder.AddBlankLine();

            // Parse each property value (only serializable properties - excludes computed FixedLocale properties)
            foreach (var prop in model.GetSerializableProperties())
            {
                var varName = CodeBuilder.ToCamelCase(prop.Name);
                GetCsvPropertyParseCodeWithLogging(codeBuilder, prop, "values", "headerIndex", varName, "config", "logger", "lineNumber + 1", "fileName");
                if (prop.Name == "Id")
                {
                    codeBuilder.AppendLine($"currentRecordId = {varName}.ToString();");
                }
            }

            // Create object using constructor (only constructor properties)
            var constructorProps = model.GetConstructorProperties().ToList();
            codeBuilder.AppendLine($"var item = new {typeName}(");
            for (int i = 0; i < constructorProps.Count; i++)
                codeBuilder.AppendLine($"    {CodeBuilder.ToCamelCase(constructorProps[i].Name)}{(i == constructorProps.Count - 1 ? "" : ",")}");
            codeBuilder.AppendLine(");");

            // Store ALL column indices and values in CsvMetadata
            codeBuilder.AppendLine("// Store all column information for perfect serialization");
            codeBuilder.AppendLine("var tildeCounter = 0;");
            codeBuilder.AppendLine("for (int i = 0; i < headers.Length; i++)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var header = headers[i];");
            codeBuilder.AppendLine("var cellValue = i < values.Length ? values[i] : string.Empty;");
            codeBuilder.AppendLine("var metadataKey = header;");
            codeBuilder.AppendLine("var columnName = header;");
            codeBuilder.AddBlankLine();
            codeBuilder.AppendLine("// Handle '~' columns by numbering them");
            codeBuilder.AppendLine("if (header == \"~\")");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("tildeCounter++;");
            codeBuilder.AppendLine("columnName = $\"~{tildeCounter}\";");
            codeBuilder.AppendLine("metadataKey = columnName;");
            codeBuilder.EndBlock();
            codeBuilder.AppendLine("else if (item.CsvMetadata.ContainsKey(header))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// Handle other duplicate column names");
            codeBuilder.AppendLine("metadataKey = $\"{header}_{i}\";");
            codeBuilder.EndBlock();
            codeBuilder.AddBlankLine();
            codeBuilder.AppendLine("if (header.StartsWith(\"~\"))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// Metadata column - store the value");
            codeBuilder.AppendLine("item.CsvMetadata[metadataKey] = (i, columnName, cellValue);");
            codeBuilder.EndBlock();
            codeBuilder.AppendLine("else");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// Regular column - store just the index and column name (value comes from property)");
            codeBuilder.AppendLine("item.CsvMetadata[metadataKey] = (i, columnName, null);");
            codeBuilder.EndBlock();
            codeBuilder.EndBlock();

            codeBuilder.AppendLine("result[item.Id] = item;");
            codeBuilder.AppendLine("successCount++;");

            codeBuilder.EndBlock(); // try block
            codeBuilder.AppendLine("catch (global::System.Exception ex)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var context = new global::Datra.Interfaces.SerializationErrorContext");
            codeBuilder.AppendLine("{");
            codeBuilder.AppendLine($"    FileName = \"{model.FilePath}\",");
            codeBuilder.AppendLine("    Format = \"CSV\",");
            codeBuilder.AppendLine("    LineNumber = lineNumber + 1,");
            codeBuilder.AppendLine("    RecordId = currentRecordId,");
            codeBuilder.AppendLine("    Message = $\"Failed to parse record: {ex.Message}\"");
            codeBuilder.AppendLine("};");
            codeBuilder.AppendLine("logger.LogParsingError(context, ex);");
            codeBuilder.AppendLine("errorCount++;");
            codeBuilder.EndBlock(); // catch block

            codeBuilder.EndBlock(); // while loop
            codeBuilder.EndBlock(); // using block

            codeBuilder.AppendLine($"logger.LogDeserializationComplete(\"{model.FilePath}\", successCount, errorCount);");
            codeBuilder.AppendLine("return result;");
        }
        
        public void GenerateTableSerializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("config ??= global::Datra.Configuration.DatraConfigurationValue.CreateDefault();");
            codeBuilder.AppendLine("logger ??= global::Datra.Logging.NullSerializationLogger.Instance;");
            codeBuilder.AppendLine("var csv = new global::System.Text.StringBuilder();");
            codeBuilder.AppendLine($"logger.LogSerializationStart(\"{model.FilePath}\", \"CSV\");");
            codeBuilder.AddBlankLine();

            // Build complete column list from first item's CsvMetadata
            codeBuilder.AppendLine("if (table.Count > 0)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var firstItem = table.Values.First();");
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("// Build column list based on class property order with metadata preservation");
            codeBuilder.AppendLine("var columnList = new global::System.Collections.Generic.List<string>();");
            codeBuilder.AppendLine("var processedMetadata = new global::System.Collections.Generic.HashSet<string>();");
            codeBuilder.AddBlankLine();

            // Check if we have metadata to preserve
            codeBuilder.AppendLine("if (firstItem.CsvMetadata != null && firstItem.CsvMetadata.Count > 0)");
            codeBuilder.BeginBlock();

            codeBuilder.AppendLine("// Use the original column order from CsvMetadata");
            codeBuilder.AppendLine("// This preserves the exact order and metadata positions");
            codeBuilder.AppendLine("var originalColumns = firstItem.CsvMetadata.OrderBy(kvp => kvp.Value.columnIndex).ToList();");

            codeBuilder.AppendLine("// Get class properties for finding new ones (only serializable properties)");
            codeBuilder.AppendLine("var classProperties = new global::System.Collections.Generic.HashSet<string>();");
            foreach (var prop in model.GetSerializableProperties())
            {
                codeBuilder.AppendLine($"classProperties.Add(\"{prop.Name}\");");
            }

            codeBuilder.AppendLine("// First, add all original columns in their exact order");
            codeBuilder.AppendLine("foreach (var col in originalColumns)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("columnList.Add(col.Value.columnName);");
            codeBuilder.EndBlock();

            codeBuilder.AppendLine("// Then, add any new properties that weren't in the original CSV");
            codeBuilder.AppendLine("// Add them in class definition order");

            // Create a list of properties to add (only serializable properties)
            codeBuilder.AppendLine("var propertiesToAdd = new global::System.Collections.Generic.List<string>();");
            foreach (var prop in model.GetSerializableProperties())
            {
                codeBuilder.AppendLine($"if (!columnList.Contains(\"{prop.Name}\"))");
                codeBuilder.BeginBlock();
                codeBuilder.AppendLine($"propertiesToAdd.Add(\"{prop.Name}\");");
                codeBuilder.EndBlock();
            }

            codeBuilder.AppendLine("// Build property order map for efficient insertion");
            codeBuilder.AppendLine("var propOrder = new global::System.Collections.Generic.Dictionary<string, int>();");
            for (int i = 0; i < model.Properties.Count; i++)
            {
                codeBuilder.AppendLine($"propOrder[\"{model.Properties[i].Name}\"] = {i};");
            }
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("// Add new properties in class order, finding the best position for each");
            codeBuilder.AppendLine("foreach (var propName in propertiesToAdd)");
            codeBuilder.BeginBlock();

            codeBuilder.AppendLine("var insertIndex = 0;");
            codeBuilder.AppendLine("var maxFoundOrder = -1;");
            codeBuilder.AppendLine("var targetOrder = propOrder[propName];");
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("// Find the rightmost existing property that comes before this one in class order");
            codeBuilder.AppendLine("for (int i = 0; i < columnList.Count; i++)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var colName = columnList[i];");
            codeBuilder.AppendLine("if (propOrder.TryGetValue(colName, out var order) && order < targetOrder && order > maxFoundOrder)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("maxFoundOrder = order;");
            codeBuilder.AppendLine("insertIndex = i + 1;");
            codeBuilder.AppendLine("// Skip any metadata columns that follow");
            codeBuilder.AppendLine("while (insertIndex < columnList.Count && columnList[insertIndex].StartsWith(\"~\"))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("insertIndex++;");
            codeBuilder.EndBlock();
            codeBuilder.EndBlock();
            codeBuilder.EndBlock();
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("columnList.Insert(insertIndex, propName);");
            codeBuilder.EndBlock(); // foreach propertiesToAdd

            codeBuilder.EndBlock(); // if CsvMetadata populated
            codeBuilder.AppendLine("else");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// No metadata - just use class properties in order (only serializable properties)");
            foreach (var prop in model.GetSerializableProperties())
            {
                codeBuilder.AppendLine($"columnList.Add(\"{prop.Name}\");");
            }
            codeBuilder.EndBlock();
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("// Write header");
            codeBuilder.AppendLine("csv.AppendLine(global::Datra.Helpers.CsvParsingHelper.JoinCsvFields(columnList.ToArray(), config.CsvFieldDelimiter));");
            codeBuilder.AddBlankLine();

            codeBuilder.AppendLine("// Write data rows");
            codeBuilder.AppendLine("foreach (var item in table.Values)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var values = new string[columnList.Count];");
            codeBuilder.AppendLine("for (int i = 0; i < columnList.Count; i++)");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("var columnName = columnList[i];");
            codeBuilder.AppendLine("if (columnName.StartsWith(\"~\"))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// Metadata column - find the stored value");
            codeBuilder.AppendLine("var metaEntry = item.CsvMetadata.FirstOrDefault(kvp => kvp.Value.columnName == columnName);");
            codeBuilder.AppendLine("values[i] = metaEntry.Value.value ?? string.Empty;");
            codeBuilder.EndBlock();
            codeBuilder.AppendLine("else");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine("// Regular property column - get property value (only serializable properties)");
            codeBuilder.AppendLine("switch (columnName)");
            codeBuilder.BeginBlock();
            foreach (var prop in model.GetSerializableProperties())
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
            codeBuilder.AppendLine("csv.AppendLine(global::Datra.Helpers.CsvParsingHelper.JoinCsvFields(values, config.CsvFieldDelimiter));");
            codeBuilder.EndBlock(); // foreach item
            codeBuilder.EndBlock(); // if table.Count > 0

            codeBuilder.AppendLine($"logger.LogSerializationComplete(\"{model.FilePath}\", table.Count);");
            codeBuilder.AppendLine("return csv.ToString();");
        }
        
        private void GetCsvPropertyParseCodeWithLogging(CodeBuilder codeBuilder, PropertyInfo prop, string valuesVar, string headerIndexVar, string varName, string configVar, string loggerVar, string lineNumberVar, string fileName)
        {
            var getValueCode = $"{headerIndexVar}.TryGetValue(\"{prop.Name}\", out var {varName}Idx) && {varName}Idx < {valuesVar}.Length ? {valuesVar}[{varName}Idx] : \"\"";

            // Get the raw value
            codeBuilder.AppendLine($"var {varName}Raw = {getValueCode};");

            // Generate parse code using ParsingHelper
            var parseCode = GetParseCodeWithHelper(prop, $"{varName}Raw", varName, configVar, loggerVar, lineNumberVar, fileName);
            codeBuilder.AppendLine($"var {varName} = {parseCode};");
            codeBuilder.AddBlankLine(); // Add blank line between properties for readability
        }

        private string GetParseCodeWithHelper(PropertyInfo prop, string rawValueVar, string varName, string configVar, string loggerVar, string lineNumberVar, string fileName)
        {
            // Handle array types
            if (prop.IsArray)
            {
                return GetArrayParseCodeWithHelper(prop, rawValueVar, varName, configVar, loggerVar, lineNumberVar, fileName);
            }

            // Handle DataRef types
            if (prop.IsDataRef)
            {
                if (prop.DataRefKeyType == "string")
                {
                    return $"new {prop.Type} {{ Value = {rawValueVar} }}";
                }
                else if (prop.DataRefKeyType == "int")
                {
                    return $"new {prop.Type} {{ Value = global::Datra.Helpers.ParsingHelper.ParseInt({rawValueVar}, 0, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\") }}";
                }
            }

            // Handle enums using enhanced metadata
            if (prop.IsEnum)
            {
                // Use ParsingHelper for enum parsing
                return $"global::Datra.Helpers.ParsingHelper.ParseEnum<{prop.Type}>({rawValueVar}, default({prop.Type}), {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
            }

            // Handle basic types using CleanTypeName to avoid issues with fully qualified names
            var cleanType = prop.CleanTypeName ?? prop.Type;
            switch (cleanType)
            {
                case "string":
                case "System.String":
                    return rawValueVar;
                case "int":
                case "System.Int32":
                    return $"global::Datra.Helpers.ParsingHelper.ParseInt({rawValueVar}, 0, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "float":
                case "System.Single":
                    return $"global::Datra.Helpers.ParsingHelper.ParseFloat({rawValueVar}, 0f, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "double":
                case "System.Double":
                    return $"global::Datra.Helpers.ParsingHelper.ParseDouble({rawValueVar}, 0.0, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "bool":
                case "System.Boolean":
                    return $"global::Datra.Helpers.ParsingHelper.ParseBool({rawValueVar}, false, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "long":
                case "System.Int64":
                    return $"global::Datra.Helpers.ParsingHelper.ParseLong({rawValueVar}, 0L, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "short":
                case "System.Int16":
                    return $"global::Datra.Helpers.ParsingHelper.ParseShort({rawValueVar}, (short)0, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "byte":
                case "System.Byte":
                    return $"global::Datra.Helpers.ParsingHelper.ParseByte({rawValueVar}, (byte)0, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "decimal":
                case "System.Decimal":
                    return $"global::Datra.Helpers.ParsingHelper.ParseDecimal({rawValueVar}, 0m, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "uint":
                case "System.UInt32":
                    return $"global::Datra.Helpers.ParsingHelper.ParseUInt({rawValueVar}, 0u, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "ulong":
                case "System.UInt64":
                    return $"global::Datra.Helpers.ParsingHelper.ParseULong({rawValueVar}, 0ul, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "ushort":
                case "System.UInt16":
                    return $"global::Datra.Helpers.ParsingHelper.ParseUShort({rawValueVar}, (ushort)0, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "sbyte":
                case "System.SByte":
                    return $"global::Datra.Helpers.ParsingHelper.ParseSByte({rawValueVar}, (sbyte)0, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                case "char":
                case "System.Char":
                    return $"global::Datra.Helpers.ParsingHelper.ParseChar({rawValueVar}, '\\0', {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}\")";
                default:
                    // Fallback for unknown types
                    return $"default({prop.Type})";
            }
        }

        private string GetCsvPropertyParseCode(PropertyInfo prop, string rawValueVar, string varName, string configVar)
        {
            // Handle array types
            if (prop.IsArray)
            {
                return GetArrayParseCode(prop, rawValueVar, varName, configVar);
            }

            // Handle DataRef types
            if (prop.IsDataRef)
            {
                if (prop.DataRefKeyType == "string")
                {
                    return $"new {prop.Type} {{ Value = {rawValueVar} }}";
                }
                else if (prop.DataRefKeyType == "int")
                {
                    return $"new {prop.Type} {{ Value = int.TryParse({rawValueVar}, out var {varName}RefVal) ? {varName}RefVal : 0 }}";
                }
            }

            // Handle enums using enhanced metadata
            if (prop.IsEnum)
            {
                // Use Type which already has global:: prefix from ToDisplayString(FullyQualifiedFormat)
                return $"global::System.Enum.TryParse<{prop.Type}>({rawValueVar}, true, out var {varName}Val) ? {varName}Val : default({prop.Type})";
            }

            // Handle basic types using CleanTypeName to avoid issues with fully qualified names
            var cleanType = prop.CleanTypeName ?? prop.Type;
            switch (cleanType)
            {
                case "string":
                case "System.String":
                    return rawValueVar;
                case "int":
                case "System.Int32":
                    return $"int.TryParse({rawValueVar}, out var {varName}Val) ? {varName}Val : 0";
                case "float":
                case "System.Single":
                    return $"float.TryParse({rawValueVar}, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var {varName}Val) ? {varName}Val : 0f";
                case "double":
                case "System.Double":
                    return $"double.TryParse({rawValueVar}, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var {varName}Val) ? {varName}Val : 0.0";
                case "bool":
                case "System.Boolean":
                    return $"bool.TryParse({rawValueVar}, out var {varName}Val) ? {varName}Val : false";
                default:
                    // Fallback for unknown types
                    return $"default({prop.Type})";
            }
        }
        
        private string GetArrayParseCodeWithHelper(PropertyInfo prop, string getValueCode, string varName, string configVar, string loggerVar, string lineNumberVar, string fileName)
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
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select((x, idx) => new {elementType} {{ Value = global::Datra.Helpers.ParsingHelper.ParseInt(x, 0, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}[\" + idx + \"]\") }}).ToArray()";
                }
            }

            // Handle enum arrays using enhanced metadata
            if (prop.ElementIsEnum)
            {
                // Use ParsingHelper for enum parsing in arrays
                return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select((x, idx) => global::Datra.Helpers.ParsingHelper.ParseEnum<{elementType}>(x, default({elementType}), {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}[\" + idx + \"]\")).ToArray()";
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
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select((x, idx) => global::Datra.Helpers.ParsingHelper.ParseInt(x, 0, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}[\" + idx + \"]\")).ToArray()";
                case "float":
                case "System.Single":
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select((x, idx) => global::Datra.Helpers.ParsingHelper.ParseFloat(x, 0f, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}[\" + idx + \"]\")).ToArray()";
                case "double":
                case "System.Double":
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select((x, idx) => global::Datra.Helpers.ParsingHelper.ParseDouble(x, 0.0, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}[\" + idx + \"]\")).ToArray()";
                case "bool":
                case "System.Boolean":
                    return $"({getValueCode}).Split({arrayDelimiter}, global::System.StringSplitOptions.RemoveEmptyEntries).Select((x, idx) => global::Datra.Helpers.ParsingHelper.ParseBool(x, false, {loggerVar}, {fileName}, {lineNumberVar}, \"{prop.Name}[\" + idx + \"]\")).ToArray()";
                default:
                    // Fallback for unknown types - return empty array
                    return $"new {elementType}[0]";
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