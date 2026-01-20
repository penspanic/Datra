using System;
using System.IO;
using System.Threading.Tasks;
using Datra.Providers;
using Xunit;

namespace Datra.Tests
{
    public class FileSystemRawDataProviderTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly FileSystemRawDataProvider _provider;

        public FileSystemRawDataProviderTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "DatraTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            _provider = new FileSystemRawDataProvider(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        [Fact]
        public async Task LoadTextAsync_ExistingFile_ReturnsContent()
        {
            // Arrange
            var fileName = "test.txt";
            var content = "Hello, World!";
            var filePath = Path.Combine(_testDirectory, fileName);
            await File.WriteAllTextAsync(filePath, content);

            // Act
            var result = await _provider.LoadTextAsync(fileName);

            // Assert
            Assert.Equal(content, result);
        }

        [Fact]
        public async Task LoadTextAsync_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _provider.LoadTextAsync("nonexistent.txt"));
        }

        [Fact]
        public async Task SaveTextAsync_NewFile_CreatesFile()
        {
            // Arrange
            var fileName = "new_file.txt";
            var content = "New content";

            // Act
            await _provider.SaveTextAsync(fileName, content);

            // Assert
            var filePath = Path.Combine(_testDirectory, fileName);
            Assert.True(File.Exists(filePath));
            Assert.Equal(content, await File.ReadAllTextAsync(filePath));
        }

        [Fact]
        public async Task SaveTextAsync_NestedDirectory_CreatesDirectoryAndFile()
        {
            // Arrange
            var fileName = "nested/dir/file.txt";
            var content = "Nested content";

            // Act
            await _provider.SaveTextAsync(fileName, content);

            // Assert
            var filePath = Path.Combine(_testDirectory, fileName);
            Assert.True(File.Exists(filePath));
            Assert.Equal(content, await File.ReadAllTextAsync(filePath));
        }

        [Fact]
        public void Exists_ExistingFile_ReturnsTrue()
        {
            // Arrange
            var fileName = "exists.txt";
            var filePath = Path.Combine(_testDirectory, fileName);
            File.WriteAllText(filePath, "content");

            // Act
            var result = _provider.Exists(fileName);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Exists_NonExistentFile_ReturnsFalse()
        {
            // Act
            var result = _provider.Exists("nonexistent.txt");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ResolveFilePath_ReturnsAbsolutePath()
        {
            // Arrange
            var fileName = "test.txt";

            // Act
            var result = _provider.ResolveFilePath(fileName);

            // Assert
            Assert.True(Path.IsPathRooted(result));
            Assert.Contains(fileName, result);
        }

        [Fact]
        public async Task ListFilesAsync_EmptyDirectory_ReturnsEmpty()
        {
            // Act
            var result = await _provider.ListFilesAsync(".");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task ListFilesAsync_WithFiles_ReturnsMatchingFiles()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testDirectory, "file1.json"), "{}");
            File.WriteAllText(Path.Combine(_testDirectory, "file2.json"), "{}");
            File.WriteAllText(Path.Combine(_testDirectory, "file3.txt"), "text");

            // Act
            var jsonFiles = await _provider.ListFilesAsync(".", "*.json");
            var allFiles = await _provider.ListFilesAsync(".", "*.*");

            // Assert
            Assert.Equal(2, jsonFiles.Count);
            Assert.Equal(3, allFiles.Count);
        }

        [Fact]
        public async Task ListFilesAsync_NonExistentDirectory_ReturnsEmpty()
        {
            // Act
            var result = await _provider.ListFilesAsync("nonexistent");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task LoadMultipleTextAsync_LoadsAllMatchingFiles()
        {
            // Arrange
            var subDir = "data";
            Directory.CreateDirectory(Path.Combine(_testDirectory, subDir));
            File.WriteAllText(Path.Combine(_testDirectory, subDir, "item1.json"), "{\"id\":1}");
            File.WriteAllText(Path.Combine(_testDirectory, subDir, "item2.json"), "{\"id\":2}");
            File.WriteAllText(Path.Combine(_testDirectory, subDir, "readme.txt"), "ignore me");

            // Act
            var result = await _provider.LoadMultipleTextAsync(subDir, "*.json");

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains("{\"id\":1}", result.Values);
            Assert.Contains("{\"id\":2}", result.Values);
        }

        [Fact]
        public async Task RoundTrip_SaveAndLoad_PreservesContent()
        {
            // Arrange
            var fileName = "roundtrip.json";
            var content = "{\"name\":\"test\",\"value\":123}";

            // Act
            await _provider.SaveTextAsync(fileName, content);
            var loaded = await _provider.LoadTextAsync(fileName);

            // Assert
            Assert.Equal(content, loaded);
        }
    }
}
