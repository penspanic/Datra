#nullable enable
using System;
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
    /// IRawDataProvider 기반 Asset Repository 구현
    /// EditableAssetRepository 확장
    /// 각 Asset은 .datrameta 파일에 안정적인 GUID와 메타데이터를 포함
    /// </summary>
    public class AssetRepository<T> : EditableAssetRepository<T>, IEditableRepository
        where T : class
    {
        private static string MetaExtension => AssetDataAttribute.MetaExtension;

        private readonly string _folderPath;
        private readonly string _filePattern;
        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory _serializerFactory;
        private readonly Func<string, IDataSerializer, T> _deserializeFunc;
        private readonly Func<T, IDataSerializer, string>? _serializeFunc;
        private readonly ISerializationLogger? _logger;

        /// <summary>
        /// 로드된 폴더 경로
        /// </summary>
        public string LoadedFolderPath { get; private set; } = string.Empty;

        string? IEditableRepository.LoadedFilePath => LoadedFolderPath;

        /// <summary>
        /// 항목 수 (IEditableRepository 구현)
        /// </summary>
        public int ItemCount => Count;

        /// <summary>
        /// 모든 항목 열거 (IEditableRepository 구현)
        /// </summary>
        public IEnumerable<object> EnumerateItems() => LoadedAssets.Values.Select(a => (object)a);

        public AssetRepository(
            string folderPath,
            string filePattern,
            IRawDataProvider rawDataProvider,
            DataSerializerFactory serializerFactory,
            Func<string, IDataSerializer, T> deserializeFunc,
            Func<T, IDataSerializer, string>? serializeFunc = null,
            ISerializationLogger? logger = null)
        {
            _folderPath = folderPath;
            _filePattern = filePattern;
            _rawDataProvider = rawDataProvider;
            _serializerFactory = serializerFactory;
            _deserializeFunc = deserializeFunc;
            _serializeFunc = serializeFunc;
            _logger = logger;
        }

        protected override async Task<IEnumerable<AssetSummary>> LoadSummariesAsync()
        {
            // Use ListFilesAsync instead of LoadMultipleTextAsync to avoid loading file contents
            var files = await _rawDataProvider.ListFilesAsync(_folderPath, _filePattern);
            LoadedFolderPath = _rawDataProvider.ResolveFilePath(_folderPath);

            var metaSerializer = _serializerFactory.GetSerializer(".json");

            // Filter out .meta files first
            var dataFiles = files.Where(f => !f.EndsWith(MetaExtension, StringComparison.OrdinalIgnoreCase)).ToList();

            // Load all metadata in parallel
            var tasks = dataFiles.Select(async filePath =>
            {
                var metadata = await LoadOrCreateMetadataAsync(filePath, metaSerializer);
                var relativePath = GetRelativePath(filePath);
                return new AssetSummary(metadata.Guid, metadata, relativePath);
            });

            var summaries = await Task.WhenAll(tasks);
            return summaries;
        }

        protected override async Task<Asset<T>?> LoadAssetAsync(AssetId id)
        {
            var summary = GetSummary(id);
            if (summary == null)
                return null;

            // Use relative path to avoid double-combining with basePath in provider
            var relativePath = Path.Combine(_folderPath, summary.FilePath).Replace("\\", "/");
            var serializer = _serializerFactory.GetSerializer(_filePattern);

            try
            {
                var content = await _rawDataProvider.LoadTextAsync(relativePath);
                var data = _deserializeFunc(content, serializer);
                return new Asset<T>(summary.Id, summary.Metadata ?? new AssetMetadata(), data, summary.FilePath);
            }
            catch (Exception ex)
            {
                _logger?.LogParsingError(new SerializationErrorContext
                {
                    FileName = relativePath,
                    Format = _filePattern,
                    RecordId = id.ToString(),
                    Message = ex.Message
                }, ex);
                throw;
            }
        }

        protected override async Task SaveAssetAsync(Asset<T> asset)
        {
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
        }

        protected override async Task DeleteAssetAsync(AssetId id)
        {
            var summary = GetSummary(id);
            if (summary == null)
                return;

            var dataPath = Path.Combine(_folderPath, summary.FilePath);
            var metaPath = dataPath + MetaExtension;

            await _rawDataProvider.DeleteAsync(dataPath);
            await _rawDataProvider.DeleteAsync(metaPath);
        }

        private async Task<AssetMetadata> LoadOrCreateMetadataAsync(string dataFilePath, IDataSerializer metaSerializer)
        {
            // Include _folderPath to match Addressables address structure
            var metaPath = Path.Combine(_folderPath, dataFilePath + MetaExtension).Replace("\\", "/");

            try
            {
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

            // Create new metadata
            var newMetadata = CreateMetadata(dataFilePath);

            // Save the new meta file
            try
            {
                var metaContent = metaSerializer.SerializeSingle(newMetadata);
                await _rawDataProvider.SaveTextAsync(metaPath, metaContent);
            }
            catch
            {
                // Ignore save errors - might be read-only
            }

            return newMetadata;
        }

        private AssetMetadata CreateMetadata(string assetFilePath)
        {
            var metadata = AssetMetadata.CreateNew();
            var fileName = Path.GetFileNameWithoutExtension(assetFilePath);
            metadata.DisplayName = fileName;
            return metadata;
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
    }
}
