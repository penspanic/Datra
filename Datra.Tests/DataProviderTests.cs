#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Datra.DataTypes;
using Datra.Providers;
using Xunit;

namespace Datra.Tests
{
    /// <summary>
    /// IDataProvider 구현 테스트
    /// </summary>
    public class DataProviderTests : IDisposable
    {
        private readonly string _testDir;
        private readonly FileSystemDataProvider _fileProvider;
        private readonly InMemoryDataProvider _memoryProvider;

        public DataProviderTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"DatraTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);

            _fileProvider = new FileSystemDataProvider(_testDir);
            _memoryProvider = new InMemoryDataProvider();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        #region Test Data

        public class TestData
        {
            public string Name { get; set; } = "";
            public int Value { get; set; }
        }

        #endregion

        #region InMemoryDataProvider Tests

        [Fact]
        public async Task InMemory_LoadTextAsync_ReturnsContent()
        {
            // Arrange
            _memoryProvider.AddTextFile("test.txt", "Hello World");

            // Act
            var content = await _memoryProvider.LoadTextAsync("test.txt");

            // Assert
            Assert.Equal("Hello World", content);
        }

        [Fact]
        public async Task InMemory_LoadAsync_ReturnsObject()
        {
            // Arrange
            _memoryProvider.AddObjectFile("data.json", new TestData { Name = "Test", Value = 42 });

            // Act
            var data = await _memoryProvider.LoadAsync<TestData>("data.json");

            // Assert
            Assert.NotNull(data);
            Assert.Equal("Test", data.Name);
            Assert.Equal(42, data.Value);
        }

        [Fact]
        public async Task InMemory_SaveTextAsync_PersistsContent()
        {
            // Act
            await _memoryProvider.SaveTextAsync("new.txt", "New Content");

            // Assert
            var content = await _memoryProvider.LoadTextAsync("new.txt");
            Assert.Equal("New Content", content);
        }

        [Fact]
        public async Task InMemory_SaveAsync_PersistsObject()
        {
            // Act
            await _memoryProvider.SaveAsync("new.json", new TestData { Name = "New", Value = 100 });

            // Assert
            var data = await _memoryProvider.LoadAsync<TestData>("new.json");
            Assert.NotNull(data);
            Assert.Equal("New", data.Name);
            Assert.Equal(100, data.Value);
        }

        [Fact]
        public async Task InMemory_DeleteAsync_RemovesFile()
        {
            // Arrange
            _memoryProvider.AddTextFile("delete.txt", "To Delete");

            // Act
            await _memoryProvider.DeleteAsync("delete.txt");

            // Assert
            var exists = await _memoryProvider.ExistsAsync("delete.txt");
            Assert.False(exists);
        }

        [Fact]
        public async Task InMemory_ExistsAsync_ReturnsTrueForExisting()
        {
            // Arrange
            _memoryProvider.AddTextFile("exists.txt", "Content");

            // Assert
            Assert.True(await _memoryProvider.ExistsAsync("exists.txt"));
            Assert.False(await _memoryProvider.ExistsAsync("nonexistent.txt"));
        }

        [Fact]
        public void InMemory_Clear_RemovesAllData()
        {
            // Arrange
            _memoryProvider.AddTextFile("file1.txt", "Content1");
            _memoryProvider.AddObjectFile("file2.json", new TestData { Name = "Test" });

            // Act
            _memoryProvider.Clear();

            // Assert
            Assert.ThrowsAsync<KeyNotFoundException>(() => _memoryProvider.LoadTextAsync("file1.txt"));
        }

        [Fact]
        public async Task InMemory_LoadAssetSummariesAsync_ReturnsMatchingSummaries()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var id2 = AssetId.NewId();
            var summary1 = new AssetSummary(id1, new AssetMetadata { Guid = id1 }, "graphs/test1.json");
            var summary2 = new AssetSummary(id2, new AssetMetadata { Guid = id2 }, "graphs/test2.json");

            _memoryProvider.AddAssetSummary("graphs", summary1);
            _memoryProvider.AddAssetSummary("graphs", summary2);
            _memoryProvider.AddAssetSummary("other", new AssetSummary(AssetId.NewId(), new AssetMetadata(), "other/file.json"));

            // Act
            var summaries = await _memoryProvider.LoadAssetSummariesAsync("graphs", "*.json");

            // Assert
            Assert.Equal(2, System.Linq.Enumerable.Count(summaries));
        }

        #endregion

        #region FileSystemDataProvider Tests

        [Fact]
        public async Task FileSystem_LoadTextAsync_ReturnsContent()
        {
            // Arrange
            var path = Path.Combine(_testDir, "test.txt");
            await File.WriteAllTextAsync(path, "File Content");

            // Act
            var content = await _fileProvider.LoadTextAsync("test.txt");

            // Assert
            Assert.Equal("File Content", content);
        }

        [Fact]
        public async Task FileSystem_LoadTextAsync_ThrowsForMissingFile()
        {
            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _fileProvider.LoadTextAsync("nonexistent.txt")
            );
        }

        [Fact]
        public async Task FileSystem_LoadAsync_ReturnsDeserializedObject()
        {
            // Arrange
            var path = Path.Combine(_testDir, "data.json");
            await File.WriteAllTextAsync(path, "{\"Name\":\"FileTest\",\"Value\":99}");

            // Act
            var data = await _fileProvider.LoadAsync<TestData>("data.json");

            // Assert
            Assert.NotNull(data);
            Assert.Equal("FileTest", data.Name);
            Assert.Equal(99, data.Value);
        }

        [Fact]
        public async Task FileSystem_LoadAsync_ReturnsNullForMissingFile()
        {
            // Act
            var data = await _fileProvider.LoadAsync<TestData>("nonexistent.json");

            // Assert
            Assert.Null(data);
        }

        [Fact]
        public async Task FileSystem_SaveTextAsync_WritesFile()
        {
            // Act
            await _fileProvider.SaveTextAsync("output.txt", "Output Content");

            // Assert
            var path = Path.Combine(_testDir, "output.txt");
            Assert.True(File.Exists(path));
            Assert.Equal("Output Content", await File.ReadAllTextAsync(path));
        }

        [Fact]
        public async Task FileSystem_SaveTextAsync_CreatesSubdirectory()
        {
            // Act
            await _fileProvider.SaveTextAsync("subdir/nested/file.txt", "Nested Content");

            // Assert
            var path = Path.Combine(_testDir, "subdir/nested/file.txt");
            Assert.True(File.Exists(path));
        }

        [Fact]
        public async Task FileSystem_SaveAsync_SerializesAndWritesFile()
        {
            // Act
            await _fileProvider.SaveAsync("output.json", new TestData { Name = "Saved", Value = 123 });

            // Assert
            var data = await _fileProvider.LoadAsync<TestData>("output.json");
            Assert.NotNull(data);
            Assert.Equal("Saved", data.Name);
            Assert.Equal(123, data.Value);
        }

        [Fact]
        public async Task FileSystem_DeleteAsync_RemovesFile()
        {
            // Arrange
            var path = Path.Combine(_testDir, "todelete.txt");
            await File.WriteAllTextAsync(path, "To Delete");

            // Act
            await _fileProvider.DeleteAsync("todelete.txt");

            // Assert
            Assert.False(File.Exists(path));
        }

        [Fact]
        public async Task FileSystem_ExistsAsync_ReturnsCorrectValue()
        {
            // Arrange
            var path = Path.Combine(_testDir, "exists.txt");
            await File.WriteAllTextAsync(path, "Content");

            // Assert
            Assert.True(await _fileProvider.ExistsAsync("exists.txt"));
            Assert.False(await _fileProvider.ExistsAsync("nonexistent.txt"));
        }

        [Fact]
        public async Task FileSystem_LoadAssetSummariesAsync_FindsFiles()
        {
            // Arrange
            var graphsDir = Path.Combine(_testDir, "graphs");
            Directory.CreateDirectory(graphsDir);

            await File.WriteAllTextAsync(Path.Combine(graphsDir, "graph1.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(graphsDir, "graph2.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(graphsDir, "other.txt"), "text");

            // Act
            var summaries = await _fileProvider.LoadAssetSummariesAsync("graphs", "*.json");
            var summaryList = System.Linq.Enumerable.ToList(summaries);

            // Assert
            Assert.Equal(2, summaryList.Count);
            Assert.All(summaryList, s => Assert.EndsWith(".json", s.FilePath));
        }

        [Fact]
        public async Task FileSystem_LoadAssetSummariesAsync_UsesMetaFile()
        {
            // Arrange
            var graphsDir = Path.Combine(_testDir, "graphs");
            Directory.CreateDirectory(graphsDir);

            var stableId = AssetId.NewId();
            var metaContent = $"{{\"Guid\":\"{stableId}\"}}";

            await File.WriteAllTextAsync(Path.Combine(graphsDir, "graph.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(graphsDir, "graph.json.datrameta"), metaContent);

            // Act
            var summaries = await _fileProvider.LoadAssetSummariesAsync("graphs", "*.json");
            var summary = System.Linq.Enumerable.First(summaries);

            // Assert
            Assert.Equal(stableId, summary.Id);
        }

        #endregion
    }
}
