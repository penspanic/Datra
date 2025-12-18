#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Interfaces;

namespace Datra.Editor.Interfaces
{
    /// <summary>
    /// Extended storage provider with file listing and metadata capabilities.
    /// Extends IRawDataProvider for editor scenarios requiring file management.
    /// </summary>
    public interface IStorageProvider : IRawDataProvider
    {
        /// <summary>
        /// Gets all files in a directory matching the pattern
        /// </summary>
        /// <param name="directory">Directory path</param>
        /// <param name="pattern">File pattern (e.g., "*.csv", "*.json")</param>
        /// <returns>List of file paths</returns>
        Task<IReadOnlyList<DataFilePath>> GetFilesAsync(DataFilePath directory, string pattern = "*");

        /// <summary>
        /// Gets all subdirectories in a directory
        /// </summary>
        /// <param name="directory">Directory path</param>
        /// <returns>List of directory paths</returns>
        Task<IReadOnlyList<DataFilePath>> GetDirectoriesAsync(DataFilePath directory);

        /// <summary>
        /// Gets metadata for a file
        /// </summary>
        /// <param name="path">File path</param>
        /// <returns>File metadata or null if not found</returns>
        Task<StorageFileMetadata?> GetMetadataAsync(DataFilePath path);

        /// <summary>
        /// Deletes a file or directory
        /// </summary>
        /// <param name="path">Path to delete</param>
        /// <returns>True if deleted successfully</returns>
        Task<bool> DeleteAsync(DataFilePath path);

        /// <summary>
        /// Creates a directory
        /// </summary>
        /// <param name="path">Directory path to create</param>
        /// <returns>True if created successfully</returns>
        Task<bool> CreateDirectoryAsync(DataFilePath path);

        /// <summary>
        /// Checks if path exists asynchronously
        /// </summary>
        Task<bool> ExistsAsync(DataFilePath path);
    }

    /// <summary>
    /// Metadata about a stored file
    /// </summary>
    public class StorageFileMetadata
    {
        public DataFilePath Path { get; }
        public long Size { get; }
        public DateTime LastModified { get; }
        public string? Checksum { get; }

        public StorageFileMetadata(DataFilePath path, long size, DateTime lastModified, string? checksum = null)
        {
            Path = path;
            Size = size;
            LastModified = lastModified;
            Checksum = checksum;
        }
    }
}
