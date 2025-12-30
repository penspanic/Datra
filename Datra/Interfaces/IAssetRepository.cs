#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.DataTypes;

namespace Datra.Interfaces
{
    /// <summary>
    /// Repository interface for file-based asset data.
    /// Assets are identified by stable GUIDs from .datrameta files.
    /// </summary>
    /// <typeparam name="T">The asset data type</typeparam>
    public interface IAssetRepository<T> : IDataRepository, IReadOnlyDictionary<AssetId, Asset<T>>
        where T : class
    {
        /// <summary>
        /// Get asset by its stable GUID
        /// </summary>
        Asset<T> GetById(AssetId id);

        /// <summary>
        /// Try to get asset by its stable GUID
        /// </summary>
        Asset<T>? TryGetById(AssetId id);

        /// <summary>
        /// Get asset by its file path (relative to asset folder)
        /// </summary>
        Asset<T>? GetByPath(string filePath);

        /// <summary>
        /// Find assets matching the predicate
        /// </summary>
        IEnumerable<Asset<T>> Find(Func<Asset<T>, bool> predicate);

        /// <summary>
        /// Find assets by tag
        /// </summary>
        IEnumerable<Asset<T>> FindByTag(string tag);

        /// <summary>
        /// Find assets by category
        /// </summary>
        IEnumerable<Asset<T>> FindByCategory(string category);

        /// <summary>
        /// Get all asset data (without metadata wrapper)
        /// </summary>
        IEnumerable<T> GetAllData();

        /// <summary>
        /// Check if an asset with the given GUID exists
        /// </summary>
        bool Contains(AssetId id);

        /// <summary>
        /// Check if an asset at the given path exists
        /// </summary>
        bool ContainsPath(string filePath);

        /// <summary>
        /// Number of loaded assets
        /// </summary>
        new int Count { get; }
    }

    /// <summary>
    /// Editable version of IAssetRepository for editor scenarios
    /// </summary>
    /// <typeparam name="T">The asset data type</typeparam>
    public interface IEditableAssetRepository<T> : IAssetRepository<T>
        where T : class
    {
        /// <summary>
        /// Add a new asset
        /// </summary>
        /// <param name="data">The asset data</param>
        /// <param name="filePath">Relative file path for the new asset</param>
        /// <returns>The created asset with generated metadata</returns>
        Asset<T> Add(T data, string filePath);

        /// <summary>
        /// Add a new asset with custom metadata
        /// </summary>
        Asset<T> Add(T data, AssetMetadata metadata, string filePath);

        /// <summary>
        /// Update an existing asset's data
        /// </summary>
        void Update(AssetId id, T data);

        /// <summary>
        /// Update an existing asset's metadata
        /// </summary>
        void UpdateMetadata(AssetId id, Action<AssetMetadata> updateAction);

        /// <summary>
        /// Remove an asset by GUID
        /// </summary>
        bool Remove(AssetId id);

        /// <summary>
        /// Remove an asset by file path
        /// </summary>
        bool RemoveByPath(string filePath);

        /// <summary>
        /// Rename/move an asset to a new path (GUID remains stable)
        /// </summary>
        bool Move(AssetId id, string newFilePath);

        /// <summary>
        /// Save a specific asset
        /// </summary>
        Task SaveAssetAsync(AssetId id);

        /// <summary>
        /// Save all modified assets
        /// </summary>
        new Task SaveAsync();

        /// <summary>
        /// Create a new .datrameta file for an asset that doesn't have one
        /// </summary>
        AssetMetadata CreateMetaFile(string assetFilePath);

        /// <summary>
        /// Mark an asset as modified (for in-place property edits).
        /// This ensures the asset will be saved when SaveAsync() is called.
        /// </summary>
        void MarkAsModified(AssetId id);
    }
}
