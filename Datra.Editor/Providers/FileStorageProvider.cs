#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;

namespace Datra.Editor.Providers
{
    /// <summary>
    /// File system implementation of IStorageProvider.
    /// Provides file operations for editor scenarios.
    /// </summary>
    public class FileStorageProvider : IStorageProvider
    {
        private readonly string _basePath;

        public FileStorageProvider(string basePath)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        }

        #region IRawDataProvider Implementation

        public bool Exists(string path)
        {
            var fullPath = ResolveFilePath(path);
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        }

        public string ResolveFilePath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;
            return Path.Combine(_basePath, path);
        }

        public Task<string> LoadTextAsync(string path)
        {
            var fullPath = ResolveFilePath(path);
            return File.ReadAllTextAsync(fullPath);
        }

        public async Task SaveTextAsync(string path, string content)
        {
            var fullPath = ResolveFilePath(path);
            EnsureDirectoryExists(fullPath);
            await File.WriteAllTextAsync(fullPath, content);
        }

        #endregion

        #region IStorageProvider Extended Methods

        public Task<IReadOnlyList<DataFilePath>> GetFilesAsync(DataFilePath directory, string pattern = "*")
        {
            var fullPath = ResolveFilePath(directory.ToString());

            if (!Directory.Exists(fullPath))
                return Task.FromResult<IReadOnlyList<DataFilePath>>(Array.Empty<DataFilePath>());

            var files = Directory.GetFiles(fullPath, pattern)
                .Select(f => new DataFilePath(GetRelativePath(_basePath, f)))
                .ToList();

            return Task.FromResult<IReadOnlyList<DataFilePath>>(files);
        }

        public Task<IReadOnlyList<DataFilePath>> GetDirectoriesAsync(DataFilePath directory)
        {
            var fullPath = ResolveFilePath(directory.ToString());

            if (!Directory.Exists(fullPath))
                return Task.FromResult<IReadOnlyList<DataFilePath>>(Array.Empty<DataFilePath>());

            var directories = Directory.GetDirectories(fullPath)
                .Select(d => new DataFilePath(GetRelativePath(_basePath, d)))
                .ToList();

            return Task.FromResult<IReadOnlyList<DataFilePath>>(directories);
        }

        public Task<StorageFileMetadata?> GetMetadataAsync(DataFilePath path)
        {
            var fullPath = ResolveFilePath(path.ToString());

            if (!File.Exists(fullPath))
                return Task.FromResult<StorageFileMetadata?>(null);

            var fileInfo = new FileInfo(fullPath);
            var checksum = ComputeChecksum(fullPath);

            var metadata = new StorageFileMetadata(
                path: new DataFilePath(GetRelativePath(_basePath, fullPath)),
                size: fileInfo.Length,
                lastModified: fileInfo.LastWriteTimeUtc,
                checksum: checksum
            );

            return Task.FromResult<StorageFileMetadata?>(metadata);
        }

        public Task<bool> DeleteAsync(DataFilePath path)
        {
            var fullPath = ResolveFilePath(path.ToString());

            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return Task.FromResult(true);
                }

                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, recursive: true);
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> CreateDirectoryAsync(DataFilePath path)
        {
            var fullPath = ResolveFilePath(path.ToString());

            try
            {
                Directory.CreateDirectory(fullPath);
                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> ExistsAsync(DataFilePath path)
        {
            return Task.FromResult(Exists(path.ToString()));
        }

        #endregion

        #region Helper Methods

        private void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private string GetRelativePath(string basePath, string fullPath)
        {
            // Normalize paths
            basePath = Path.GetFullPath(basePath);
            fullPath = Path.GetFullPath(fullPath);

            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;

            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(basePath.Length);
            }

            return fullPath;
        }

        private string ComputeChecksum(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hashBytes);
        }

        #endregion
    }
}
