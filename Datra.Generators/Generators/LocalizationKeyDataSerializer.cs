using System.Collections.Generic;
using Datra.Generators.Builders;

namespace Datra.Generators.Generators
{
    internal static class LocalizationKeyDataSerializer
    {
        public static string GenerateSerializer()
        {
            var builder = new CodeBuilder();
            
            // Add using statements
            builder.AddUsings(new[]
            {
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "Datra.Models",
                "Datra.Configuration"
            });
            
            builder.BeginNamespace("Datra.Generated");
            
            // Begin class
            builder.BeginClass("LocalizationKeyDataSerializer", "public static");
            
            // Deserialize CSV method
            builder.BeginMethod("public static Dictionary<string, LocalizationKeyData> DeserializeCsv(string csvData, DatraConfigurationValue config, global::Datra.Interfaces.ISerializationLogger logger = null)");
            builder.AppendLine("var result = new Dictionary<string, Datra.Models.LocalizationKeyData>();");
            builder.AppendLine("var lines = csvData.Split(new[] { '\\r', '\\n' }, StringSplitOptions.RemoveEmptyEntries);");
            builder.AppendLine();
            builder.AppendLine("if (lines.Length <= 1) return result;");
            builder.AppendLine();
            builder.AppendLine("// Parse header");
            builder.AppendLine("var headers = ParseCsvLine(lines[0]);");
            builder.AppendLine("var idIndex = Array.FindIndex(headers, h => h.Equals(\"Id\", StringComparison.OrdinalIgnoreCase));");
            builder.AppendLine("var descIndex = Array.FindIndex(headers, h => h.Equals(\"Description\", StringComparison.OrdinalIgnoreCase));");
            builder.AppendLine("var categoryIndex = Array.FindIndex(headers, h => h.Equals(\"Category\", StringComparison.OrdinalIgnoreCase));");
            builder.AppendLine("var isFixedKeyIndex = Array.FindIndex(headers, h => h.Equals(\"IsFixedKey\", StringComparison.OrdinalIgnoreCase));");
            builder.AppendLine();
            builder.AppendLine("// Parse data rows");
            builder.AppendLine("for (int i = 1; i < lines.Length; i++)");
            builder.BeginBlock();
            builder.AppendLine("var values = ParseCsvLine(lines[i]);");
            builder.AppendLine("if (values.Length > idIndex && !string.IsNullOrWhiteSpace(values[idIndex]))");
            builder.BeginBlock();
            builder.AppendLine("var data = new LocalizationKeyData");
            builder.BeginBlock();
            builder.AppendLine("Id = values[idIndex],");
            builder.AppendLine("Description = descIndex >= 0 && values.Length > descIndex ? values[descIndex] : null,");
            builder.AppendLine("Category = categoryIndex >= 0 && values.Length > categoryIndex ? values[categoryIndex] : null,");
            builder.AppendLine("IsFixedKey = isFixedKeyIndex >= 0 && values.Length > isFixedKeyIndex && bool.TryParse(values[isFixedKeyIndex], out var isFixed) ? isFixed : false");
            builder.EndBlock(";");
            builder.AppendLine("result[data.Id] = data;");
            builder.EndBlock();
            builder.EndBlock();
            builder.AppendLine();
            builder.AppendLine("return result;");
            builder.EndMethod();
            
            builder.AddBlankLine();
            
            // Serialize CSV method
            builder.BeginMethod("public static string SerializeCsv(Dictionary<string, LocalizationKeyData> data, DatraConfigurationValue config, global::Datra.Interfaces.ISerializationLogger logger = null)");
            builder.AppendLine("var lines = new List<string>();");
            builder.AppendLine("lines.Add(\"Id,Description,Category,IsFixedKey\");");
            builder.AppendLine();
            builder.AppendLine("foreach (var item in data.Values.OrderBy(x => x.Id))");
            builder.BeginBlock();
            builder.AppendLine("lines.Add($\"{EscapeCsvValue(item.Id)},{EscapeCsvValue(item.Description)},{EscapeCsvValue(item.Category)},{item.IsFixedKey.ToString().ToLower()}\");");
            builder.EndBlock();
            builder.AppendLine();
            builder.AppendLine("return string.Join(\"\\n\", lines);");
            builder.EndMethod();
            
            builder.AddBlankLine();
            
            // Helper method for parsing CSV lines
            builder.BeginMethod("private static string[] ParseCsvLine(string line)");
            builder.AppendLine("var values = new List<string>();");
            builder.AppendLine("var inQuotes = false;");
            builder.AppendLine("var currentValue = new System.Text.StringBuilder();");
            builder.AppendLine();
            builder.AppendLine("for (int i = 0; i < line.Length; i++)");
            builder.BeginBlock();
            builder.AppendLine("var c = line[i];");
            builder.AppendLine();
            builder.AppendLine("if (c == '\"')");
            builder.BeginBlock();
            builder.AppendLine("if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')");
            builder.BeginBlock();
            builder.AppendLine("currentValue.Append('\"');");
            builder.AppendLine("i++;");
            builder.EndBlock();
            builder.AppendLine("else");
            builder.BeginBlock();
            builder.AppendLine("inQuotes = !inQuotes;");
            builder.EndBlock();
            builder.EndBlock();
            builder.AppendLine("else if (c == ',' && !inQuotes)");
            builder.BeginBlock();
            builder.AppendLine("values.Add(currentValue.ToString().Trim());");
            builder.AppendLine("currentValue.Clear();");
            builder.EndBlock();
            builder.AppendLine("else");
            builder.BeginBlock();
            builder.AppendLine("currentValue.Append(c);");
            builder.EndBlock();
            builder.EndBlock();
            builder.AppendLine();
            builder.AppendLine("values.Add(currentValue.ToString().Trim());");
            builder.AppendLine("return values.ToArray();");
            builder.EndMethod();
            
            builder.AddBlankLine();
            
            // Helper method for escaping CSV values
            builder.BeginMethod("private static string EscapeCsvValue(string value)");
            builder.AppendLine("if (string.IsNullOrEmpty(value)) return string.Empty;");
            builder.AppendLine("if (value.Contains(\",\") || value.Contains(\"\\\"\") || value.Contains(\"\\n\"))");
            builder.BeginBlock();
            builder.AppendLine("return $\"\\\"{value.Replace(\"\\\"\", \"\\\"\\\"\")}\\\"\";");
            builder.EndBlock();
            builder.AppendLine("return value;");
            builder.EndMethod();
            
            builder.EndClass();
            builder.EndNamespace();
            
            return builder.ToString();
        }
    }
}