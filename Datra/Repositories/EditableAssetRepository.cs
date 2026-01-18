#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.DataTypes;

namespace Datra.Repositories
{
    /// <summary>
    /// 파일 기반 Asset 데이터용 Editable Repository 구현
    /// Summary 패턴: 초기화 시 메타데이터만 로드, 실제 데이터는 lazy load
    /// ChangeTrackingBase를 상속하여 변경 추적 지원
    /// </summary>
    /// <typeparam name="T">Asset 데이터 타입</typeparam>
    public abstract class EditableAssetRepository<T> : ChangeTrackingBase<AssetId, Asset<T>>, IAssetRepository<T>
        where T : class
    {
        private bool _isInitialized;

        // Summary (메타데이터만, 항상 로드)
        private readonly Dictionary<AssetId, AssetSummary> _summaries = new();
        private readonly Dictionary<string, AssetId> _pathToId = new();
        private readonly Dictionary<string, AssetId> _nameToId = new();

        #region IRepository

        public bool IsInitialized => _isInitialized;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            ClearBaselines();
            _summaries.Clear();
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

        /// <summary>
        /// 파생 클래스에서 Summary 목록 로드 구현
        /// </summary>
        protected abstract Task<IEnumerable<AssetSummary>> LoadSummariesAsync();

        /// <summary>
        /// 파생 클래스에서 개별 Asset 로드 구현
        /// </summary>
        protected abstract Task<Asset<T>?> LoadAssetAsync(AssetId id);

        /// <summary>
        /// 파생 클래스에서 Asset 저장 구현
        /// </summary>
        protected abstract Task SaveAssetAsync(Asset<T> asset);

        /// <summary>
        /// 파생 클래스에서 Asset 삭제 구현
        /// </summary>
        protected abstract Task DeleteAssetAsync(AssetId id);

        #endregion

        #region IAssetRepository<T> - Summary (동기)

        public int Count => _summaries.Count + _addedKeys.Count - _deletedKeys.Count;

        public IEnumerable<AssetSummary> Summaries
        {
            get
            {
                foreach (var summary in _summaries.Values)
                {
                    if (!_deletedKeys.Contains(summary.Id))
                        yield return summary;
                }

                // Added 항목의 Summary
                foreach (var key in _addedKeys)
                {
                    if (_workingCopies.TryGetValue(key, out var asset))
                    {
                        yield return AssetSummary.FromAsset(asset);
                    }
                }
            }
        }

        public AssetSummary? GetSummary(AssetId id)
        {
            if (_deletedKeys.Contains(id))
                return null;

            if (_summaries.TryGetValue(id, out var summary))
                return summary;

            // Added 항목
            if (_addedKeys.Contains(id) && _workingCopies.TryGetValue(id, out var asset))
            {
                return AssetSummary.FromAsset(asset);
            }

            return null;
        }

        public AssetSummary? GetSummaryByPath(string path)
        {
            if (_pathToId.TryGetValue(path, out var id))
                return GetSummary(id);
            return null;
        }

        public AssetSummary? GetSummaryByName(string name)
        {
            if (_nameToId.TryGetValue(name, out var id))
                return GetSummary(id);
            return null;
        }

        public bool Contains(AssetId id)
        {
            if (_deletedKeys.Contains(id))
                return false;

            return _summaries.ContainsKey(id) || _addedKeys.Contains(id);
        }

        public bool ContainsPath(string path)
        {
            if (!_pathToId.TryGetValue(path, out var id))
                return false;

            return !_deletedKeys.Contains(id);
        }

        #endregion

        #region IAssetRepository<T> - 읽기 (비동기)

        public async Task<Asset<T>?> GetAsync(AssetId id)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (_deletedKeys.Contains(id))
                return null;

            // Working copy 먼저 확인
            if (_workingCopies.TryGetValue(id, out var workingCopy))
                return workingCopy;

            // Baseline에서 확인
            if (_baselines.TryGetValue(id, out var baseline))
                return baseline;

            // Summary가 있으면 lazy load
            if (_summaries.ContainsKey(id))
            {
                var asset = await LoadAssetAsync(id);
                if (asset != null)
                {
                    SetBaseline(id, asset);
                    return asset;
                }
            }

            return null;
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

            foreach (var summary in Summaries)
            {
                if (predicate(summary))
                {
                    var asset = await GetAsync(summary.Id);
                    if (asset != null)
                    {
                        results.Add(asset);
                    }
                }
            }

            return results;
        }

        #endregion

        #region IAssetRepository<T> - 로드된 데이터 (동기)

        public Asset<T>? TryGetLoaded(AssetId id)
        {
            if (_deletedKeys.Contains(id))
                return null;

            if (_workingCopies.TryGetValue(id, out var workingCopy))
                return workingCopy;

            if (_baselines.TryGetValue(id, out var baseline))
                return baseline;

            return null;
        }

        public bool IsLoaded(AssetId id)
        {
            return _workingCopies.ContainsKey(id) || _baselines.ContainsKey(id);
        }

        public IReadOnlyDictionary<AssetId, Asset<T>> LoadedAssets
        {
            get
            {
                var result = new Dictionary<AssetId, Asset<T>>();

                foreach (var kvp in _baselines)
                {
                    if (!_deletedKeys.Contains(kvp.Key))
                    {
                        if (_workingCopies.TryGetValue(kvp.Key, out var workingCopy))
                            result[kvp.Key] = workingCopy;
                        else
                            result[kvp.Key] = kvp.Value;
                    }
                }

                foreach (var key in _addedKeys)
                {
                    if (_workingCopies.TryGetValue(key, out var added))
                        result[key] = added;
                }

                return result;
            }
        }

        #endregion

        #region IAssetRepository<T> - 쓰기

        public Asset<T> Add(T data, string filePath)
        {
            var metadata = AssetMetadata.CreateNew();
            return Add(data, metadata, filePath);
        }

        public Asset<T> Add(T data, AssetMetadata metadata, string filePath)
        {
            var asset = Asset<T>.Create(data, metadata, filePath);

            if (Contains(asset.Id))
                throw new InvalidOperationException($"Asset with id '{asset.Id}' already exists.");

            MarkAsAdded(asset.Id, asset);

            // Summary 등록
            var summary = AssetSummary.FromAsset(asset);
            _pathToId[filePath] = asset.Id;
            _nameToId[summary.Name] = asset.Id;

            return asset;
        }

        public void Update(AssetId id, T data)
        {
            if (!Contains(id))
                throw new KeyNotFoundException($"Asset with id '{id}' not found.");

            bool hadChanges = HasChanges;

            // 기존 Asset 가져오기
            var existing = TryGetLoaded(id);
            if (existing == null)
            {
                // Baseline에서 로드해야 함 (동기 접근이므로 예외적 상황)
                throw new InvalidOperationException($"Asset '{id}' must be loaded before updating.");
            }

            // Working copy 생성/업데이트
            var workingCopy = new Asset<T>(existing.Id, existing.Metadata, data, existing.FilePath);
            _workingCopies[id] = workingCopy;

            if (!_addedKeys.Contains(id))
            {
                MarkAsModifiedInternal(id);
            }

            NotifyIfStateChanged(hadChanges);
        }

        public void UpdateMetadata(AssetId id, Action<AssetMetadata> action)
        {
            if (!Contains(id))
                throw new KeyNotFoundException($"Asset with id '{id}' not found.");

            var existing = TryGetLoaded(id);
            if (existing == null)
                throw new InvalidOperationException($"Asset '{id}' must be loaded before updating metadata.");

            bool hadChanges = HasChanges;

            // Working copy 가져오거나 생성
            if (!_workingCopies.TryGetValue(id, out var workingCopy))
            {
                workingCopy = DeepCloner.Clone(existing);
                _workingCopies[id] = workingCopy;
            }

            action(workingCopy.Metadata);

            if (!_addedKeys.Contains(id))
            {
                MarkAsModifiedInternal(id);
            }

            NotifyIfStateChanged(hadChanges);
        }

        public bool Remove(AssetId id)
        {
            if (!Contains(id))
                return false;

            // Summary에서 제거
            if (_summaries.TryGetValue(id, out var summary))
            {
                _pathToId.Remove(summary.FilePath);
                _nameToId.Remove(summary.Name);
            }

            MarkAsDeleted(id);
            return true;
        }

        #endregion

        #region IAssetRepository<T> - Working Copy

        public Asset<T> GetWorkingCopy(AssetId id)
        {
            return GetOrCreateWorkingCopy(id);
        }

        public void MarkAsModified(AssetId id)
        {
            MarkAsModifiedInternal(id);
        }

        #endregion

        #region IAssetRepository<T> - 개별 저장

        public async Task SaveAsync(AssetId id)
        {
            if (!Contains(id))
                throw new KeyNotFoundException($"Asset with id '{id}' not found.");

            var state = GetState(id);

            if (state == ChangeState.Deleted)
            {
                await DeleteAssetAsync(id);
                _summaries.Remove(id);
                _deletedKeys.Remove(id);
            }
            else if (state == ChangeState.Added || state == ChangeState.Modified)
            {
                if (_workingCopies.TryGetValue(id, out var workingCopy))
                {
                    await SaveAssetAsync(workingCopy);

                    // Baseline 갱신
                    SetBaseline(id, workingCopy);

                    // Summary 갱신
                    var summary = AssetSummary.FromAsset(workingCopy);
                    _summaries[id] = summary;

                    _workingCopies.Remove(id);
                    _addedKeys.Remove(id);
                    _modifiedKeys.Remove(id);
                    _propertyTracker.ClearChangesForKey(id);
                }
            }

            RaiseModifiedStateChanged(HasChanges);
        }

        #endregion

        #region IChangeTracking (SaveAsync 전체 저장)

        public override async Task SaveAsync()
        {
            // Deleted 처리
            foreach (var id in _deletedKeys.ToList())
            {
                await DeleteAssetAsync(id);
                _summaries.Remove(id);
            }

            // Added 처리
            foreach (var id in _addedKeys.ToList())
            {
                if (_workingCopies.TryGetValue(id, out var added))
                {
                    await SaveAssetAsync(added);
                    SetBaseline(id, added);

                    var summary = AssetSummary.FromAsset(added);
                    _summaries[id] = summary;
                }
            }

            // Modified 처리
            foreach (var id in _modifiedKeys.ToList())
            {
                if (!_addedKeys.Contains(id) && _workingCopies.TryGetValue(id, out var modified))
                {
                    await SaveAssetAsync(modified);
                    SetBaseline(id, modified);

                    var summary = AssetSummary.FromAsset(modified);
                    _summaries[id] = summary;
                }
            }

            RefreshBaselinesAfterSave();
            RaiseModifiedStateChanged(false);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Summary를 직접 등록 (테스트용)
        /// </summary>
        protected void RegisterSummary(AssetSummary summary)
        {
            _summaries[summary.Id] = summary;
            _pathToId[summary.FilePath] = summary.Id;
            _nameToId[summary.Name] = summary.Id;
            _isInitialized = true;
        }

        /// <summary>
        /// Baseline을 직접 설정 (테스트용)
        /// </summary>
        protected void SetBaselineDirectly(AssetId id, Asset<T> asset)
        {
            SetBaseline(id, asset);
            RegisterSummary(AssetSummary.FromAsset(asset));
        }

        #endregion
    }
}
