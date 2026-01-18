#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.Serializers;
using Newtonsoft.Json;

namespace Datra.Editor.DataSources
{
    /// <summary>
    /// Editable data source for single data (one item).
    /// Uses a singleton key pattern with a constant key.
    /// Provides a transactional editing layer that doesn't modify the repository until Save().
    /// </summary>
    /// <typeparam name="TData">The data type</typeparam>
    public class EditableSingleDataSource<TData> : IEditableDataSource<string, TData>
        where TData : class
    {
        private static readonly JsonSerializerSettings _jsonSettings = DatraJsonSettings.CreateForClone();

        /// <summary>
        /// Constant key for the single data item
        /// </summary>
        public const string SingleKey = "__single__";

        private readonly ISingleRepository<TData> _repository;

        // Baseline snapshot
        private TData? _baseline;

        // Working copy (only created if modifications are made)
        private TData? _workingCopy;

        // Property-level change tracking
        private readonly Dictionary<string, PropertyChangeRecord> _propertyChanges = new();

        private class PropertyChangeRecord
        {
            public object? BaselineValue { get; set; }
            public object? CurrentValue { get; set; }
        }

        public event Action<bool>? OnModifiedStateChanged;

        public EditableSingleDataSource(ISingleRepository<TData> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            InitializeBaseline();
        }

        #region Initialization

        private void InitializeBaseline()
        {
            _propertyChanges.Clear();
            _workingCopy = null;

            if (_repository.IsInitialized && _repository.Current != null)
            {
                _baseline = DeepClone(_repository.Current);
            }
            else
            {
                _baseline = null;
            }
        }

        /// <summary>
        /// Refresh baseline from repository (call after external reload)
        /// </summary>
        public void RefreshBaseline()
        {
            InitializeBaseline();
            OnModifiedStateChanged?.Invoke(false);
        }

        #endregion

        #region IEditableDataSource Implementation

        public bool HasModifications => _propertyChanges.Count > 0;

        public int Count => _baseline != null ? 1 : 0;

        IEnumerable<object> IEditableDataSource.EnumerateItems()
        {
            // Always return working copy to prevent baseline mutation during editing
            // This matches EditableKeyValueDataSource behavior
            var data = GetOrCreateWorkingCopy();
            if (data != null)
                yield return data;
        }

        /// <summary>
        /// Get or create working copy for editing.
        /// This ensures baseline is never exposed for direct modification.
        /// </summary>
        private TData? GetOrCreateWorkingCopy()
        {
            if (_baseline == null) return null;

            if (_workingCopy == null)
            {
                _workingCopy = DeepClone(_baseline);
            }
            return _workingCopy;
        }

        public IEnumerable<KeyValuePair<string, TData>> EnumerateItems()
        {
            // Always return working copy to prevent baseline mutation during editing
            var data = GetOrCreateWorkingCopy();
            if (data != null)
                yield return new KeyValuePair<string, TData>(SingleKey, data);
        }

        /// <summary>
        /// Get the current data (working copy if modified, otherwise baseline)
        /// </summary>
        public TData? GetCurrentData()
        {
            return _workingCopy ?? _baseline;
        }

        public TData GetItem(string key)
        {
            if (key != SingleKey)
                throw new KeyNotFoundException($"Invalid key '{key}'. Use SingleKey for single data.");

            var data = GetCurrentData();
            if (data == null)
                throw new InvalidOperationException("Data has not been loaded.");

            return data;
        }

        public bool TryGetItem(string key, out TData? value)
        {
            if (key != SingleKey)
            {
                value = null;
                return false;
            }

            value = GetCurrentData();
            return value != null;
        }

        public bool ContainsKey(string key)
        {
            return key == SingleKey && _baseline != null;
        }

        ItemState IEditableDataSource.GetItemState(object key)
        {
            if (key is string strKey && strKey == SingleKey)
                return GetItemState(strKey);
            return ItemState.Unchanged;
        }

        public ItemState GetItemState(string key)
        {
            if (key != SingleKey)
                return ItemState.Unchanged;

            if (HasModifications)
                return ItemState.Modified;

            return ItemState.Unchanged;
        }

        public TData GetWorkingCopy(string key)
        {
            if (key != SingleKey)
                throw new KeyNotFoundException($"Invalid key '{key}'. Use SingleKey for single data.");

            if (_workingCopy != null)
                return _workingCopy;

            if (_baseline == null)
                throw new InvalidOperationException("Data has not been loaded.");

            _workingCopy = DeepClone(_baseline);
            return _workingCopy;
        }

        public void MarkModified(string key)
        {
            if (key != SingleKey || _baseline == null)
                return;

            // Ensure we have a working copy
            if (_workingCopy == null)
            {
                _workingCopy = DeepClone(_baseline);
            }
        }

        public void TrackPropertyChange(string key, string propertyName, object? newValue, out bool isPropertyModified)
        {
            if (key != SingleKey)
            {
                isPropertyModified = false;
                return;
            }

            bool hadModifications = HasModifications;
            isPropertyModified = false;

            // Ensure we have a working copy
            if (_workingCopy == null && _baseline != null)
            {
                _workingCopy = DeepClone(_baseline);
            }

            // Get baseline value
            object? baselineValue = null;
            if (_baseline != null)
            {
                var propInfo = typeof(TData).GetProperty(propertyName);
                if (propInfo != null)
                    baselineValue = propInfo.GetValue(_baseline);
            }

            // Compare values
            bool isEqual = DeepEqualsValues(baselineValue, newValue);

            if (!isEqual)
            {
                _propertyChanges[propertyName] = new PropertyChangeRecord
                {
                    BaselineValue = baselineValue,
                    CurrentValue = newValue
                };
                isPropertyModified = true;
            }
            else
            {
                _propertyChanges.Remove(propertyName);

                // If no more changes, remove working copy
                if (_propertyChanges.Count == 0)
                {
                    _workingCopy = null;
                }
            }

            // Update working copy property
            if (_workingCopy != null)
            {
                var prop = typeof(TData).GetProperty(propertyName);
                if (prop != null)
                {
                    prop.SetValue(_workingCopy, newValue);
                }
            }

            if (hadModifications != HasModifications)
                OnModifiedStateChanged?.Invoke(HasModifications);
        }

        public void Add(string key, TData value)
        {
            throw new NotSupportedException("Cannot add items to single data source.");
        }

        public void Delete(string key)
        {
            throw new NotSupportedException("Cannot delete items from single data source.");
        }

        public TData? GetBaselineValue(string key)
        {
            if (key != SingleKey)
                return null;

            return _baseline != null ? DeepClone(_baseline) : null;
        }

        bool IEditableDataSource.IsPropertyModified(object key, string propertyName)
        {
            if (key is string strKey && strKey == SingleKey)
                return IsPropertyModified(strKey, propertyName);
            return false;
        }

        public bool IsPropertyModified(string key, string propertyName)
        {
            if (key != SingleKey)
                return false;

            return _propertyChanges.ContainsKey(propertyName);
        }

        IEnumerable<string> IEditableDataSource.GetModifiedProperties(object key)
        {
            if (key is string strKey && strKey == SingleKey)
                return GetModifiedProperties(strKey);
            return Enumerable.Empty<string>();
        }

        public IEnumerable<string> GetModifiedProperties(string key)
        {
            if (key != SingleKey)
                return Enumerable.Empty<string>();

            return _propertyChanges.Keys;
        }

        object? IEditableDataSource.GetPropertyBaselineValue(object key, string propertyName)
        {
            if (key is string strKey && strKey == SingleKey)
                return GetPropertyBaselineValue(strKey, propertyName);
            return null;
        }

        public object? GetPropertyBaselineValue(string key, string propertyName)
        {
            if (key != SingleKey || _baseline == null)
                return null;

            var propInfo = typeof(TData).GetProperty(propertyName);
            return propInfo?.GetValue(_baseline);
        }

        /// <summary>
        /// Get the key for an item. For single data, always returns SingleKey.
        /// Since single data only has one item, we always return the constant key.
        /// </summary>
        public object? GetItemKey(object item)
        {
            // For single data, there's only one item, so always return SingleKey
            // We don't need to verify the type - if someone is calling this with an item,
            // they got it from EnumerateItems() which only returns our single data item.
            if (item == null) return null;
            return SingleKey;
        }

        /// <summary>
        /// Track property change (non-generic version for IEditableDataSource).
        /// </summary>
        public void TrackPropertyChange(object key, string propertyName, object? newValue, out bool isPropertyModified)
        {
            // For single data, accept both SingleKey and "single" for backward compatibility
            if (key is string strKey && (strKey == SingleKey || strKey == "single"))
            {
                TrackPropertyChange(SingleKey, propertyName, newValue, out isPropertyModified);
            }
            else
            {
                isPropertyModified = false;
            }
        }

        #endregion

        #region Revert

        public void Revert()
        {
            bool hadModifications = HasModifications;

            _workingCopy = null;
            _propertyChanges.Clear();

            if (hadModifications)
                OnModifiedStateChanged?.Invoke(false);
        }

        #endregion

        #region Save

        public async Task SaveAsync()
        {
            if (_workingCopy != null && _repository.Current != null)
            {
                // Copy properties from working copy to repository's data
                var repoData = _repository.Current;
                CopyProperties(_workingCopy, repoData);
            }

            await _repository.SaveAsync();

            // Refresh baseline
            RefreshBaseline();
        }

        #endregion

        #region Helpers

        private TData DeepClone(TData value)
        {
            if (value == null) return null!;

            try
            {
                var json = JsonConvert.SerializeObject(value, _jsonSettings);
                return JsonConvert.DeserializeObject<TData>(json, _jsonSettings)!;
            }
            catch
            {
                return value;
            }
        }

        private static bool DeepEqualsValues(object? a, object? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            if (a.GetType().IsValueType || a is string)
                return a.Equals(b);

            try
            {
                var jsonA = JsonConvert.SerializeObject(a);
                var jsonB = JsonConvert.SerializeObject(b);
                return jsonA == jsonB;
            }
            catch
            {
                return ReferenceEquals(a, b);
            }
        }

        private static void CopyProperties(TData source, TData target)
        {
            var properties = typeof(TData).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);

            foreach (var prop in properties)
            {
                var value = prop.GetValue(source);
                prop.SetValue(target, value);
            }
        }

        #endregion
    }
}
