#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datra.Repositories
{
    /// <summary>
    /// 단일 데이터 객체용 Editable Repository 구현
    /// Property-level 변경 추적 지원
    /// </summary>
    /// <typeparam name="T">데이터 타입</typeparam>
    public abstract class EditableSingleRepository<T> : ISingleRepository<T>
        where T : class
    {
        private T? _baseline;
        private T? _current;
        private bool _isModified;
        private bool _isInitialized;

        private readonly Dictionary<string, PropertyChangeRecord> _propertyChanges = new();

        private class PropertyChangeRecord
        {
            public object? BaselineValue { get; set; }
            public object? CurrentValue { get; set; }
        }

        public event Action<bool>? OnModifiedStateChanged;

        #region IRepository

        public bool IsInitialized => _isInitialized;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _baseline = await LoadDataAsync();
            _current = _baseline != null ? DeepCloner.Clone(_baseline) : null;
            _isInitialized = true;
        }

        /// <summary>
        /// 파생 클래스에서 실제 데이터 로드 구현
        /// </summary>
        protected abstract Task<T?> LoadDataAsync();

        /// <summary>
        /// 파생 클래스에서 실제 데이터 저장 구현
        /// </summary>
        protected abstract Task SaveDataAsync(T data);

        #endregion

        #region ISingleRepository<T>

        public async Task<T?> GetAsync()
        {
            if (!_isInitialized)
                await InitializeAsync();

            return _current;
        }

        public T? Current => _current;
        public T? Baseline => _baseline;

        public void Set(T data)
        {
            bool hadChanges = HasChanges;

            _current = data;
            _isModified = true;

            // 전체 비교를 통해 실제 수정 여부 확인
            if (_baseline != null && DeepCloner.DeepEquals(_baseline, data))
            {
                _isModified = false;
                _propertyChanges.Clear();
            }

            NotifyIfStateChanged(hadChanges);
        }

        public bool IsPropertyModified(string propertyName)
        {
            return _propertyChanges.ContainsKey(propertyName);
        }

        public IEnumerable<string> GetModifiedProperties()
        {
            return _propertyChanges.Keys;
        }

        public object? GetPropertyBaseline(string propertyName)
        {
            if (_baseline == null)
                return null;

            return PropertyChangeTracker<string>.GetPropertyValue(_baseline, propertyName);
        }

        public void TrackPropertyChange(string propertyName, object? newValue)
        {
            if (_baseline == null)
                return;

            bool hadChanges = HasChanges;
            var baselineValue = PropertyChangeTracker<string>.GetPropertyValue(_baseline, propertyName);
            bool isPropertyModified = !DeepCloner.DeepEquals(baselineValue, newValue);

            if (isPropertyModified)
            {
                _propertyChanges[propertyName] = new PropertyChangeRecord
                {
                    BaselineValue = baselineValue,
                    CurrentValue = newValue
                };
                _isModified = true;
            }
            else
            {
                _propertyChanges.Remove(propertyName);

                // 모든 속성이 원복되었으면 수정 상태 해제
                if (_propertyChanges.Count == 0)
                {
                    _isModified = false;
                }
            }

            // Current 객체의 속성 값 업데이트
            if (_current != null)
            {
                PropertyChangeTracker<string>.SetPropertyValue(_current, propertyName, newValue);
            }

            NotifyIfStateChanged(hadChanges);
        }

        public void RevertProperty(string propertyName)
        {
            if (_baseline == null || _current == null)
                return;

            bool hadChanges = HasChanges;

            // Baseline 값으로 복원
            var baselineValue = PropertyChangeTracker<string>.GetPropertyValue(_baseline, propertyName);
            PropertyChangeTracker<string>.SetPropertyValue(_current, propertyName, baselineValue);

            // Property 변경 기록 제거
            _propertyChanges.Remove(propertyName);

            // 모든 속성이 원복되었으면 수정 상태 해제
            if (_propertyChanges.Count == 0)
            {
                _isModified = false;
            }

            NotifyIfStateChanged(hadChanges);
        }

        #endregion

        #region IChangeTracking

        public bool HasChanges => _isModified;

        public void Revert()
        {
            bool hadChanges = HasChanges;

            _current = _baseline != null ? DeepCloner.Clone(_baseline) : null;
            _isModified = false;
            _propertyChanges.Clear();

            NotifyIfStateChanged(hadChanges);
        }

        public async Task SaveAsync()
        {
            if (_current == null)
                throw new InvalidOperationException("No data to save.");

            await SaveDataAsync(_current);

            // 저장 후 Baseline 갱신
            _baseline = DeepCloner.Clone(_current);
            _isModified = false;
            _propertyChanges.Clear();

            OnModifiedStateChanged?.Invoke(false);
        }

        #endregion

        #region Helpers

        private void NotifyIfStateChanged(bool previousHasChanges)
        {
            if (previousHasChanges != HasChanges)
            {
                OnModifiedStateChanged?.Invoke(HasChanges);
            }
        }

        /// <summary>
        /// Baseline을 직접 설정 (테스트 또는 특수 케이스용)
        /// </summary>
        protected void SetBaselineDirectly(T data)
        {
            _baseline = DeepCloner.Clone(data);
            _current = DeepCloner.Clone(data);
            _isInitialized = true;
            _isModified = false;
            _propertyChanges.Clear();
        }

        /// <summary>
        /// 외부 변경 사항 반영 (서버에서 데이터가 변경된 경우 등)
        /// </summary>
        public void RefreshBaseline(T newBaseline)
        {
            bool hadChanges = HasChanges;

            _baseline = DeepCloner.Clone(newBaseline);

            // 수정 중이 아니면 Current도 갱신
            if (!_isModified)
            {
                _current = DeepCloner.Clone(newBaseline);
            }
            else
            {
                // 수정 중이면 property-level 비교 다시 수행
                RecalculatePropertyChanges();
            }

            NotifyIfStateChanged(hadChanges);
        }

        private void RecalculatePropertyChanges()
        {
            if (_baseline == null || _current == null)
                return;

            var oldPropertyChanges = new Dictionary<string, PropertyChangeRecord>(_propertyChanges);
            _propertyChanges.Clear();

            foreach (var propertyName in oldPropertyChanges.Keys)
            {
                var baselineValue = PropertyChangeTracker<string>.GetPropertyValue(_baseline, propertyName);
                var currentValue = PropertyChangeTracker<string>.GetPropertyValue(_current, propertyName);

                if (!DeepCloner.DeepEquals(baselineValue, currentValue))
                {
                    _propertyChanges[propertyName] = new PropertyChangeRecord
                    {
                        BaselineValue = baselineValue,
                        CurrentValue = currentValue
                    };
                }
            }

            _isModified = _propertyChanges.Count > 0;
        }

        #endregion
    }
}
