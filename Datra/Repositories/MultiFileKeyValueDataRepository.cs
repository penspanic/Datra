#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Serializers;
using Datra.Utilities;

namespace Datra.Repositories
{
    /// <summary>
    /// Repository implementation for multi-file key-value table data.
    /// Each file contains a single data item, and all files are merged into one table.
    /// </summary>
    public class MultiFileKeyValueDataRepository<TKey, TData> : IKeyValueDataRepository<TKey, TData>
        where TData : class, ITableData<TKey>
    {
        private Dictionary<TKey, TData> _data = new();
        private readonly string _folderPathOrLabel;
        private readonly string _filePattern;
        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory? _serializerFactory;
        private readonly Func<string, IDataSerializer, TData>? _deserializeSingleFunc;
        private readonly Func<TData, IDataSerializer, string>? _serializeSingleFunc;
        private string _loadedFilePath = string.Empty;

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
            var files = await _rawDataProvider.LoadMultipleTextAsync(_folderPathOrLabel, _filePattern);
            _loadedFilePath = _rawDataProvider.ResolveFilePath(_folderPathOrLabel);

            // Extract extension from file pattern (e.g., "*.yaml" -> ".yaml")
            var extension = DataFormatHelper.GetExtensionFromPattern(_filePattern);
            var serializer = _serializerFactory?.GetSerializer(extension)
                ?? throw new InvalidOperationException("Serializer factory not available");

            _data.Clear();
            foreach (var (filePath, content) in files)
            {
                try
                {
                    var item = _deserializeSingleFunc!(content, serializer);
                    if (item != null)
                    {
                        _data[item.Id] = item;
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to deserialize file '{filePath}': {ex.Message}", ex);
                }
            }
        }

        public void Add(TData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var key = data.Id;
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
            if (newKey == null || (newKey is string strKey && string.IsNullOrWhiteSpace(strKey)))
                throw new InvalidOperationException("Item ID cannot be empty.");

            if (!_data.TryGetValue(oldKey, out var data))
                return false;

            if (_data.ContainsKey(newKey) && !oldKey!.Equals(newKey))
                throw new InvalidOperationException($"Item with ID '{newKey}' already exists.");

            var idProperty = typeof(TData).GetProperty("Id");
            if (idProperty != null && idProperty.CanWrite)
            {
                _data.Remove(oldKey);
                idProperty.SetValue(data, newKey);
                _data[newKey] = data;
                return true;
            }

            return false;
        }

        public void Clear()
        {
            _data.Clear();
        }

        public Task SaveAsync()
        {
            // Multi-file save would need to save each item to a separate file
            // This is not yet implemented - typically multi-file data is read-only in runtime
            throw new NotImplementedException(
                "Multi-file repository save is not yet implemented. " +
                "Multi-file data is typically read-only in runtime scenarios.");
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
            return _data.TryGetValue(key, out value!);
        }

        public TData this[TKey key] => GetById(key);

        public IEnumerable<TKey> Keys => _data.Keys;
        public IEnumerable<TData> Values => _data.Values;

        public IEnumerable<object> EnumerateItems() => _data.Values.Cast<object>();

        public int ItemCount => _data.Count;
    }
}
