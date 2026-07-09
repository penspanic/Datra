using System.Collections.Generic;
using Datra.Generators.Generators;
using Datra.Generators.Models;
using Xunit;

namespace Datra.Tests
{
    public class DataModelGeneratorTests
    {
        [Fact]
        public void GenerateDataModelFile_YamlTable_EmitsYamlHelpersByDefault()
        {
            var generator = new DataModelGenerator();

            var code = generator.GenerateDataModelFile(CreateYamlTableModel());

            Assert.Contains("DeserializeYaml", code);
            Assert.Contains("SerializeYaml", code);
            Assert.Contains("YamlDotNet", code);
        }

        [Fact]
        public void GenerateDataModelFile_YamlTable_CanSkipYamlHelpers()
        {
            var generator = new DataModelGenerator(emitYamlSerializers: false);

            var code = generator.GenerateDataModelFile(CreateYamlTableModel());

            Assert.Contains("serializer.DeserializeTable<int, MonsterData>(data);", code);
            Assert.Contains("serializer.SerializeTable<int, MonsterData>(table);", code);
            Assert.DoesNotContain("DeserializeYaml", code);
            Assert.DoesNotContain("SerializeYaml", code);
            Assert.DoesNotContain("YamlDotNet", code);
        }

        [Fact]
        public void GenerateDataModelFile_YamlSingle_CanSkipDirectYamlDeserializer()
        {
            var generator = new DataModelGenerator(emitYamlSerializers: false);

            var code = generator.GenerateDataModelFile(CreateYamlSingleModel());

            Assert.Contains("serializer.DeserializeSingle<GameSettings>(data);", code);
            Assert.Contains("serializer.SerializeSingle<GameSettings>(data);", code);
            Assert.DoesNotContain("DeserializeYaml", code);
            Assert.DoesNotContain("SerializeYaml", code);
            Assert.DoesNotContain("YamlDotNet", code);
        }

        private static DataModelInfo CreateYamlTableModel()
        {
            return new DataModelInfo
            {
                TypeName = "Game.Data.MonsterData",
                PropertyName = "Monsters",
                IsTableData = true,
                KeyType = "int",
                Format = "Yaml",
                FilePath = "monsters.yaml",
                Properties = new List<PropertyInfo>
                {
                    new PropertyInfo { Name = "Id", Type = "int" },
                    new PropertyInfo { Name = "Name", Type = "string" }
                }
            };
        }

        private static DataModelInfo CreateYamlSingleModel()
        {
            return new DataModelInfo
            {
                TypeName = "Game.Data.GameSettings",
                PropertyName = "Settings",
                IsTableData = false,
                Format = "Yaml",
                FilePath = "settings.yaml",
                Properties = new List<PropertyInfo>
                {
                    new PropertyInfo { Name = "DisplayName", Type = "string" }
                }
            };
        }
    }
}
