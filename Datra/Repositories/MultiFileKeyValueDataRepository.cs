#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Serializers;
using Datra.Utilities;

namespace Datra.Repositories
{
    /// <summary>
    /// IRawDataProvider 기반 Multi-File KeyValue Repository 구현
    /// EditableTableRepository 확장
    /// 각 파일이 하나의 데이터 항목을 포함하며, 모든 파일이 하나의 테이블로 병합됨
    /// </summary>
    public class MultiFileKeyValueDataRepository<TKey, TData> : EditableTableRepository<TKey, TData>
        where TKey : notnull
        where TData : class, ITableData<TKey>
    {
        private readonly string _folderPathOrLabel;
        private readonly string _filePattern;
        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory _serializerFactory;
        private readonly Func<string, IDataSerializer, TData> _deserializeSingleFunc;
        private readonly Func<TData, IDataSerializer, string>? _serializeSingleFunc;

        /// <summary>
        /// 로드된 폴더 경로
        /// </summary>
        public string LoadedFolderPath { get; private set; } = string.Empty;

        /// <summary>
        /// Creates a multi-file repository
        /// </summary>
        /// <param name="folderPathOrLabel">Folder path (for file system) or Addressables label</param>
        /// <param name="filePattern">File pattern like "*.json", "*.yaml", or "*.csv"</param>
        /// <param name="rawDataProvider">Data provider that supports LoadMultipleTextAsync</param>
        /// <param name="serializerFactory">Serializer factory</param>
        /// <param name="deserializeSingleFunc">Function to deserialize a single item</param>
        /// <param name="serializeSingleFunc">Function to serialize a single item</param>
        public MultiFileKeyValueDataRepository(
            string folderPathOrLabel,
            string filePattern,
            IRawDataProvider rawDataProvider,
            DataSerializerFactory serializerFactory,
            Func<string, IDataSerializer, TData> deserializeSingleFunc,
            Func<TData, IDataSerializer, string>? serializeSingleFunc = null)
        {
            _folderPathOrLabel = folderPathOrLabel;
            _filePattern = filePattern;
            _rawDataProvider = rawDataProvider;
            _serializerFactory = serializerFactory;
            _deserializeSingleFunc = deserializeSingleFunc;
            _serializeSingleFunc = serializeSingleFunc;
        }

        protected override TKey ExtractKey(TData data) => data.Id;

        protected override async IAsyncEnumerable<(TKey key, TData data)> LoadAllDataAsync()
        {
            var files = await _rawDataProvider.LoadMultipleTextAsync(_folderPathOrLabel, _filePattern);
            LoadedFolderPath = _rawDataProvider.ResolveFilePath(_folderPathOrLabel);

            var extension = DataFormatHelper.GetExtensionFromPattern(_filePattern);
            var serializer = _serializerFactory.GetSerializer(extension);

            foreach (var (filePath, content) in files)
            {
                TData? item;
                try
                {
                    item = _deserializeSingleFunc(content, serializer);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to deserialize file '{filePath}': {ex.Message}", ex);
                }

                if (item != null)
                {
                    yield return (item.Id, item);
                }
            }
        }

        protected override Task<TData?> LoadDataAsync(TKey key)
        {
            // 개별 로드는 지원하지 않음 (전체 로드 후 메모리에서 조회)
            return Task.FromResult<TData?>(null);
        }

        protected override Task SaveAllDataAsync(
            IEnumerable<(TKey key, TData data)> addedItems,
            IEnumerable<(TKey key, TData data)> modifiedItems,
            IEnumerable<TKey> deletedKeys)
        {
            // Multi-file save는 각 항목을 별도 파일로 저장해야 함
            // 런타임 시나리오에서는 일반적으로 읽기 전용
            throw new NotSupportedException(
                "Multi-file repository save is not yet implemented. " +
                "Multi-file data is typically read-only in runtime scenarios.");
        }
    }
}
