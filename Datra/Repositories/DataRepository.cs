using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Serializers;

namespace Datra.Repositories
{
    /// <summary>
    /// Repository implementation for table data
    /// </summary>
    public class DataRepository<TKey, TData> : IDataRepository<TKey, TData>
        where TData : class, ITableData<TKey>
    {
        private Dictionary<TKey, TData> _data;
        private readonly string _filePath;
        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory _serializerFactory;
        private readonly Func<string, IDataSerializer, Dictionary<TKey, TData>> _deserializeFunc;
        private readonly Func<Dictionary<TKey, TData>, IDataSerializer, string> _serializeFunc;
        private readonly Func<string, Dictionary<TKey, TData>> _csvDeserializeFunc;
        private readonly Func<Dictionary<TKey, TData>, string> _csvSerializeFunc;
        
        public DataRepository(Dictionary<TKey, TData> data)
        {
            _data = data ?? new Dictionary<TKey, TData>();
        }
        
        public DataRepository(
            string filePath,
            IRawDataProvider rawDataProvider,
            DataSerializerFactory serializerFactory,
            Func<string, IDataSerializer, Dictionary<TKey, TData>> deserializeFunc,
            Func<Dictionary<TKey, TData>, IDataSerializer, string> serializeFunc)
        {
            _filePath = filePath;
            _rawDataProvider = rawDataProvider;
            _serializerFactory = serializerFactory;
            _deserializeFunc = deserializeFunc;
            _serializeFunc = serializeFunc;
            _data = new Dictionary<TKey, TData>();
        }
        
        // CSV-specific constructor (no loader needed)
        public DataRepository(
            string filePath,
            IRawDataProvider rawDataProvider,
            Func<string, Dictionary<TKey, TData>> csvDeserializeFunc,
            Func<Dictionary<TKey, TData>, string> csvSerializeFunc)
        {
            _filePath = filePath;
            _rawDataProvider = rawDataProvider;
            _csvDeserializeFunc = csvDeserializeFunc;
            _csvSerializeFunc = csvSerializeFunc;
            _data = new Dictionary<TKey, TData>();
        }
        
        public IReadOnlyDictionary<TKey, TData> GetAll() => _data;
        
        public TData GetById(TKey id)
        {
            if (_data.TryGetValue(id, out var data))
            {
                return data;
            }
            
            throw new KeyNotFoundException($"Data with ID '{id}' not found.");
        }
        
        public TData TryGetById(TKey id)
        {
            return _data.TryGetValue(id, out var data) ? data : null;
        }
        
        public IEnumerable<TData> Find(Func<TData, bool> predicate)
        {
            return _data.Values.Where(predicate);
        }
        
        public bool Contains(TKey id)
        {
            return _data.ContainsKey(id);
        }
        
        public int Count => _data.Count;
        
        public async Task LoadAsync()
        {
            if (_rawDataProvider == null)
                throw new InvalidOperationException("Repository was not initialized with load functionality.");
                
            var rawData = await _rawDataProvider.LoadTextAsync(_filePath);
            
            // Use CSV-specific deserializer if available
            if (_csvDeserializeFunc != null)
            {
                _data = _csvDeserializeFunc(rawData);
            }
            else if (_deserializeFunc != null)
            {
                var serializer = _serializerFactory.GetSerializer(_filePath);
                _data = _deserializeFunc(rawData, serializer);
            }
            else
            {
                throw new InvalidOperationException("No deserialize function available.");
            }
        }
        
        public async Task SaveAsync()
        {
            if (_rawDataProvider == null)
                throw new InvalidOperationException("Repository was not initialized with save functionality.");
                
            string rawData;
            
            // Use CSV-specific serializer if available
            if (_csvSerializeFunc != null)
            {
                rawData = _csvSerializeFunc(_data);
            }
            else if (_serializeFunc != null)
            {
                var serializer = _serializerFactory.GetSerializer(_filePath);
                rawData = _serializeFunc(_data, serializer);
            }
            else
            {
                throw new InvalidOperationException("No serialize function available.");
            }
            
            await _rawDataProvider.SaveTextAsync(_filePath, rawData);
        }
    }
    
    /// <summary>
    /// Repository implementation for single data
    /// </summary>
    public class SingleDataRepository<TData> : ISingleDataRepository<TData>
        where TData : class
    {
        private TData _data;
        private readonly string _filePath;
        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory _serializerFactory;
        private readonly Func<string, IDataSerializer, TData> _deserializeFunc;
        private readonly Func<TData, IDataSerializer, string> _serializeFunc;
        
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
        
        internal void SetData(TData data)
        {
            _data = data;
        }
        
        public async Task LoadAsync()
        {
            if (_rawDataProvider == null || _deserializeFunc == null)
                throw new InvalidOperationException("Repository was not initialized with load functionality.");
                
            var rawData = await _rawDataProvider.LoadTextAsync(_filePath);
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
    }
}
