using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Interfaces;

namespace Datra.Providers
{
    /// <summary>
    /// File system based data provider for non-Unity environments (Blazor, ASP.NET, etc.)
    /// </summary>
    public class FileSystemRawDataProvider : IRawDataProvider
    {
        private readonly string _basePath;

        /// <summary>
        /// Creates a new FileSystemRawDataProvider
        /// </summary>
        /// <param name="basePath">Base directory path for all data files</param>
        public FileSystemRawDataProvider(string basePath)
        {
            _basePath = basePath ?? string.Empty;
        }

        public async Task<string> LoadTextAsync(string path)
        {
            var fullPath = GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fullPath}", fullPath);
            }
#if NETSTANDARD2_0
            return await Task.FromResult(File.ReadAllText(fullPath));
#else
            return await File.ReadAllTextAsync(fullPath);
#endif
        }

        public async Task SaveTextAsync(string path, string content)
        {
            var fullPath = GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

#if NETSTANDARD2_0
            File.WriteAllText(fullPath, content);
            await Task.CompletedTask;
#else
            await File.WriteAllTextAsync(fullPath, content);
#endif
        }

        public bool Exists(string path)
        {
            var fullPath = GetFullPath(path);
            return File.Exists(fullPath);
        }

        public string ResolveFilePath(string path)
        {
            return Path.GetFullPath(GetFullPath(path));
        }

        /// <summary>
        /// Asynchronously load multiple text files from a directory.
        /// </summary>
        /// <param name="folderPath">Relative folder path from base path</param>
        /// <param name="pattern">Search pattern (e.g., "*.json")</param>
        /// <returns>Dictionary where key is file name (relative to folderPath), value is content</returns>
        public async Task<Dictionary<string, string>> LoadMultipleTextAsync(string folderPath, string pattern = "*.json")
        {
            var result = new Dictionary<string, string>();
            var absoluteFolderPath = GetFullPath(folderPath);

            if (!Directory.Exists(absoluteFolderPath))
            {
                return result;
            }

            var absoluteFiles = Directory.GetFiles(absoluteFolderPath, pattern, SearchOption.TopDirectoryOnly);

            foreach (var absoluteFile in absoluteFiles)
            {
                var fileName = Path.GetFileName(absoluteFile);
                // Build path for LoadTextAsync (relative to basePath)
                var loadPath = string.IsNullOrEmpty(folderPath)
                    ? fileName
                    : Path.Combine(folderPath, fileName).Replace('\\', '/');
                var content = await LoadTextAsync(loadPath);
                // Key is file name only (relative to folderPath)
                result[fileName] = content;
            }

            return result;
        }

        /// <summary>
        /// List files in a directory without loading their contents.
        /// More efficient than LoadMultipleTextAsync for lazy loading scenarios.
        /// </summary>
        /// <param name="folderPath">Relative folder path from base path</param>
        /// <param name="pattern">Search pattern (e.g., "*.json")</param>
        /// <returns>List of file names (relative to folderPath, not including folderPath)</returns>
        public Task<IReadOnlyList<string>> ListFilesAsync(string folderPath, string pattern = "*.json")
        {
            var absoluteFolderPath = GetFullPath(folderPath);

            if (!Directory.Exists(absoluteFolderPath))
            {
                return Task.FromResult<IReadOnlyList<string>>(new List<string>());
            }

            // Return file names only (relative to folderPath)
            var files = Directory.GetFiles(absoluteFolderPath, pattern, SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(files);
        }

        /// <summary>
        /// Delete a file at the specified path
        /// </summary>
        /// <param name="path">Relative path from base path</param>
        /// <returns>True if deleted, false if file didn't exist</returns>
        public Task<bool> DeleteAsync(string path)
        {
            var fullPath = GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                return Task.FromResult(false);
            }

            File.Delete(fullPath);
            return Task.FromResult(true);
        }

        private string GetFullPath(string path)
        {
            if (string.IsNullOrEmpty(_basePath))
            {
                return path;
            }

            // Normalize path separators
            path = path.Replace('\\', Path.DirectorySeparatorChar)
                      .Replace('/', Path.DirectorySeparatorChar);

            return Path.Combine(_basePath, path);
        }
    }
}
