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
    }
}
