#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;
using Datra.Serializers;

namespace Datra.Repositories
{
    /// <summary>
    /// Repository implementation for file-based asset data.
    /// Each asset has a companion .datrameta file containing stable GUID and metadata.
    /// Meta files are always auto-generated if missing.
    /// Note: Uses .datrameta extension to avoid conflicts with Unity's .meta files.
    /// </summary>
    public class AssetRepository<T> : IEditableAssetRepository<T>
        where T : class
    {
        private static string MetaExtension => AssetDataAttribute.MetaExtension;

        private readonly Dictionary<AssetId, Asset<T>> _dataById = new();
        private readonly Dictionary<string, Asset<T>> _dataByPath = new();
        private readonly HashSet<AssetId> _modifiedAssets = new();

        private readonly string _folderPath;
        private readonly string _filePattern;
        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory _serializerFactory;
        private readonly Func<string, IDataSerializer, T> _deserializeFunc;
        private readonly Func<T, IDataSerializer, string>? _serializeFunc;

        private string _loadedFilePath = string.Empty;

        public AssetRepository(
            string folderPath,
            string filePattern,
            IRawDataProvider rawDataProvider,
            DataSerializerFactory serializerFactory,
            Func<string, IDataSerializer, T> deserializeFunc,
            Func<T, IDataSerializer, string>? serializeFunc = null)
        {
            _folderPath = folderPath;
            _filePattern = filePattern;
            _rawDataProvider = rawDataProvider;
            _serializerFactory = serializerFactory;
            _deserializeFunc = deserializeFunc;
            _serializeFunc = serializeFunc;
        }

        #region IAssetRepository Implementation

        public Asset<T> GetById(AssetId id)
        {
            if (_dataById.TryGetValue(id, out var asset))
                return asset;
            throw new KeyNotFoundException($"Asset with ID '{id}' not found.");
        }

        public Asset<T>? TryGetById(AssetId id)
        {
            return _dataById.TryGetValue(id, out var asset) ? asset : null;
        }

        public Asset<T>? GetByPath(string filePath)
        {
            var normalizedPath = NormalizePath(filePath);
            return _dataByPath.TryGetValue(normalizedPath, out var asset) ? asset : null;
        }

        public IEnumerable<Asset<T>> Find(Func<Asset<T>, bool> predicate)
        {
            return _dataById.Values.Where(predicate);
        }

        public IEnumerable<Asset<T>> FindByTag(string tag)
        {
            return _dataById.Values.Where(a =>
                a.Metadata.Tags != null && a.Metadata.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
        }

        public IEnumerable<Asset<T>> FindByCategory(string category)
        {
            return _dataById.Values.Where(a =>
                string.Equals(a.Metadata.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<T> GetAllData()
        {
            return _dataById.Values.Select(a => a.Data);
        }

        public bool Contains(AssetId id) => _dataById.ContainsKey(id);

        public bool ContainsPath(string filePath) => _dataByPath.ContainsKey(NormalizePath(filePath));

        public int Count => _dataById.Count;

        public string GetLoadedFilePath() => _loadedFilePath;

        #endregion

        #region IEditableAssetRepository Implementation

        public Asset<T> Add(T data, string filePath)
        {
            var metadata = AssetMetadata.CreateNew();
            return Add(data, metadata, filePath);
        }

        public Asset<T> Add(T data, AssetMetadata metadata, string filePath)
        {
            var normalizedPath = NormalizePath(filePath);

            if (_dataByPath.ContainsKey(normalizedPath))
                throw new InvalidOperationException($"Asset at path '{filePath}' already exists.");

            if (_dataById.ContainsKey(metadata.Guid))
                throw new InvalidOperationException($"Asset with ID '{metadata.Guid}' already exists.");

            var asset = new Asset<T>(metadata.Guid, metadata, data, normalizedPath);
            _dataById[metadata.Guid] = asset;
            _dataByPath[normalizedPath] = asset;
            _modifiedAssets.Add(metadata.Guid);

            return asset;
        }

        public void Update(AssetId id, T data)
        {
            if (!_dataById.TryGetValue(id, out var existing))
                throw new KeyNotFoundException($"Asset with ID '{id}' not found.");

            existing.Data = data;
            existing.Metadata.ModifiedAt = DateTime.UtcNow;
            _modifiedAssets.Add(id);
        }

        public void UpdateMetadata(AssetId id, Action<AssetMetadata> updateAction)
        {
            if (!_dataById.TryGetValue(id, out var existing))
                throw new KeyNotFoundException($"Asset with ID '{id}' not found.");

            updateAction(existing.Metadata);
            existing.Metadata.ModifiedAt = DateTime.UtcNow;
            _modifiedAssets.Add(id);
        }

        public bool Remove(AssetId id)
        {
            if (!_dataById.TryGetValue(id, out var asset))
                return false;

            _dataById.Remove(id);
            _dataByPath.Remove(asset.FilePath);
            _modifiedAssets.Remove(id);
            return true;
        }

        public bool RemoveByPath(string filePath)
        {
            var normalizedPath = NormalizePath(filePath);
            if (!_dataByPath.TryGetValue(normalizedPath, out var asset))
                return false;

            return Remove(asset.Id);
        }

        public bool Move(AssetId id, string newFilePath)
        {
            if (!_dataById.TryGetValue(id, out var asset))
                return false;

            var normalizedNewPath = NormalizePath(newFilePath);
            if (_dataByPath.ContainsKey(normalizedNewPath))
                throw new InvalidOperationException($"Asset at path '{newFilePath}' already exists.");

            _dataByPath.Remove(asset.FilePath);

            // Create new asset with updated path (Asset is a class, so we update in place)
            var newAsset = new Asset<T>(asset.Id, asset.Metadata, asset.Data, normalizedNewPath);
            _dataById[id] = newAsset;
            _dataByPath[normalizedNewPath] = newAsset;

            _modifiedAssets.Add(id);
            return true;
        }

        public async Task SaveAssetAsync(AssetId id)
        {
            if (!_dataById.TryGetValue(id, out var asset))
                throw new KeyNotFoundException($"Asset with ID '{id}' not found.");

            if (_serializeFunc == null)
                throw new InvalidOperationException("Serialize function not provided.");

            var serializer = _serializerFactory.GetSerializer(_filePattern);
            var metaSerializer = _serializerFactory.GetSerializer(".json");

            // Save data file
            var dataContent = _serializeFunc(asset.Data, serializer);
            var dataPath = Path.Combine(_folderPath, asset.FilePath);
            await _rawDataProvider.SaveTextAsync(dataPath, dataContent);

            // Save meta file
            var metaContent = metaSerializer.SerializeSingle(asset.Metadata);
            var metaPath = dataPath + MetaExtension;
            await _rawDataProvider.SaveTextAsync(metaPath, metaContent);

            _modifiedAssets.Remove(id);
        }

        public async Task SaveAsync()
        {
            foreach (var id in _modifiedAssets.ToList())
            {
                await SaveAssetAsync(id);
            }
        }

        public AssetMetadata CreateMetaFile(string assetFilePath)
        {
            var normalizedPath = NormalizePath(assetFilePath);
            var metadata = AssetMetadata.CreateNew();

            // Extract display name from file name
            var fileName = Path.GetFileNameWithoutExtension(assetFilePath);
            metadata.DisplayName = fileName;

            return metadata;
        }

        #endregion

        #region Loading

        public async Task LoadAsync()
        {
            var files = await _rawDataProvider.LoadMultipleTextAsync(_folderPath, _filePattern);
            _loadedFilePath = _rawDataProvider.ResolveFilePath(_folderPath);

            var serializer = _serializerFactory.GetSerializer(_filePattern);
            var metaSerializer = _serializerFactory.GetSerializer(".json");

            _dataById.Clear();
            _dataByPath.Clear();
            _modifiedAssets.Clear();

            // Phase 1: Filter and deserialize data (synchronous, fast)
            var dataEntries = new List<(string filePath, T data)>();
            foreach (var (filePath, content) in files)
            {
                // Skip .meta files
                if (filePath.EndsWith(MetaExtension, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var data = _deserializeFunc(content, serializer);
                    if (data != null)
                        dataEntries.Add((filePath, data));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to deserialize asset '{filePath}': {ex.Message}", ex);
                }
            }

            // Phase 2: Load metadata in parallel (async, was the bottleneck)
            var loadTasks = dataEntries.Select(async entry =>
            {
                try
                {
                    var metadata = await LoadOrCreateMetadataAsync(entry.filePath, metaSerializer);
                    var relativePath = GetRelativePath(entry.filePath);
                    return new Asset<T>(metadata.Guid, metadata, entry.data, relativePath);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to load metadata for asset '{entry.filePath}': {ex.Message}", ex);
                }
            }).ToList();

            var assets = await Task.WhenAll(loadTasks);

            // Phase 3: Add to dictionaries (synchronous)
            foreach (var asset in assets)
            {
                _dataById[asset.Id] = asset;
                _dataByPath[asset.FilePath] = asset;
            }
        }

        private async Task<AssetMetadata> LoadOrCreateMetadataAsync(string dataFilePath, IDataSerializer metaSerializer)
        {
            var metaPath = dataFilePath + MetaExtension;

            try
            {
                // Try to load existing meta file
                var metaContent = await _rawDataProvider.LoadTextAsync(metaPath);
                if (!string.IsNullOrEmpty(metaContent))
                {
                    var metadata = metaSerializer.DeserializeSingle<AssetMetadata>(metaContent);
                    if (metadata != null && metadata.Guid.IsValid)
                        return metadata;
                }
            }
            catch
            {
                // Meta file doesn't exist or is invalid
            }

            // Create new metadata (always auto-generate)
            {
                var metadata = CreateMetaFile(dataFilePath);

                // Save the new meta file
                try
                {
                    var metaContent = metaSerializer.SerializeSingle(metadata);
                    await _rawDataProvider.SaveTextAsync(metaPath, metaContent);
                }
                catch
                {
                    // Ignore save errors - might be read-only
                }

                return metadata;
            }
        }

        private string GetRelativePath(string fullPath)
        {
            var basePath = _rawDataProvider.ResolveFilePath(_folderPath);
            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                var relative = fullPath.Substring(basePath.Length);
                return relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return Path.GetFileName(fullPath);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimStart('/');
        }

        #endregion

        #region IReadOnlyDictionary Implementation

        public Asset<T> this[AssetId key] => GetById(key);

        public IEnumerable<AssetId> Keys => _dataById.Keys;

        public IEnumerable<Asset<T>> Values => _dataById.Values;

        public bool ContainsKey(AssetId key) => _dataById.ContainsKey(key);

        public bool TryGetValue(AssetId key, out Asset<T> value)
        {
            return _dataById.TryGetValue(key, out value!);
        }

        public IEnumerator<KeyValuePair<AssetId, Asset<T>>> GetEnumerator()
        {
            return _dataById.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
