using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Data.Interfaces;
using Datra.Data.Loaders;

namespace Datra.Data.Repositories
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
        private readonly DataLoaderFactory _loaderFactory;
        private readonly Func<string, IDataLoader, Dictionary<TKey, TData>> _deserializeFunc;
        private readonly Func<Dictionary<TKey, TData>, IDataLoader, string> _serializeFunc;
        
        public DataRepository(Dictionary<TKey, TData> data)
        {
            _data = data ?? new Dictionary<TKey, TData>();
        }
        
        public DataRepository(
            string filePath,
            IRawDataProvider rawDataProvider,
            DataLoaderFactory loaderFactory,
            Func<string, IDataLoader, Dictionary<TKey, TData>> deserializeFunc,
            Func<Dictionary<TKey, TData>, IDataLoader, string> serializeFunc)
        {
            _filePath = filePath;
            _rawDataProvider = rawDataProvider;
            _loaderFactory = loaderFactory;
            _deserializeFunc = deserializeFunc;
            _serializeFunc = serializeFunc;
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
            if (_rawDataProvider == null || _deserializeFunc == null)
                throw new InvalidOperationException("Repository was not initialized with load functionality.");
                
            var rawData = await _rawDataProvider.LoadTextAsync(_filePath);
            var loader = _loaderFactory.GetLoader(_filePath);
            _data = _deserializeFunc(rawData, loader);
        }
        
        public async Task SaveAsync()
        {
            if (_rawDataProvider == null || _serializeFunc == null)
                throw new InvalidOperationException("Repository was not initialized with save functionality.");
                
            var loader = _loaderFactory.GetLoader(_filePath);
            var rawData = _serializeFunc(_data, loader);
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
        private readonly DataLoaderFactory _loaderFactory;
        private readonly Func<string, IDataLoader, TData> _deserializeFunc;
        private readonly Func<TData, IDataLoader, string> _serializeFunc;
        
        public SingleDataRepository(TData data)
        {
            _data = data;
        }
        
        public SingleDataRepository(
            string filePath,
            IRawDataProvider rawDataProvider,
            DataLoaderFactory loaderFactory,
            Func<string, IDataLoader, TData> deserializeFunc,
            Func<TData, IDataLoader, string> serializeFunc)
        {
            _filePath = filePath;
            _rawDataProvider = rawDataProvider;
            _loaderFactory = loaderFactory;
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
            var loader = _loaderFactory.GetLoader(_filePath);
            _data = _deserializeFunc(rawData, loader);
        }
        
        public async Task SaveAsync()
        {
            if (_rawDataProvider == null || _serializeFunc == null)
                throw new InvalidOperationException("Repository was not initialized with save functionality.");
                
            var loader = _loaderFactory.GetLoader(_filePath);
            var rawData = _serializeFunc(_data, loader);
            await _rawDataProvider.SaveTextAsync(_filePath, rawData);
        }
    }
}
