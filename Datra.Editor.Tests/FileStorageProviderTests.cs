#nullable enable
using System.IO;
using System.Threading.Tasks;
using Datra.Editor;
using Datra.Editor.Providers;
using Xunit;

namespace Datra.Editor.Tests
{
    public class FileStorageProviderTests : IDisposable
    {
        private readonly string _testDir;
        private readonly FileStorageProvider _provider;

        public FileStorageProviderTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "DatraEditorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _provider = new FileStorageProvider(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }

        [Fact]
        public void ResolveFilePath_ReturnsFullPath()
        {
            var result = _provider.ResolveFilePath("folder/file.csv");

            Assert.Equal(Path.Combine(_testDir, "folder/file.csv"), result);
        }

        [Fact]
        public void ResolveFilePath_ReturnsAbsolutePathAsIs()
        {
            var absolutePath = "/absolute/path/file.csv";

            var result = _provider.ResolveFilePath(absolutePath);

            Assert.Equal(absolutePath, result);
        }

        [Fact]
        public async Task SaveTextAsync_CreatesFile()
        {
            var content = "test content";
            var path = "test.txt";

            await _provider.SaveTextAsync(path, content);

            var fullPath = _provider.ResolveFilePath(path);
            Assert.True(File.Exists(fullPath));
            Assert.Equal(content, await File.ReadAllTextAsync(fullPath));
        }

        [Fact]
        public async Task SaveTextAsync_CreatesDirectory()
        {
            var content = "test content";
            var path = "subdir/nested/test.txt";

            await _provider.SaveTextAsync(path, content);

            var fullPath = _provider.ResolveFilePath(path);
            Assert.True(File.Exists(fullPath));
        }

        [Fact]
        public async Task LoadTextAsync_ReadsFile()
        {
            var content = "test content";
            var path = "test.txt";
            await _provider.SaveTextAsync(path, content);

            var result = await _provider.LoadTextAsync(path);

            Assert.Equal(content, result);
        }

        [Fact]
        public void Exists_ReturnsTrueForExistingFile()
        {
            var path = "test.txt";
            File.WriteAllText(Path.Combine(_testDir, path), "content");

            Assert.True(_provider.Exists(path));
        }

        [Fact]
        public void Exists_ReturnsFalseForNonExisting()
        {
            Assert.False(_provider.Exists("nonexistent.txt"));
        }

        [Fact]
        public async Task GetFilesAsync_ReturnsMatchingFiles()
        {
            // Create test files
            await _provider.SaveTextAsync("file1.csv", "1");
            await _provider.SaveTextAsync("file2.csv", "2");
            await _provider.SaveTextAsync("file3.txt", "3");

            var csvFiles = await _provider.GetFilesAsync(new DataFilePath(""), "*.csv");

            Assert.Equal(2, csvFiles.Count);
        }

        [Fact]
        public async Task GetFilesAsync_ReturnsEmptyForNonExistentDir()
        {
            var files = await _provider.GetFilesAsync(new DataFilePath("nonexistent"), "*");

            Assert.Empty(files);
        }

        [Fact]
        public async Task GetDirectoriesAsync_ReturnsSubdirectories()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "dir1"));
            Directory.CreateDirectory(Path.Combine(_testDir, "dir2"));

            var dirs = await _provider.GetDirectoriesAsync(new DataFilePath(""));

            Assert.Equal(2, dirs.Count);
        }

        [Fact]
        public async Task DeleteAsync_DeletesFile()
        {
            var path = "test.txt";
            await _provider.SaveTextAsync(path, "content");
            Assert.True(_provider.Exists(path));

            var result = await _provider.DeleteAsync(new DataFilePath(path));

            Assert.True(result);
            Assert.False(_provider.Exists(path));
        }

        [Fact]
        public async Task DeleteAsync_DeletesDirectory()
        {
            var dirPath = "testdir";
            Directory.CreateDirectory(Path.Combine(_testDir, dirPath));
            Assert.True(_provider.Exists(dirPath));

            var result = await _provider.DeleteAsync(new DataFilePath(dirPath));

            Assert.True(result);
            Assert.False(_provider.Exists(dirPath));
        }

        [Fact]
        public async Task DeleteAsync_ReturnsFalseForNonExistent()
        {
            var result = await _provider.DeleteAsync(new DataFilePath("nonexistent.txt"));

            Assert.False(result);
        }

        [Fact]
        public async Task CreateDirectoryAsync_CreatesDirectory()
        {
            var path = new DataFilePath("newdir");

            var result = await _provider.CreateDirectoryAsync(path);

            Assert.True(result);
            Assert.True(Directory.Exists(Path.Combine(_testDir, "newdir")));
        }

        [Fact]
        public async Task ExistsAsync_Works()
        {
            await _provider.SaveTextAsync("test.txt", "content");

            Assert.True(await _provider.ExistsAsync(new DataFilePath("test.txt")));
            Assert.False(await _provider.ExistsAsync(new DataFilePath("nonexistent.txt")));
        }

        [Fact]
        public async Task GetMetadataAsync_ReturnsMetadata()
        {
            var content = "test content with some length";
            await _provider.SaveTextAsync("test.txt", content);

            var metadata = await _provider.GetMetadataAsync(new DataFilePath("test.txt"));

            Assert.NotNull(metadata);
            Assert.Equal(content.Length, metadata.Size);
            Assert.NotNull(metadata.Checksum);
        }

        [Fact]
        public async Task GetMetadataAsync_ReturnsNullForNonExistent()
        {
            var metadata = await _provider.GetMetadataAsync(new DataFilePath("nonexistent.txt"));

            Assert.Null(metadata);
        }
    }
}
