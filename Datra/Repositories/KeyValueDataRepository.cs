#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Serializers;

namespace Datra.Repositories
{
    /// <summary>
    /// Repository implementation for key-value table data
    /// </summary>
    public class KeyValueDataRepository<TKey, TData> : IKeyValueDataRepository<TKey, TData>
        where TData : class, ITableData<TKey>
    {
        private Dictionary<TKey, TData> _data = null!;
        private readonly string _filePath = null!;
        private readonly IRawDataProvider _rawDataProvider = null!;
        private readonly DataSerializerFactory _serializerFactory = null!;
        private readonly Func<string, IDataSerializer, Dictionary<TKey, TData>> _deserializeFunc = null!;
        private readonly Func<Dictionary<TKey, TData>, IDataSerializer, string> _serializeFunc = null!;
        private readonly Func<string, Dictionary<TKey, TData>> _csvDeserializeFunc = null!;
        private readonly Func<Dictionary<TKey, TData>, string> _csvSerializeFunc = null!;
        private string _loadedFilePath = null!;

        public KeyValueDataRepository(Dictionary<TKey, TData> data)
        {
            _data = data ?? new Dictionary<TKey, TData>();
        }

        public KeyValueDataRepository(
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
        public KeyValueDataRepository(
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
            _serializerFactory = null!;
            _deserializeFunc = null!;
            _serializeFunc = null!;
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

        public TData? TryGetById(TKey id)
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

        public string GetLoadedFilePath() => _loadedFilePath;

        public async Task LoadAsync()
        {
            if (_rawDataProvider == null)
                throw new InvalidOperationException("Repository was not initialized with load functionality.");

            var rawData = await _rawDataProvider.LoadTextAsync(_filePath);
            _loadedFilePath = _rawDataProvider.ResolveFilePath(_filePath);

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

        public void Add(TData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var key = data.Id;

            // Validate key is not empty
            if (key == null || (key is string strKey && string.IsNullOrWhiteSpace(strKey)))
                throw new InvalidOperationException("Item ID cannot be empty.");

            if (_data.ContainsKey(key))
                throw new InvalidOperationException($"Item with ID '{key}' already exists.");

            _data[key] = data;
        }

        public bool Remove(TKey key)
        {
            return _data.Remove(key);
        }

        public bool UpdateKey(TKey oldKey, TKey newKey)
        {
            // Validate new key is not empty
            if (newKey == null || (newKey is string strKey && string.IsNullOrWhiteSpace(strKey)))
                throw new InvalidOperationException("Item ID cannot be empty.");

            if (!_data.TryGetValue(oldKey, out var data))
                return false;

            if (_data.ContainsKey(newKey) && !oldKey!.Equals(newKey))
                throw new InvalidOperationException($"Item with ID '{newKey}' already exists.");

            // If the Id property has a setter, update it
            var idProperty = typeof(TData).GetProperty("Id");
            if (idProperty != null && idProperty.CanWrite)
            {
                _data.Remove(oldKey);
                idProperty.SetValue(data, newKey);
                _data[newKey] = data;
                return true;
            }

            // If Id is read-only, we can't update the key
            return false;
        }

        public void Clear()
        {
            _data.Clear();
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

        public IEnumerator<KeyValuePair<TKey, TData>> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool ContainsKey(TKey key)
        {
            return _data.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TData value)
        {
            return _data.TryGetValue(key, out value);
        }

        public TData this[TKey key] => GetById(key);

        public IEnumerable<TKey> Keys => _data.Keys;
        public IEnumerable<TData> Values => _data.Values;
    }
}
