#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datra.Repositories.Runtime
{
    /// <summary>
    /// 단일 데이터용 읽기 전용 Repository (Runtime용)
    /// 변경 추적 비활성, 쓰기 메서드는 NotSupportedException
    /// </summary>
    /// <typeparam name="T">데이터 타입</typeparam>
    public abstract class RuntimeSingleRepository<T> : ISingleRepository<T>
        where T : class
    {
        private T? _data;
        private bool _isInitialized;

        public event Action<bool>? OnModifiedStateChanged;

        #region IRepository

        public bool IsInitialized => _isInitialized;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _data = await LoadDataAsync();
            _isInitialized = true;
        }

        protected abstract Task<T?> LoadDataAsync();

        #endregion

        #region ISingleRepository<T> - 읽기

        public async Task<T?> GetAsync()
        {
            if (!_isInitialized)
                await InitializeAsync();

            return _data;
        }

        public T? Current => _data;
        public T? Baseline => _data;

        #endregion

        #region ISingleRepository<T> - 쓰기 (비활성)

        public void Set(T data) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void TrackPropertyChange(string propertyName, object? newValue) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        public void RevertProperty(string propertyName) =>
            throw new NotSupportedException("Runtime repository is read-only.");

        #endregion

        #region ISingleRepository<T> - 변경 추적 (항상 false)

        public bool IsPropertyModified(string propertyName) => false;
        public IEnumerable<string> GetModifiedProperties() => Array.Empty<string>();
        public object? GetPropertyBaseline(string propertyName) => null;

        #endregion

        #region IChangeTracking (항상 false)

        public bool HasChanges => false;

        public void Revert() { }

        public Task SaveAsync() =>
            throw new NotSupportedException("Runtime repository is read-only.");

        #endregion
    }
}
