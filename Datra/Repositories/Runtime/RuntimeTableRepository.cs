#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datra.Repositories.Runtime
{
    /// <summary>
    /// Table 데이터용 읽기 전용 Repository (Runtime용)
    /// InitializeAsync에서 전체 로드, 변경 추적 비활성
    /// </summary>
    /// <typeparam name="TKey">키 타입</typeparam>
    /// <typeparam name="TData">데이터 타입</typeparam>
    public abstract class RuntimeTableRepository<TKey, TData> : ITableRepository<TKey, TData>
        where TKey : notnull
        where TData : class
    {
        private readonly Dictionary<TKey, TData> _data = new();
        private bool _isInitialized;

        public event Action<bool>? OnModifiedStateChanged;

        #region IRepository

        public bool IsInitialized => _isInitialized;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _data.Clear();

            await foreach (var (key, data) in LoadAllDataAsync())
            {
                _data[key] = data;
            }

            _isInitialized = true;
        }

        protected abstract IAsyncEnumerable<(TKey key, TData data)> LoadAllDataAsync();

        #endregion

        #region ITableRepository<TKey, TData> - 메타데이터 (동기)

        public int Count => _data.Count;
        public bool Contains(TKey key) => _data.ContainsKey(key);
        public IEnumerable<TKey> Keys => _data.Keys;

        #endregion

        #region ITableRepository<TKey, TData> - 읽기

        public async Task<TData?> GetAsync(TKey key)
        {
            if (!_isInitialized)
                await InitializeAsync();

            return _data.TryGetValue(key, out var data) ? data : null;
        }

        public async Task<IReadOnlyDictionary<TKey, TData>> GetAllAsync()
        {
            if (!_isInitialized)
                await InitializeAsync();

            return _data;
        }

        public async Task<IEnumerable<TData>> FindAsync(Func<TData, bool> predicate)
        {
            if (!_isInitialized)
                await InitializeAsync();

            return _data.Values.Where(predicate);
        }

        public TData? TryGetLoaded(TKey key) =>
            _data.TryGetValue(key, out var data) ? data : null;

        public IReadOnlyDictionary<TKey, TData> LoadedItems => _data;

        #endregion

        #region ITableRepository<TKey, TData> - 쓰기 (비활성)

        public void Add(TData data) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void Add(TKey key, TData data) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void Update(TKey key, TData data) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void Remove(TKey key) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public TData GetWorkingCopy(TKey key) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void MarkAsModified(TKey key) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        #endregion

        #region IChangeTracking<TKey> (항상 false/empty)

        public ChangeState GetState(TKey key) => ChangeState.Unchanged;
        public IEnumerable<TKey> GetChangedKeys() => Enumerable.Empty<TKey>();
        public IEnumerable<TKey> GetAddedKeys() => Enumerable.Empty<TKey>();
        public IEnumerable<TKey> GetModifiedKeys() => Enumerable.Empty<TKey>();
        public IEnumerable<TKey> GetDeletedKeys() => Enumerable.Empty<TKey>();

        public TBaseline? GetBaseline<TBaseline>(TKey key) where TBaseline : class =>
            _data.TryGetValue(key, out var data) ? data as TBaseline : null;

        public bool IsPropertyModified(TKey key, string propertyName) => false;
        public IEnumerable<string> GetModifiedProperties(TKey key) => Enumerable.Empty<string>();
        public object? GetPropertyBaseline(TKey key, string propertyName) => null;

        public void TrackPropertyChange(TKey key, string propertyName, object? newValue) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void Revert(TKey key) { }
        public void RevertProperty(TKey key, string propertyName) { }

        #endregion

        #region IChangeTracking (항상 false)

        public bool HasChanges => false;
        public void Revert() { }

        public Task SaveAsync() =>
            throw new NotSupportedException("Runtime repository is read-only.");

        #endregion
    }
}
