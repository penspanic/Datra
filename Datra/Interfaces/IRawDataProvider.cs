using System;
using System.Collections.Generic;
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
    }
}
