#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Serializers;

namespace Datra.Repositories
{
    /// <summary>
    /// Repository implementation for single data
    /// </summary>
    public class SingleDataRepository<TData> : ISingleDataRepository<TData>
        where TData : class
    {
        private TData _data = null!;
        private readonly string _filePath = null!;
        private readonly IRawDataProvider _rawDataProvider = null!;
        private readonly DataSerializerFactory _serializerFactory = null!;
        private readonly Func<string, IDataSerializer, TData> _deserializeFunc = null!;
        private readonly Func<TData, IDataSerializer, string> _serializeFunc = null!;
        private string _loadedFilePath = null!;

        public SingleDataRepository(TData data)
        {
            _data = data;
        }

        public SingleDataRepository(
            string filePath,
            IRawDataProvider rawDataProvider,
            DataSerializerFactory serializerFactory,
            Func<string, IDataSerializer, TData> deserializeFunc,
            Func<TData, IDataSerializer, string> serializeFunc)
        {
            _filePath = filePath;
            _rawDataProvider = rawDataProvider;
            _serializerFactory = serializerFactory;
            _deserializeFunc = deserializeFunc;
            _serializeFunc = serializeFunc;
        }

        public TData Get()
        {
            if (_data == null)
            {
                throw new InvalidOperationException("Data has not been loaded.");
            }

            return _data;
        }

        public bool IsLoaded => _data != null;

        public string GetLoadedFilePath() => _loadedFilePath;

        internal void SetData(TData data)
        {
            _data = data;
        }

        public void Set(TData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public async Task LoadAsync()
        {
            if (_rawDataProvider == null || _deserializeFunc == null)
                throw new InvalidOperationException("Repository was not initialized with load functionality.");

            var rawData = await _rawDataProvider.LoadTextAsync(_filePath);
            _loadedFilePath = _rawDataProvider.ResolveFilePath(_filePath);
            var serializer = _serializerFactory.GetSerializer(_filePath);
            _data = _deserializeFunc(rawData, serializer);
        }

        public async Task SaveAsync()
        {
            if (_rawDataProvider == null || _serializeFunc == null)
                throw new InvalidOperationException("Repository was not initialized with save functionality.");

            var serializer = _serializerFactory.GetSerializer(_filePath);
            var rawData = _serializeFunc(_data, serializer);
            await _rawDataProvider.SaveTextAsync(_filePath, rawData);
        }

        public IEnumerable<object> EnumerateItems()
        {
            if (_data != null)
                yield return _data;
        }

        public int ItemCount => _data != null ? 1 : 0;
    }
}
