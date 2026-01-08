using System;
using System.IO;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Providers;
using Datra.Repositories;
using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
    public class MultiFileRepositoryTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly FileSystemRawDataProvider _provider;
        private readonly DataSerializerFactory _serializerFactory;

        public MultiFileRepositoryTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "DatraMultiFileTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            _provider = new FileSystemRawDataProvider(_testDirectory);
            _serializerFactory = new DataSerializerFactory();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        [Fact]
        public async Task LoadAsync_MultipleJsonFiles_LoadsAllItems()
        {
            // Arrange
            var dataFolder = Path.Combine(_testDirectory, "items");
            Directory.CreateDirectory(dataFolder);

            // Create test JSON files
            File.WriteAllText(Path.Combine(dataFolder, "item1.json"), "{\"Id\":\"item1\",\"Name\":\"Sword\",\"Value\":100}");
            File.WriteAllText(Path.Combine(dataFolder, "item2.json"), "{\"Id\":\"item2\",\"Name\":\"Shield\",\"Value\":150}");
            File.WriteAllText(Path.Combine(dataFolder, "item3.json"), "{\"Id\":\"item3\",\"Name\":\"Potion\",\"Value\":50}");

            var repository = new MultiFileKeyValueDataRepository<string, TestItem>(
                "items",
                "*.json",
                _provider,
                _serializerFactory,
                (data, serializer) => serializer.DeserializeSingle<TestItem>(data)
            );

            // Act
            await repository.LoadAsync();

            // Assert
            Assert.Equal(3, repository.Count);
            Assert.True(repository.Contains("item1"));
            Assert.True(repository.Contains("item2"));
            Assert.True(repository.Contains("item3"));

            Assert.Equal("Sword", repository["item1"].Name);
            Assert.Equal(150, repository["item2"].Value);
        }

        [Fact]
        public async Task LoadAsync_EmptyFolder_LoadsNoItems()
        {
            // Arrange
            var dataFolder = Path.Combine(_testDirectory, "empty");
            Directory.CreateDirectory(dataFolder);

            var repository = new MultiFileKeyValueDataRepository<string, TestItem>(
                "empty",
                "*.json",
                _provider,
                _serializerFactory,
                (data, serializer) => serializer.DeserializeSingle<TestItem>(data)
            );

            // Act
            await repository.LoadAsync();

            // Assert
            Assert.Equal(0, repository.Count);
        }

        [Fact]
        public async Task LoadAsync_PatternFilter_LoadsOnlyMatchingFiles()
        {
            // Arrange
            var dataFolder = Path.Combine(_testDirectory, "mixed");
            Directory.CreateDirectory(dataFolder);

            File.WriteAllText(Path.Combine(dataFolder, "item1.json"), "{\"Id\":\"item1\",\"Name\":\"A\",\"Value\":1}");
            File.WriteAllText(Path.Combine(dataFolder, "item2.json"), "{\"Id\":\"item2\",\"Name\":\"B\",\"Value\":2}");
            File.WriteAllText(Path.Combine(dataFolder, "readme.txt"), "This should be ignored");
            File.WriteAllText(Path.Combine(dataFolder, "config.yaml"), "ignored: true");

            var repository = new MultiFileKeyValueDataRepository<string, TestItem>(
                "mixed",
                "*.json",
                _provider,
                _serializerFactory,
                (data, serializer) => serializer.DeserializeSingle<TestItem>(data)
            );

            // Act
            await repository.LoadAsync();

            // Assert
            Assert.Equal(2, repository.Count);
        }

        [Fact]
        public async Task LoadAsync_NonExistentFolder_LoadsNoItems()
        {
            // Arrange
            var repository = new MultiFileKeyValueDataRepository<string, TestItem>(
                "nonexistent",
                "*.json",
                _provider,
                _serializerFactory,
                (data, serializer) => serializer.DeserializeSingle<TestItem>(data)
            );

            // Act
            await repository.LoadAsync();

            // Assert
            Assert.Equal(0, repository.Count);
        }

        [Fact]
        public void Add_NewItem_AddsToRepository()
        {
            // Arrange
            var repository = new MultiFileKeyValueDataRepository<string, TestItem>(
                "items",
                "*.json",
                _provider,
                _serializerFactory,
                (data, serializer) => serializer.DeserializeSingle<TestItem>(data)
            );

            var item = new TestItem { Id = "new_item", Name = "New", Value = 999 };

            // Act
            repository.Add(item);

            // Assert
            Assert.Equal(1, repository.Count);
            Assert.Equal("New", repository["new_item"].Name);
        }

        [Fact]
        public void TryGetById_ExistingItem_ReturnsItem()
        {
            // Arrange
            var repository = new MultiFileKeyValueDataRepository<string, TestItem>(
                "items",
                "*.json",
                _provider,
                _serializerFactory,
                (data, serializer) => serializer.DeserializeSingle<TestItem>(data)
            );

            repository.Add(new TestItem { Id = "test", Name = "Test", Value = 1 });

            // Act
            var result = repository.TryGetById("test");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test", result.Name);
        }

        [Fact]
        public void TryGetById_NonExistentItem_ReturnsNull()
        {
            // Arrange
            var repository = new MultiFileKeyValueDataRepository<string, TestItem>(
                "items",
                "*.json",
                _provider,
                _serializerFactory,
                (data, serializer) => serializer.DeserializeSingle<TestItem>(data)
            );

            // Act
            var result = repository.TryGetById("nonexistent");

            // Assert
            Assert.Null(result);
        }

        #region YAML MultiFile Tests

        [Fact]
        public async Task LoadAsync_MultipleYamlFiles_LoadsAllItems()
        {
            // Arrange
            var dataFolder = Path.Combine(_testDirectory, "yaml_items");
            Directory.CreateDirectory(dataFolder);

            // Create test YAML files
            File.WriteAllText(Path.Combine(dataFolder, "item1.yaml"), "Id: item1\nName: Sword\nValue: 100");
            File.WriteAllText(Path.Combine(dataFolder, "item2.yaml"), "Id: item2\nName: Shield\nValue: 150");
            File.WriteAllText(Path.Combine(dataFolder, "item3.yml"), "Id: item3\nName: Potion\nValue: 50");

            var repository = new MultiFileKeyValueDataRepository<string, TestItem>(
                "yaml_items",
                "*.yaml",
                _provider,
                _serializerFactory,
                (data, serializer) => serializer.DeserializeSingle<TestItem>(data)
            );

            // Act
            await repository.LoadAsync();

            // Assert - only .yaml files, not .yml
            Assert.Equal(2, repository.Count);
            Assert.True(repository.Contains("item1"));
            Assert.True(repository.Contains("item2"));
            Assert.Equal("Sword", repository["item1"].Name);
            Assert.Equal(150, repository["item2"].Value);
        }

        [Fact]
        public async Task LoadAsync_YmlPattern_LoadsYmlFiles()
        {
            // Arrange
            var dataFolder = Path.Combine(_testDirectory, "yml_items");
            Directory.CreateDirectory(dataFolder);

            File.WriteAllText(Path.Combine(dataFolder, "item1.yml"), "Id: item1\nName: Axe\nValue: 200");
            File.WriteAllText(Path.Combine(dataFolder, "item2.yml"), "Id: item2\nName: Bow\nValue: 175");

            var repository = new MultiFileKeyValueDataRepository<string, TestItem>(
                "yml_items",
                "*.yml",
                _provider,
                _serializerFactory,
                (data, serializer) => serializer.DeserializeSingle<TestItem>(data)
            );

            // Act
            await repository.LoadAsync();

            // Assert
            Assert.Equal(2, repository.Count);
            Assert.Equal("Axe", repository["item1"].Name);
            Assert.Equal(175, repository["item2"].Value);
        }

        [Fact]
        public async Task LoadAsync_MixedFormats_OnlyLoadsMatchingPattern()
        {
            // Arrange
            var dataFolder = Path.Combine(_testDirectory, "mixed_formats");
            Directory.CreateDirectory(dataFolder);

            File.WriteAllText(Path.Combine(dataFolder, "item1.json"), "{\"Id\":\"json_item\",\"Name\":\"JSON\",\"Value\":1}");
            File.WriteAllText(Path.Combine(dataFolder, "item2.yaml"), "Id: yaml_item\nName: YAML\nValue: 2");

            // Load only YAML files
            var yamlRepository = new MultiFileKeyValueDataRepository<string, TestItem>(
                "mixed_formats",
                "*.yaml",
                _provider,
                _serializerFactory,
                (data, serializer) => serializer.DeserializeSingle<TestItem>(data)
            );

            await yamlRepository.LoadAsync();

            // Assert - only YAML file loaded
            Assert.Equal(1, yamlRepository.Count);
            Assert.True(yamlRepository.Contains("yaml_item"));
            Assert.False(yamlRepository.Contains("json_item"));
        }

        #endregion
    }

    // Test data class
    public class TestItem : ITableData<string>
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
