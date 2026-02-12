#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.Repositories;

namespace Datra.Editor.DataSources
{
    /// <summary>
    /// Editable data source for single data (one item).
    /// Uses a singleton key pattern with a constant key.
    /// Provides a transactional editing layer that doesn't modify the repository until Save().
    /// </summary>
    /// <typeparam name="TData">The data type</typeparam>
    public class EditableSingleDataSource<TData> : EditableDataSourceBase, IEditableDataSource<string, TData>
        where TData : class
    {
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

        public EditableSingleDataSource(ISingleRepository<TData> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            InitializeBaselineInternal();
        }

        #region Initialization

        private void InitializeBaselineInternal()
        {
            _propertyChanges.Clear();
            _workingCopy = null;

            if (_repository.IsInitialized && _repository.Current != null)
            {
                _baseline = DeepCloner.Clone(_repository.Current);
            }
            else
            {
                _baseline = null;
            }
        }

        #endregion

        #region EditableDataSourceBase Implementation

        public override bool HasModifications => _propertyChanges.Count > 0;

        public override int Count => _baseline != null ? 1 : 0;

        public override IEnumerable<object> EnumerateItems()
        {
            // Always return working copy to prevent baseline mutation during editing
            var data = GetOrCreateWorkingCopy();
            if (data != null)
                yield return data;
        }

        public override ItemState GetItemState(object key)
        {
            if (key is string strKey && strKey == SingleKey)
                return GetItemState(strKey);
            return ItemState.Unchanged;
        }

        public override bool IsPropertyModified(object key, string propertyName)
        {
            if (key is string strKey && strKey == SingleKey)
                return IsPropertyModified(strKey, propertyName);
            return false;
        }

        public override IEnumerable<string> GetModifiedProperties(object key)
        {
            if (key is string strKey && strKey == SingleKey)
                return GetModifiedProperties(strKey);
            return Enumerable.Empty<string>();
        }

        public override object? GetPropertyBaselineValue(object key, string propertyName)
        {
            if (key is string strKey && strKey == SingleKey)
                return GetPropertyBaselineValue(strKey, propertyName);
            return null;
        }

        public override ChangeSummary GetChangeSummary()
        {
            if (_propertyChanges.Count == 0)
                return new ChangeSummary();

            return new ChangeSummary
            {
                Entries = new List<ChangeEntry>
                {
                    new ChangeEntry
                    {
                        Key = SingleKey,
                        State = ItemState.Modified,
                        ModifiedProperties = _propertyChanges.Keys.ToList()
                    }
                }
            };
        }

        public override object? GetItemKey(object item)
        {
            if (item == null) return null;
            return SingleKey;
        }

        public override void TrackPropertyChange(object key, string propertyName, object? newValue, out bool isPropertyModified)
        {
            if (key is string strKey && (strKey == SingleKey || strKey == "single"))
            {
                TrackPropertyChange(SingleKey, propertyName, newValue, out isPropertyModified);
            }
            else
            {
                isPropertyModified = false;
            }
        }

        protected override void RevertInternal()
        {
            _workingCopy = null;
            _propertyChanges.Clear();
        }

        protected override async Task SaveInternalAsync()
        {
            if (_workingCopy != null && _repository.Current != null)
            {
                var repoData = _repository.Current;
                CopyProperties(_workingCopy, repoData);
            }

            await _repository.SaveAsync();
        }

        protected override void RefreshBaselineInternal()
        {
            InitializeBaselineInternal();
        }

        #endregion

        #region IEditableDataSource<string, TData> Implementation

        IEnumerable<KeyValuePair<string, TData>> IEditableDataSource<string, TData>.EnumerateItems()
        {
            var data = GetOrCreateWorkingCopy();
            if (data != null)
                yield return new KeyValuePair<string, TData>(SingleKey, data);
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

            _workingCopy = DeepCloner.Clone(_baseline);
            return _workingCopy;
        }

        public void MarkModified(string key)
        {
            if (key != SingleKey || _baseline == null)
                return;

            if (_workingCopy == null)
            {
                _workingCopy = DeepCloner.Clone(_baseline);
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

            if (_workingCopy == null && _baseline != null)
            {
                _workingCopy = DeepCloner.Clone(_baseline);
            }

            object? baselineValue = null;
            if (_baseline != null)
            {
                var propInfo = typeof(TData).GetProperty(propertyName);
                if (propInfo != null)
                    baselineValue = propInfo.GetValue(_baseline);
            }

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

                if (_propertyChanges.Count == 0)
                {
                    _workingCopy = null;
                }
            }

            if (_workingCopy != null)
            {
                var prop = typeof(TData).GetProperty(propertyName);
                if (prop != null)
                {
                    prop.SetValue(_workingCopy, newValue);
                }
            }

            NotifyIfStateChanged(hadModifications);
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

            return _baseline != null ? DeepCloner.Clone(_baseline) : null;
        }

        public bool IsPropertyModified(string key, string propertyName)
        {
            if (key != SingleKey)
                return false;

            return _propertyChanges.ContainsKey(propertyName);
        }

        public IEnumerable<string> GetModifiedProperties(string key)
        {
            if (key != SingleKey)
                return Enumerable.Empty<string>();

            return _propertyChanges.Keys;
        }

        public object? GetPropertyBaselineValue(string key, string propertyName)
        {
            if (key != SingleKey || _baseline == null)
                return null;

            var propInfo = typeof(TData).GetProperty(propertyName);
            return propInfo?.GetValue(_baseline);
        }

        #endregion

        #region Helpers

        private TData? GetOrCreateWorkingCopy()
        {
            if (_baseline == null) return null;

            if (_workingCopy == null)
            {
                _workingCopy = DeepCloner.Clone(_baseline);
            }
            return _workingCopy;
        }

        public TData? GetCurrentData()
        {
            return _workingCopy ?? _baseline;
        }

        private static bool DeepEqualsValues(object? a, object? b)
        {
            return DeepCloner.DeepEquals(a, b);
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
