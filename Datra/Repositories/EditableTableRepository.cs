#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datra.Repositories
{
    /// <summary>
    /// Key-Value 테이블 데이터용 Editable Repository 구현
    /// ChangeTrackingBase를 상속하여 변경 추적 지원
    /// </summary>
    /// <typeparam name="TKey">키 타입</typeparam>
    /// <typeparam name="TData">데이터 타입</typeparam>
    public abstract class EditableTableRepository<TKey, TData> : ChangeTrackingBase<TKey, TData>, ITableRepository<TKey, TData>
        where TKey : notnull
        where TData : class
    {
        private bool _isInitialized;

        #region IRepository

        public bool IsInitialized => _isInitialized;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            ClearBaselines();

            await foreach (var (key, data) in LoadAllDataAsync())
            {
                SetBaseline(key, data);
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 파생 클래스에서 전체 데이터 로드 구현
        /// </summary>
        protected abstract IAsyncEnumerable<(TKey key, TData data)> LoadAllDataAsync();

        /// <summary>
        /// 파생 클래스에서 개별 데이터 로드 구현 (lazy load용)
        /// </summary>
        protected abstract Task<TData?> LoadDataAsync(TKey key);

        /// <summary>
        /// 파생 클래스에서 데이터 저장 구현
        /// </summary>
        protected abstract Task SaveAllDataAsync(
            IEnumerable<(TKey key, TData data)> addedItems,
            IEnumerable<(TKey key, TData data)> modifiedItems,
            IEnumerable<TKey> deletedKeys);

        #endregion

        #region ITableRepository<TKey, TData>

        public int Count
        {
            get
            {
                int count = _baselines.Count;
                count += _addedKeys.Count;
                count -= _deletedKeys.Count;
                return count;
            }
        }

        public bool Contains(TKey key)
        {
            if (_deletedKeys.Contains(key))
                return false;

            return _baselines.ContainsKey(key) || _addedKeys.Contains(key);
        }

        public IEnumerable<TKey> Keys
        {
            get
            {
                foreach (var key in _baselines.Keys)
                {
                    if (!_deletedKeys.Contains(key))
                        yield return key;
                }

                foreach (var key in _addedKeys)
                {
                    yield return key;
                }
            }
        }

        public async Task<TData?> GetAsync(TKey key)
        {
            if (!_isInitialized)
                await InitializeAsync();

            // Deleted는 반환하지 않음
            if (_deletedKeys.Contains(key))
                return null;

            // Working copy 먼저 확인
            if (_workingCopies.TryGetValue(key, out var workingCopy))
                return workingCopy;

            // Baseline에서 반환
            if (_baselines.TryGetValue(key, out var baseline))
                return baseline;

            // 없으면 개별 로드 시도
            var data = await LoadDataAsync(key);
            if (data != null)
            {
                SetBaseline(key, data);
                return data;
            }

            return null;
        }

        public async Task<IReadOnlyDictionary<TKey, TData>> GetAllAsync()
        {
            if (!_isInitialized)
                await InitializeAsync();

            var result = new Dictionary<TKey, TData>();

            foreach (var key in Keys)
            {
                var data = TryGetLoaded(key);
                if (data != null)
                {
                    result[key] = data;
                }
            }

            return result;
        }

        public async Task<IEnumerable<TData>> FindAsync(Func<TData, bool> predicate)
        {
            if (!_isInitialized)
                await InitializeAsync();

            var results = new List<TData>();

            foreach (var key in Keys)
            {
                var data = TryGetLoaded(key);
                if (data != null && predicate(data))
                {
                    results.Add(data);
                }
            }

            return results;
        }

        public TData? TryGetLoaded(TKey key)
        {
            if (_deletedKeys.Contains(key))
                return null;

            if (_workingCopies.TryGetValue(key, out var workingCopy))
                return workingCopy;

            if (_baselines.TryGetValue(key, out var baseline))
                return baseline;

            return null;
        }

        public IReadOnlyDictionary<TKey, TData> LoadedItems
        {
            get
            {
                var result = new Dictionary<TKey, TData>();

                foreach (var key in Keys)
                {
                    var data = TryGetLoaded(key);
                    if (data != null)
                    {
                        result[key] = data;
                    }
                }

                return result;
            }
        }

        public void Add(TData data)
        {
            var key = ExtractKey(data);
            Add(key, data);
        }

        public void Add(TKey key, TData data)
        {
            if (Contains(key))
                throw new InvalidOperationException($"Item with key '{key}' already exists.");

            MarkAsAdded(key, DeepCloner.Clone(data));
        }

        public void Update(TKey key, TData data)
        {
            if (!Contains(key))
                throw new KeyNotFoundException($"Item with key '{key}' not found.");

            bool hadChanges = HasChanges;

            _workingCopies[key] = DeepCloner.Clone(data);

            if (!_addedKeys.Contains(key))
            {
                MarkAsModifiedInternal(key);
            }

            NotifyIfStateChanged(hadChanges);
        }

        public void Remove(TKey key)
        {
            if (!Contains(key))
                return;

            MarkAsDeleted(key);
        }

        public TData GetWorkingCopy(TKey key)
        {
            return GetOrCreateWorkingCopy(key);
        }

        public void MarkAsModified(TKey key)
        {
            MarkAsModifiedInternal(key);
        }

        /// <summary>
        /// 데이터에서 키 추출 (파생 클래스에서 구현)
        /// </summary>
        protected abstract TKey ExtractKey(TData data);

        #endregion

        #region IChangeTracking (SaveAsync 구현)

        public override async Task SaveAsync()
        {
            var addedItems = _addedKeys
                .Where(k => _workingCopies.ContainsKey(k))
                .Select(k => (k, _workingCopies[k]))
                .ToList();

            var modifiedItems = _modifiedKeys
                .Where(k => !_addedKeys.Contains(k) && _workingCopies.ContainsKey(k))
                .Select(k => (k, _workingCopies[k]))
                .ToList();

            var deletedKeys = _deletedKeys.ToList();

            await SaveAllDataAsync(addedItems, modifiedItems, deletedKeys);

            RefreshBaselinesAfterSave();

            RaiseModifiedStateChanged(false);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Baseline을 직접 설정 (테스트용)
        /// </summary>
        protected void SetBaselineDirectly(TKey key, TData data)
        {
            SetBaseline(key, data);
            _isInitialized = true;
        }

        /// <summary>
        /// 외부 변경 사항 반영 (서버에서 데이터가 변경된 경우 등)
        /// </summary>
        public void RefreshBaseline(TKey key, TData newBaseline)
        {
            bool hadChanges = HasChanges;

            SetBaseline(key, newBaseline);

            // 수정 중이 아니면 Working copy 제거
            if (!_modifiedKeys.Contains(key) && !_addedKeys.Contains(key))
            {
                _workingCopies.Remove(key);
            }

            NotifyIfStateChanged(hadChanges);
        }

        /// <summary>
        /// 전체 Baseline 새로고침
        /// </summary>
        public async Task RefreshAllBaselinesAsync()
        {
            bool hadChanges = HasChanges;

            ClearBaselines();

            await foreach (var (key, data) in LoadAllDataAsync())
            {
                SetBaseline(key, data);
            }

            NotifyIfStateChanged(hadChanges);
        }

        #endregion
    }
}
