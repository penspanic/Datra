#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datra.Attributes;
using Datra.Interfaces;
using Datra.Serializers;
using Datra.Utilities;

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

        /// <summary>
        /// The text the data was last loaded from or saved as; see
        /// <see cref="KeyValueDataRepository{TKey,TData}"/>.
        /// </summary>
        private string? _lastKnownText;

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
                _lastKnownText = rawData;
                var serializer = ResolveSerializer();
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

            var serializer = ResolveSerializer();
            var rawData = _serializeFunc(data, serializer);

            // Keep a hand-written YAML file's comments and layout.
            if (IsYaml())
            {
                var serializeFunc = _serializeFunc;
                var original = await ReadCurrentTextAsync();
                rawData = YamlCommentPreserver.Reconcile(
                    original,
                    rawData,
                    text => _deserializeFunc(text, serializer),
                    obj => serializeFunc(obj, serializer));
            }

            await _rawDataProvider.SaveTextAsync(_filePath, rawData);
            _lastKnownText = rawData;
        }

        private async Task<string?> ReadCurrentTextAsync()
        {
            try
            {
                if (!_rawDataProvider.Exists(_filePath))
                    return _lastKnownText;
                return await _rawDataProvider.LoadTextAsync(_filePath);
            }
            catch (Exception)
            {
                return _lastKnownText;
            }
        }

        private bool IsYaml()
        {
            if (_rawDataProvider is IFormatAwareRawDataProvider fa &&
                fa.GetFormat(_filePath) is { } overrideFmt)
            {
                return overrideFmt == DataFormat.Yaml;
            }
            return DataFormatHelper.TryDetectFormat(_filePath, out var format) && format == DataFormat.Yaml;
        }

        /// <summary>
        /// See <see cref="KeyValueDataRepository{TKey,TData}"/>'s analogue —
        /// allows an <see cref="IFormatAwareRawDataProvider"/> to override
        /// extension-based format inference.
        /// </summary>
        private IDataSerializer ResolveSerializer()
        {
            if (_rawDataProvider is IFormatAwareRawDataProvider fa &&
                fa.GetFormat(_filePath) is { } overrideFmt)
            {
                return _serializerFactory.GetSerializer(_filePath, overrideFmt);
            }
            return _serializerFactory.GetSerializer(_filePath);
        }
    }
}
