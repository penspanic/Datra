#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datra.Repositories
{
    /// <summary>
    /// Key 기반 변경 추적의 기본 구현
    /// TableRepository, AssetRepository에서 상속
    /// </summary>
    /// <typeparam name="TKey">키 타입</typeparam>
    /// <typeparam name="TData">데이터 타입</typeparam>
    public abstract class ChangeTrackingBase<TKey, TData> : IChangeTracking<TKey>
        where TKey : notnull
        where TData : class
    {
        // Baseline (원본 스냅샷)
        protected readonly Dictionary<TKey, TData> _baselines = new();

        // Working Copies (편집본)
        protected readonly Dictionary<TKey, TData> _workingCopies = new();

        // 상태 추적
        protected readonly HashSet<TKey> _addedKeys = new();
        protected readonly HashSet<TKey> _modifiedKeys = new();
        protected readonly HashSet<TKey> _deletedKeys = new();

        // Property-level 변경 추적
        protected readonly PropertyChangeTracker<TKey> _propertyTracker = new();

        // 이벤트
        public event Action<bool>? OnModifiedStateChanged;

        #region IChangeTracking

        public virtual bool HasChanges =>
            _addedKeys.Count > 0 || _modifiedKeys.Count > 0 || _deletedKeys.Count > 0;

        public virtual void Revert()
        {
            bool hadChanges = HasChanges;

            _workingCopies.Clear();
            _addedKeys.Clear();
            _modifiedKeys.Clear();
            _deletedKeys.Clear();
            _propertyTracker.Clear();

            NotifyIfStateChanged(hadChanges);
        }

        public abstract Task SaveAsync();

        #endregion

        #region IChangeTracking<TKey>

        public ChangeState GetState(TKey key)
        {
            if (_deletedKeys.Contains(key))
                return ChangeState.Deleted;

            if (_addedKeys.Contains(key))
                return ChangeState.Added;

            if (_modifiedKeys.Contains(key))
                return ChangeState.Modified;

            return ChangeState.Unchanged;
        }

        public IEnumerable<TKey> GetChangedKeys()
        {
            return _addedKeys.Concat(_modifiedKeys).Concat(_deletedKeys).Distinct();
        }

        public IEnumerable<TKey> GetAddedKeys() => _addedKeys;
        public IEnumerable<TKey> GetModifiedKeys() => _modifiedKeys;
        public IEnumerable<TKey> GetDeletedKeys() => _deletedKeys;

        public TBaseline? GetBaseline<TBaseline>(TKey key) where TBaseline : class
        {
            if (_baselines.TryGetValue(key, out var baseline))
                return baseline as TBaseline;
            return null;
        }

        public bool IsPropertyModified(TKey key, string propertyName)
        {
            return _propertyTracker.IsPropertyModified(key, propertyName);
        }

        public IEnumerable<string> GetModifiedProperties(TKey key)
        {
            return _propertyTracker.GetModifiedProperties(key);
        }

        public object? GetPropertyBaseline(TKey key, string propertyName)
        {
            if (!_baselines.TryGetValue(key, out var baseline))
                return null;

            return PropertyChangeTracker<TKey>.GetPropertyValue(baseline, propertyName);
        }

        public virtual void TrackPropertyChange(TKey key, string propertyName, object? newValue)
        {
            // Added 상태면 이미 추적 중
            if (_addedKeys.Contains(key))
                return;

            // Deleted 상태면 무시
            if (_deletedKeys.Contains(key))
                return;

            // Baseline이 있어야 비교 가능
            if (!_baselines.TryGetValue(key, out var baseline))
                return;

            bool hadChanges = HasChanges;
            var baselineValue = PropertyChangeTracker<TKey>.GetPropertyValue(baseline, propertyName);
            bool isModified = _propertyTracker.TrackChange(key, propertyName, baselineValue, newValue);

            if (isModified)
            {
                _modifiedKeys.Add(key);
            }
            else
            {
                // 모든 속성이 원복되었는지 확인
                if (!_propertyTracker.HasChangesForKey(key))
                {
                    _modifiedKeys.Remove(key);
                }
            }

            NotifyIfStateChanged(hadChanges);
        }

        public virtual void Revert(TKey key)
        {
            bool hadChanges = HasChanges;

            if (_addedKeys.Contains(key))
            {
                _addedKeys.Remove(key);
                _workingCopies.Remove(key);
            }
            else if (_deletedKeys.Contains(key))
            {
                _deletedKeys.Remove(key);
                // Baseline으로 복원
                if (_baselines.TryGetValue(key, out var baseline))
                {
                    _workingCopies[key] = DeepCloner.Clone(baseline);
                }
            }
            else if (_modifiedKeys.Contains(key))
            {
                _modifiedKeys.Remove(key);
                _workingCopies.Remove(key);
            }

            _propertyTracker.ClearChangesForKey(key);
            NotifyIfStateChanged(hadChanges);
        }

        public virtual void RevertProperty(TKey key, string propertyName)
        {
            if (!_baselines.TryGetValue(key, out var baseline))
                return;

            if (!_workingCopies.TryGetValue(key, out var workingCopy))
                return;

            bool hadChanges = HasChanges;

            // Baseline 값으로 복원
            var baselineValue = PropertyChangeTracker<TKey>.GetPropertyValue(baseline, propertyName);
            PropertyChangeTracker<TKey>.SetPropertyValue(workingCopy, propertyName, baselineValue);

            // Property 변경 기록 제거
            _propertyTracker.ClearPropertyChange(key, propertyName);

            // 모든 속성이 원복되었는지 확인
            if (!_propertyTracker.HasChangesForKey(key))
            {
                _modifiedKeys.Remove(key);
                _workingCopies.Remove(key);
            }

            NotifyIfStateChanged(hadChanges);
        }

        #endregion

        #region Protected Helpers

        /// <summary>
        /// Baseline 초기화 (파생 클래스에서 호출)
        /// </summary>
        protected void SetBaseline(TKey key, TData data)
        {
            _baselines[key] = DeepCloner.Clone(data);
        }

        /// <summary>
        /// 모든 Baseline 초기화
        /// </summary>
        protected void ClearBaselines()
        {
            _baselines.Clear();
            _workingCopies.Clear();
            _addedKeys.Clear();
            _modifiedKeys.Clear();
            _deletedKeys.Clear();
            _propertyTracker.Clear();
        }

        /// <summary>
        /// 항목 추가 처리 (파생 클래스에서 호출)
        /// </summary>
        protected void MarkAsAdded(TKey key, TData data)
        {
            bool hadChanges = HasChanges;

            _workingCopies[key] = data;
            _addedKeys.Add(key);
            _deletedKeys.Remove(key);

            NotifyIfStateChanged(hadChanges);
        }

        /// <summary>
        /// 항목 수정 처리 (파생 클래스에서 호출)
        /// </summary>
        protected void MarkAsModifiedInternal(TKey key)
        {
            if (_addedKeys.Contains(key) || _deletedKeys.Contains(key))
                return;

            if (!_baselines.ContainsKey(key))
                return;

            bool hadChanges = HasChanges;
            _modifiedKeys.Add(key);
            NotifyIfStateChanged(hadChanges);
        }

        /// <summary>
        /// 항목 삭제 처리 (파생 클래스에서 호출)
        /// </summary>
        protected void MarkAsDeleted(TKey key)
        {
            bool hadChanges = HasChanges;

            if (_addedKeys.Contains(key))
            {
                // 추가된 항목 삭제 → 그냥 제거
                _addedKeys.Remove(key);
                _workingCopies.Remove(key);
            }
            else if (_baselines.ContainsKey(key))
            {
                // 기존 항목 삭제
                _deletedKeys.Add(key);
                _modifiedKeys.Remove(key);
                _workingCopies.Remove(key);
            }

            _propertyTracker.ClearChangesForKey(key);
            NotifyIfStateChanged(hadChanges);
        }

        /// <summary>
        /// Working Copy 가져오기 또는 생성
        /// </summary>
        protected TData GetOrCreateWorkingCopy(TKey key)
        {
            if (_workingCopies.TryGetValue(key, out var existing))
                return existing;

            if (!_baselines.TryGetValue(key, out var baseline))
                throw new KeyNotFoundException($"Item with key '{key}' not found.");

            var workingCopy = DeepCloner.Clone(baseline);
            _workingCopies[key] = workingCopy;
            return workingCopy;
        }

        /// <summary>
        /// 변경 상태 변화 알림
        /// </summary>
        protected void NotifyIfStateChanged(bool previousHasChanges)
        {
            if (previousHasChanges != HasChanges)
            {
                OnModifiedStateChanged?.Invoke(HasChanges);
            }
        }

        /// <summary>
        /// 이벤트 직접 발생 (파생 클래스에서 SaveAsync 후 사용)
        /// </summary>
        protected void RaiseModifiedStateChanged(bool hasChanges)
        {
            OnModifiedStateChanged?.Invoke(hasChanges);
        }

        /// <summary>
        /// 저장 후 Baseline 갱신
        /// </summary>
        protected void RefreshBaselinesAfterSave()
        {
            // Added → Baseline으로 이동
            foreach (var key in _addedKeys)
            {
                if (_workingCopies.TryGetValue(key, out var data))
                {
                    _baselines[key] = DeepCloner.Clone(data);
                }
            }

            // Modified → Baseline 갱신
            foreach (var key in _modifiedKeys)
            {
                if (_workingCopies.TryGetValue(key, out var data))
                {
                    _baselines[key] = DeepCloner.Clone(data);
                }
            }

            // Deleted → Baseline에서 제거
            foreach (var key in _deletedKeys)
            {
                _baselines.Remove(key);
            }

            // 상태 초기화
            _workingCopies.Clear();
            _addedKeys.Clear();
            _modifiedKeys.Clear();
            _deletedKeys.Clear();
            _propertyTracker.Clear();
        }

        #endregion
    }
}
