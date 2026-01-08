using Datra.Generators.Builders;
using Datra.Generators.Models;

namespace Datra.Generators.Generators
{
    /// <summary>
    /// Generates YAML serialization/deserialization code using YamlDotNet.
    /// The generated code uses YamlDotNet directly for optimal performance.
    /// </summary>
    internal class YamlSerializerBuilder
    {
        public void GenerateTableDeserializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            // Generate code that uses YamlDotNet directly
            codeBuilder.AppendLine("var yamlConverter = new global::Datra.Converters.DataRefYamlConverter();");
            codeBuilder.AppendLine("var yamlDeserializer = new global::YamlDotNet.Serialization.DeserializerBuilder()");
            codeBuilder.AppendLine("    .WithNamingConvention(global::YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)");
            codeBuilder.AppendLine("    .IgnoreUnmatchedProperties()");
            codeBuilder.AppendLine("    .WithTypeConverter(yamlConverter)");
            codeBuilder.AppendLine("    .Build();");
            codeBuilder.AppendLine("");
            codeBuilder.AppendLine("using (var reader = new global::System.IO.StringReader(data))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine($"var items = yamlDeserializer.Deserialize<global::System.Collections.Generic.List<{typeName}>>(reader);");
            codeBuilder.AppendLine("if (items == null)");
            codeBuilder.AppendLine("    throw new global::System.InvalidOperationException(\"Failed to deserialize YAML table data.\");");
            codeBuilder.AppendLine($"return items.ToDictionary(item => item.Id);");
            codeBuilder.EndBlock();
        }

        public void GenerateTableSerializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("var yamlConverter = new global::Datra.Converters.DataRefYamlConverter();");
            codeBuilder.AppendLine("var yamlSerializer = new global::YamlDotNet.Serialization.SerializerBuilder()");
            codeBuilder.AppendLine("    .WithNamingConvention(global::YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)");
            codeBuilder.AppendLine("    .WithTypeConverter(yamlConverter)");
            codeBuilder.AppendLine("    .Build();");
            codeBuilder.AppendLine("");
            codeBuilder.AppendLine("var items = table.Values.ToList();");
            codeBuilder.AppendLine("return yamlSerializer.Serialize(items);");
        }

        public void GenerateSingleDeserializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("var yamlConverter = new global::Datra.Converters.DataRefYamlConverter();");
            codeBuilder.AppendLine("var yamlDeserializer = new global::YamlDotNet.Serialization.DeserializerBuilder()");
            codeBuilder.AppendLine("    .WithNamingConvention(global::YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)");
            codeBuilder.AppendLine("    .IgnoreUnmatchedProperties()");
            codeBuilder.AppendLine("    .WithTypeConverter(yamlConverter)");
            codeBuilder.AppendLine("    .Build();");
            codeBuilder.AppendLine("");
            codeBuilder.AppendLine("using (var reader = new global::System.IO.StringReader(data))");
            codeBuilder.BeginBlock();
            codeBuilder.AppendLine($"var result = yamlDeserializer.Deserialize<{typeName}>(reader);");
            codeBuilder.AppendLine("if (result == null)");
            codeBuilder.AppendLine("    throw new global::System.InvalidOperationException(\"Failed to deserialize YAML data.\");");
            codeBuilder.AppendLine("return result;");
            codeBuilder.EndBlock();
        }

        public void GenerateSingleSerializer(CodeBuilder codeBuilder, DataModelInfo model, string typeName)
        {
            codeBuilder.AppendLine("var yamlConverter = new global::Datra.Converters.DataRefYamlConverter();");
            codeBuilder.AppendLine("var yamlSerializer = new global::YamlDotNet.Serialization.SerializerBuilder()");
            codeBuilder.AppendLine("    .WithNamingConvention(global::YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)");
            codeBuilder.AppendLine("    .WithTypeConverter(yamlConverter)");
            codeBuilder.AppendLine("    .Build();");
            codeBuilder.AppendLine("");
            codeBuilder.AppendLine("return yamlSerializer.Serialize(data);");
        }
    }
}
