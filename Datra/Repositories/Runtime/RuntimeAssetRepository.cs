#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.DataTypes;

namespace Datra.Repositories.Runtime
{
    /// <summary>
    /// Asset 데이터용 읽기 전용 Repository (Runtime용)
    /// Summary 패턴 사용, lazy loading 지원, 변경 추적 비활성
    /// </summary>
    /// <typeparam name="T">Asset 데이터 타입</typeparam>
    public abstract class RuntimeAssetRepository<T> : IAssetRepository<T>
        where T : class
    {
        private readonly Dictionary<AssetId, AssetSummary> _summaries = new();
        private readonly Dictionary<AssetId, Asset<T>> _loadedAssets = new();
        private readonly Dictionary<string, AssetId> _pathToId = new();
        private readonly Dictionary<string, AssetId> _nameToId = new();
        private bool _isInitialized;

#pragma warning disable CS0067 // Event required by interface but not used in this implementation
        public event Action<bool>? OnModifiedStateChanged;
#pragma warning restore CS0067

        #region IRepository

        public bool IsInitialized => _isInitialized;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _summaries.Clear();
            _loadedAssets.Clear();
            _pathToId.Clear();
            _nameToId.Clear();

            var summaries = await LoadSummariesAsync();
            foreach (var summary in summaries)
            {
                _summaries[summary.Id] = summary;
                _pathToId[summary.FilePath] = summary.Id;
                _nameToId[summary.Name] = summary.Id;
            }

            _isInitialized = true;
        }

        protected abstract Task<IEnumerable<AssetSummary>> LoadSummariesAsync();
        protected abstract Task<Asset<T>?> LoadAssetAsync(AssetId id);

        #endregion

        #region IAssetRepository<T> - Summary (동기)

        public int Count => _summaries.Count;
        public IEnumerable<AssetSummary> Summaries => _summaries.Values;

        public AssetSummary? GetSummary(AssetId id) =>
            _summaries.TryGetValue(id, out var summary) ? summary : null;

        public AssetSummary? GetSummaryByPath(string path) =>
            _pathToId.TryGetValue(path, out var id) ? GetSummary(id) : null;

        public AssetSummary? GetSummaryByName(string name) =>
            _nameToId.TryGetValue(name, out var id) ? GetSummary(id) : null;

        public bool Contains(AssetId id) => _summaries.ContainsKey(id);
        public bool ContainsPath(string path) => _pathToId.ContainsKey(path);

        #endregion

        #region IAssetRepository<T> - 읽기 (비동기)

        public async Task<Asset<T>?> GetAsync(AssetId id)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (_loadedAssets.TryGetValue(id, out var loaded))
                return loaded;

            if (!_summaries.ContainsKey(id))
                return null;

            var asset = await LoadAssetAsync(id);
            if (asset != null)
            {
                _loadedAssets[id] = asset;
            }
            return asset;
        }

        public async Task<Asset<T>?> GetByPathAsync(string path)
        {
            if (_pathToId.TryGetValue(path, out var id))
                return await GetAsync(id);
            return null;
        }

        public async Task<Asset<T>?> GetByNameAsync(string name)
        {
            if (_nameToId.TryGetValue(name, out var id))
                return await GetAsync(id);
            return null;
        }

        public async Task<IEnumerable<Asset<T>>> FindAsync(Func<AssetSummary, bool> predicate)
        {
            if (!_isInitialized)
                await InitializeAsync();

            var results = new List<Asset<T>>();
            foreach (var summary in _summaries.Values.Where(predicate))
            {
                var asset = await GetAsync(summary.Id);
                if (asset != null)
                    results.Add(asset);
            }
            return results;
        }

        #endregion

        #region IAssetRepository<T> - 로드된 데이터 (동기)

        public Asset<T>? TryGetLoaded(AssetId id) =>
            _loadedAssets.TryGetValue(id, out var asset) ? asset : null;

        public bool IsLoaded(AssetId id) => _loadedAssets.ContainsKey(id);

        public IReadOnlyDictionary<AssetId, Asset<T>> LoadedAssets => _loadedAssets;

        #endregion

        #region IAssetRepository<T> - 쓰기 (비활성)

        public Asset<T> Add(T data, string filePath) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public Asset<T> Add(T data, AssetMetadata metadata, string filePath) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void Update(AssetId id, T data) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void UpdateMetadata(AssetId id, Action<AssetMetadata> action) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public bool Remove(AssetId id) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public Asset<T> GetWorkingCopy(AssetId id) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void MarkAsModified(AssetId id) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public Task SaveAsync(AssetId id) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        #endregion

        #region IChangeTracking<AssetId> (항상 false/empty)

        public ChangeState GetState(AssetId key) => ChangeState.Unchanged;
        public IEnumerable<AssetId> GetChangedKeys() => Enumerable.Empty<AssetId>();
        public IEnumerable<AssetId> GetAddedKeys() => Enumerable.Empty<AssetId>();
        public IEnumerable<AssetId> GetModifiedKeys() => Enumerable.Empty<AssetId>();
        public IEnumerable<AssetId> GetDeletedKeys() => Enumerable.Empty<AssetId>();

        public TBaseline? GetBaseline<TBaseline>(AssetId key) where TBaseline : class =>
            _loadedAssets.TryGetValue(key, out var asset) ? asset as TBaseline : null;

        public bool IsPropertyModified(AssetId key, string propertyName) => false;
        public IEnumerable<string> GetModifiedProperties(AssetId key) => Enumerable.Empty<string>();
        public object? GetPropertyBaseline(AssetId key, string propertyName) => null;

        public void TrackPropertyChange(AssetId key, string propertyName, object? newValue) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void Revert(AssetId key) { }
        public void RevertProperty(AssetId key, string propertyName) { }

        #endregion

        #region IChangeTracking (항상 false)

        public bool HasChanges => false;
        public void Revert() { }

        public Task SaveAsync() =>
            throw new NotSupportedException("Runtime repository is read-only.");

        #endregion
    }
}
