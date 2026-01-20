#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Serializers;

namespace Datra.Repositories
{
    /// <summary>
    /// IRawDataProvider 기반 SingleRepository 구현
    /// EditableSingleRepository 확장
    /// </summary>
    public class SingleDataRepository<TData> : EditableSingleRepository<TData>, IEditableRepository
        where TData : class, new()
    {
        private readonly string _filePath;
        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory _serializerFactory;
        private readonly Func<string, IDataSerializer, TData> _deserializeFunc;
        private readonly Func<TData, IDataSerializer, string>? _serializeFunc;

        public SingleDataRepository(
            string filePath,
            IRawDataProvider rawDataProvider,
            DataSerializerFactory serializerFactory,
            Func<string, IDataSerializer, TData> deserializeFunc,
            Func<TData, IDataSerializer, string>? serializeFunc = null)
        {
            _filePath = filePath;
            _rawDataProvider = rawDataProvider;
            _serializerFactory = serializerFactory;
            _deserializeFunc = deserializeFunc;
            _serializeFunc = serializeFunc;
        }

        /// <summary>
        /// 로드된 파일 경로
        /// </summary>
        public string LoadedFilePath { get; private set; } = string.Empty;

        string? IEditableRepository.LoadedFilePath => LoadedFilePath;

        /// <summary>
        /// 항목 수 (IEditableRepository 구현) - Single이므로 0 또는 1
        /// </summary>
        public int ItemCount => Current != null ? 1 : 0;

        /// <summary>
        /// 모든 항목 열거 (IEditableRepository 구현)
        /// </summary>
        public IEnumerable<object> EnumerateItems()
        {
            if (Current != null)
                yield return Current;
        }

        protected override async Task<TData?> LoadDataAsync()
        {
            try
            {
                var rawData = await _rawDataProvider.LoadTextAsync(_filePath);
                LoadedFilePath = _rawDataProvider.ResolveFilePath(_filePath);
                var serializer = _serializerFactory.GetSerializer(_filePath);
                return _deserializeFunc(rawData, serializer);
            }
            catch (FileNotFoundException)
            {
                // 파일이 없으면 기본 인스턴스 반환
                LoadedFilePath = _rawDataProvider.ResolveFilePath(_filePath);
                return new TData();
            }
        }

        protected override async Task SaveDataAsync(TData data)
        {
            if (_serializeFunc == null)
                throw new InvalidOperationException("Repository was not initialized with save functionality.");

            var serializer = _serializerFactory.GetSerializer(_filePath);
            var rawData = _serializeFunc(data, serializer);
            await _rawDataProvider.SaveTextAsync(_filePath, rawData);
        }
    }
}
