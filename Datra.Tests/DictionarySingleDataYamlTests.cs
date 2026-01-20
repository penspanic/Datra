using System.IO;
using System.Linq;
using Datra.SampleData.Generated;
using Datra.SampleData.Models;
using Datra.Serializers;
using Xunit;
using Xunit.Abstractions;

namespace Datra.Tests
{
    /// <summary>
    /// Tests for YAML SingleData with Dictionary properties.
    /// This tests the scenario where SingleData contains Dictionary<string, T> properties.
    /// </summary>
    public class DictionarySingleDataYamlTests
    {
        private readonly ITestOutputHelper _output;
        private readonly GameDataContext _context;

        public DictionarySingleDataYamlTests(ITestOutputHelper output)
        {
            _output = output;
            _context = TestDataHelper.CreateGameDataContext();
            _context.LoadAllAsync().Wait();
        }

        [Fact]
        public void Should_LoadDictionaryConfig_FromYaml()
        {
            // Act
            var config = _context.DictionaryConfig.Current;

            // Assert
            Assert.NotNull(config);
            Assert.Equal("Test Dictionary Config", config.Name);
            Assert.Equal(3, config.TotalCount);
        }

        [Fact]
        public void Should_ParseStringList_FromYaml()
        {
            // Act
            var config = _context.DictionaryConfig.Current;

            // Assert
            Assert.NotNull(config.EntryPoints);
            Assert.Equal(2, config.EntryPoints.Count);
            Assert.Contains("entry-a", config.EntryPoints);
            Assert.Contains("entry-b", config.EntryPoints);
        }

        [Fact]
        public void Should_ParseDictionary_StringToInt_FromYaml()
        {
            // Act
            var config = _context.DictionaryConfig.Current;

            // Assert
            Assert.NotNull(config.CategoryCounts);
            Assert.Equal(2, config.CategoryCounts.Count);
            Assert.Equal(2, config.CategoryCounts["main"]);
            Assert.Equal(1, config.CategoryCounts["extra"]);
        }

        [Fact]
        public void Should_ParseDictionary_StringToListString_FromYaml()
        {
            // Act
            var config = _context.DictionaryConfig.Current;

            // Assert
            Assert.NotNull(config.StartPoints);
            Assert.Equal(2, config.StartPoints.Count);

            Assert.True(config.StartPoints.ContainsKey("default"));
            Assert.Single(config.StartPoints["default"]);
            Assert.Equal("entry-a", config.StartPoints["default"][0]);

            Assert.True(config.StartPoints.ContainsKey("extra"));
            Assert.Single(config.StartPoints["extra"]);
            Assert.Equal("entry-b", config.StartPoints["extra"][0]);
        }

        [Fact]
        public void Should_ParseDictionary_StringToNestedObject_FromYaml()
        {
            // Act
            var config = _context.DictionaryConfig.Current;

            // Assert
            Assert.NotNull(config.Entries);
            Assert.Equal(3, config.Entries.Count);

            // entry-a
            Assert.True(config.Entries.ContainsKey("entry-a"));
            var entryA = config.Entries["entry-a"];
            Assert.Equal("entry-a", entryA.EntryId);
            Assert.Equal("main", entryA.Category);
            Assert.Equal(5, entryA.NodeCount);
            Assert.True(entryA.IsEntryPoint);
            Assert.Equal(2, entryA.Tags.Count);
            Assert.Contains("tutorial", entryA.Tags);
            Assert.Contains("basic", entryA.Tags);

            // entry-b
            Assert.True(config.Entries.ContainsKey("entry-b"));
            var entryB = config.Entries["entry-b"];
            Assert.Equal("entry-b", entryB.EntryId);
            Assert.Equal("main", entryB.Category);
            Assert.Equal(3, entryB.NodeCount);
            Assert.False(entryB.IsEntryPoint);
        }

        [Fact]
        public void Should_ParseNestedList_InDictionaryValue_FromYaml()
        {
            // Act
            var config = _context.DictionaryConfig.Current;
            var entryA = config.Entries["entry-a"];
            var entryB = config.Entries["entry-b"];

            // Assert - OutgoingLinks
            Assert.Single(entryA.OutgoingLinks);
            var outgoingLink = entryA.OutgoingLinks[0];
            Assert.Equal("entry-a", outgoingLink.SourceId);
            Assert.Equal("entry-b", outgoingLink.TargetId);
            Assert.Equal(LinkType.Immediate, outgoingLink.LinkType);
            Assert.Equal(0, outgoingLink.ChoiceIndex);

            // Assert - IncomingLinks
            Assert.Empty(entryA.IncomingLinks);
            Assert.Single(entryB.IncomingLinks);
            var incomingLink = entryB.IncomingLinks[0];
            Assert.Equal("entry-a", incomingLink.SourceId);
            Assert.Equal("entry-b", incomingLink.TargetId);
        }

        [Fact]
        public void Should_DeserializeYaml_DictionaryConfig_WithSerializer()
        {
            // Arrange
            var yamlPath = Path.Combine(TestDataHelper.FindDataPath(), "DictionaryConfig.yaml");
            var yamlContent = File.ReadAllText(yamlPath);
            var serializer = new YamlDataSerializer();

            // Act
            var config = serializer.DeserializeSingle<DictionaryConfigData>(yamlContent);

            // Assert
            Assert.NotNull(config);
            Assert.Equal("Test Dictionary Config", config.Name);
            Assert.Equal(3, config.Entries.Count);
        }

        [Fact]
        public void Should_SerializeYaml_DictionaryConfig()
        {
            // Arrange
            var config = _context.DictionaryConfig.Current;

            // Act
            var yaml = DictionaryConfigDataSerializer.SerializeYaml(config);

            // Assert
            Assert.NotEmpty(yaml);
            Assert.Contains("Name:", yaml);
            Assert.Contains("Test Dictionary Config", yaml);
            Assert.Contains("CategoryCounts:", yaml);
            Assert.Contains("Entries:", yaml);
            Assert.Contains("entry-a:", yaml);

            _output.WriteLine("Serialized YAML:");
            _output.WriteLine(yaml);
        }

        [Fact]
        public void Should_RoundTrip_DictionaryConfig_Yaml()
        {
            // Arrange
            var original = _context.DictionaryConfig.Current;

            // Act
            var yaml = DictionaryConfigDataSerializer.SerializeYaml(original);
            var deserialized = DictionaryConfigDataSerializer.DeserializeYaml(yaml);

            // Assert
            Assert.Equal(original.Name, deserialized.Name);
            Assert.Equal(original.TotalCount, deserialized.TotalCount);
            Assert.Equal(original.EntryPoints.Count, deserialized.EntryPoints.Count);
            Assert.Equal(original.CategoryCounts.Count, deserialized.CategoryCounts.Count);
            Assert.Equal(original.StartPoints.Count, deserialized.StartPoints.Count);
            Assert.Equal(original.Entries.Count, deserialized.Entries.Count);

            // Verify nested data
            foreach (var key in original.Entries.Keys)
            {
                Assert.True(deserialized.Entries.ContainsKey(key));
                Assert.Equal(original.Entries[key].EntryId, deserialized.Entries[key].EntryId);
                Assert.Equal(original.Entries[key].Category, deserialized.Entries[key].Category);
                Assert.Equal(original.Entries[key].NodeCount, deserialized.Entries[key].NodeCount);
                Assert.Equal(original.Entries[key].IsEntryPoint, deserialized.Entries[key].IsEntryPoint);
            }
        }
    }
}
