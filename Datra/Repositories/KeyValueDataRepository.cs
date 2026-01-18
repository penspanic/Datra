#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Serializers;

namespace Datra.Repositories
{
    /// <summary>
    /// IRawDataProvider 기반 KeyValue Repository 구현
    /// EditableTableRepository 확장
    /// </summary>
    public class KeyValueDataRepository<TKey, TData> : EditableTableRepository<TKey, TData>, IEditableRepository
        where TKey : notnull
        where TData : class, ITableData<TKey>
    {
        private readonly string _filePath;
        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory? _serializerFactory;
        private readonly Func<string, IDataSerializer, Dictionary<TKey, TData>>? _deserializeFunc;
        private readonly Func<Dictionary<TKey, TData>, IDataSerializer, string>? _serializeFunc;
        private readonly Func<string, Dictionary<TKey, TData>>? _csvDeserializeFunc;
        private readonly Func<Dictionary<TKey, TData>, string>? _csvSerializeFunc;

        /// <summary>
        /// 로드된 파일 경로
        /// </summary>
        public string LoadedFilePath { get; private set; } = string.Empty;

        string? IEditableRepository.LoadedFilePath => LoadedFilePath;

        /// <summary>
        /// 항목 수 (IEditableRepository 구현)
        /// </summary>
        public int ItemCount => Count;

        /// <summary>
        /// 모든 항목 열거 (IEditableRepository 구현)
        /// </summary>
        public IEnumerable<object> EnumerateItems() => LoadedItems.Values.Cast<object>();

        /// <summary>
        /// JSON/YAML 직렬화용 생성자
        /// </summary>
        public KeyValueDataRepository(
            string filePath,
            IRawDataProvider rawDataProvider,
            DataSerializerFactory serializerFactory,
            Func<string, IDataSerializer, Dictionary<TKey, TData>> deserializeFunc,
            Func<Dictionary<TKey, TData>, IDataSerializer, string>? serializeFunc = null)
        {
            _filePath = filePath;
            _rawDataProvider = rawDataProvider;
            _serializerFactory = serializerFactory;
            _deserializeFunc = deserializeFunc;
            _serializeFunc = serializeFunc;
        }

        /// <summary>
        /// CSV 직렬화용 생성자
        /// </summary>
        public KeyValueDataRepository(
            string filePath,
            IRawDataProvider rawDataProvider,
            Func<string, Dictionary<TKey, TData>> csvDeserializeFunc,
            Func<Dictionary<TKey, TData>, string>? csvSerializeFunc = null)
        {
            _filePath = filePath;
            _rawDataProvider = rawDataProvider;
            _csvDeserializeFunc = csvDeserializeFunc;
            _csvSerializeFunc = csvSerializeFunc;
        }

        protected override TKey ExtractKey(TData data) => data.Id;

        protected override async IAsyncEnumerable<(TKey key, TData data)> LoadAllDataAsync()
        {
            var rawData = await _rawDataProvider.LoadTextAsync(_filePath);
            LoadedFilePath = _rawDataProvider.ResolveFilePath(_filePath);

            Dictionary<TKey, TData> data;

            if (_csvDeserializeFunc != null)
            {
                data = _csvDeserializeFunc(rawData);
            }
            else if (_deserializeFunc != null && _serializerFactory != null)
            {
                var serializer = _serializerFactory.GetSerializer(_filePath);
                data = _deserializeFunc(rawData, serializer);
            }
            else
            {
                throw new InvalidOperationException("No deserialize function available.");
            }

            foreach (var kvp in data)
            {
                yield return (kvp.Key, kvp.Value);
            }
        }

        protected override Task<TData?> LoadDataAsync(TKey key)
        {
            // 개별 로드는 지원하지 않음 (전체 로드 후 메모리에서 조회)
            return Task.FromResult<TData?>(null);
        }

        protected override async Task SaveAllDataAsync(
            IEnumerable<(TKey key, TData data)> addedItems,
            IEnumerable<(TKey key, TData data)> modifiedItems,
            IEnumerable<TKey> deletedKeys)
        {
            // 전체 데이터를 다시 직렬화하여 저장
            var allData = new Dictionary<TKey, TData>();

            foreach (var kvp in LoadedItems)
            {
                allData[kvp.Key] = kvp.Value;
            }

            string rawData;

            if (_csvSerializeFunc != null)
            {
                rawData = _csvSerializeFunc(allData);
            }
            else if (_serializeFunc != null && _serializerFactory != null)
            {
                var serializer = _serializerFactory.GetSerializer(_filePath);
                rawData = _serializeFunc(allData, serializer);
            }
            else
            {
                throw new InvalidOperationException("No serialize function available.");
            }

            await _rawDataProvider.SaveTextAsync(_filePath, rawData);
        }
    }
}
