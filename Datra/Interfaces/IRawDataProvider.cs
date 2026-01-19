using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datra.Interfaces
{
    /// <summary>
    /// Interface for platform-specific text data load/save
    /// Implemented with Addressables in Unity, filesystem on server, etc.
    /// </summary>
    public interface IRawDataProvider
    {
        /// <summary>
        /// Asynchronously load text data from the specified path
        /// </summary>
        Task<string> LoadTextAsync(string path);

        /// <summary>
        /// Asynchronously save text data to the specified path
        /// </summary>
        Task SaveTextAsync(string path, string content);

        /// <summary>
        /// Check if data exists at the specified path
        /// </summary>
        bool Exists(string path);

        /// <summary>
        /// Resolve the relative path to an absolute path
        /// </summary>
        string ResolveFilePath(string path);

        /// <summary>
        /// Load multiple text files from a folder or by label (for multi-file TableData).
        /// Default implementation throws NotSupportedException - override in providers that support this.
        /// </summary>
        /// <param name="folderPathOrLabel">Folder path (FileSystem) or label (Addressables)</param>
        /// <param name="pattern">File pattern like "*.json" (ignored for Addressables)</param>
        Task<Dictionary<string, string>> LoadMultipleTextAsync(string folderPathOrLabel, string pattern = "*.json")
        {
            throw new NotSupportedException($"{GetType().Name} does not support multi-file loading. Use a provider that implements LoadMultipleTextAsync.");
        }

        /// <summary>
        /// List files in a folder without loading their contents.
        /// Used by AssetRepository to get file list for lazy loading (Summary only).
        /// Default implementation falls back to LoadMultipleTextAsync and discards content.
        /// </summary>
        /// <param name="folderPathOrLabel">Folder path (FileSystem) or label (Addressables)</param>
        /// <param name="pattern">File pattern like "*.json" (ignored for Addressables)</param>
        /// <returns>List of relative file paths</returns>
        async Task<IReadOnlyList<string>> ListFilesAsync(string folderPathOrLabel, string pattern = "*.json")
        {
            // Default: fall back to LoadMultipleTextAsync (inefficient but works)
            var files = await LoadMultipleTextAsync(folderPathOrLabel, pattern);
            return files.Keys.ToList();
        }

        /// <summary>
        /// Delete a file at the specified path.
        /// Default implementation throws NotSupportedException - override in providers that support deletion.
        /// </summary>
        /// <param name="path">Path to the file to delete</param>
        /// <returns>True if deleted successfully, false if file didn't exist</returns>
        Task<bool> DeleteAsync(string path)
        {
            throw new NotSupportedException($"{GetType().Name} does not support file deletion. Use a provider that implements DeleteAsync.");
        }
    }
}
