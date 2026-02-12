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
    /// Editable data source for key-value (table) data.
    /// Provides a transactional editing layer that doesn't modify the repository until Save().
    ///
    /// Inherits from EditableDataSourceBase for consistent event notification patterns.
    /// All state-changing operations use ExecuteWithNotification to ensure events are fired.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TData">The data type (must implement ITableData&lt;TKey&gt;)</typeparam>
    public class EditableKeyValueDataSource<TKey, TData> : EditableDataSourceBase, IEditableDataSource<TKey, TData>
        where TKey : notnull
        where TData : class, ITableData<TKey>
    {
        private readonly ITableRepository<TKey, TData> _repository;

        // Baseline snapshot (taken at initialization, represents saved state)
        private readonly Dictionary<TKey, TData> _baseline = new();

        // Working copies of modified items
        private readonly Dictionary<TKey, TData> _workingCopies = new();

        // Tracking sets
        private readonly HashSet<TKey> _addedKeys = new();
        private readonly HashSet<TKey> _deletedKeys = new();
        private readonly HashSet<TKey> _modifiedKeys = new();

        // Property-level change tracking
        private readonly Dictionary<(TKey key, string propertyName), PropertyChangeRecord> _propertyChanges = new();

        private class PropertyChangeRecord
        {
            public object? BaselineValue { get; set; }
            public object? CurrentValue { get; set; }
        }

        public EditableKeyValueDataSource(ITableRepository<TKey, TData> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            InitializeBaseline();
        }

        #region Initialization

        private void InitializeBaseline()
        {
            _baseline.Clear();
            _workingCopies.Clear();
            _addedKeys.Clear();
            _deletedKeys.Clear();
            _modifiedKeys.Clear();
            _propertyChanges.Clear();

            foreach (var kvp in _repository.LoadedItems)
            {
                _baseline[kvp.Key] = DeepCloner.Clone(kvp.Value);
            }
        }

        protected override void RefreshBaselineInternal()
        {
            InitializeBaseline();
        }

        #endregion

        #region IEditableDataSource Implementation

        public override bool HasModifications =>
            _addedKeys.Count > 0 || _deletedKeys.Count > 0 || _modifiedKeys.Count > 0;

        public override int Count
        {
            get
            {
                int count = _baseline.Count;
                count += _addedKeys.Count;
                count -= _deletedKeys.Count;
                return count;
            }
        }

        public override IEnumerable<object> EnumerateItems()
        {
            foreach (var kvp in ((IEditableDataSource<TKey, TData>)this).EnumerateItems())
            {
                yield return kvp.Value;
            }
        }

        IEnumerable<KeyValuePair<TKey, TData>> IEditableDataSource<TKey, TData>.EnumerateItems()
        {
            // Items from baseline (excluding deleted)
            // Always return working copies to prevent baseline mutation during editing
            foreach (var kvp in _baseline)
            {
                if (_deletedKeys.Contains(kvp.Key))
                    continue;

                // Get or create working copy - this ensures baseline is never exposed for direct modification
                if (!_workingCopies.TryGetValue(kvp.Key, out var workingCopy))
                {
                    workingCopy = DeepCloner.Clone(kvp.Value);
                    _workingCopies[kvp.Key] = workingCopy;
                }
                yield return new KeyValuePair<TKey, TData>(kvp.Key, workingCopy);
            }

            // Added items
            foreach (var key in _addedKeys)
            {
                if (_workingCopies.TryGetValue(key, out var addedItem))
                    yield return new KeyValuePair<TKey, TData>(key, addedItem);
            }
        }

        public TData GetItem(TKey key)
        {
            if (!TryGetItem(key, out var value))
                throw new KeyNotFoundException($"Item with key '{key}' not found or deleted.");
            return value!;
        }

        public bool TryGetItem(TKey key, out TData? value)
        {
            // Check if deleted
            if (_deletedKeys.Contains(key))
            {
                value = null;
                return false;
            }

            // Check working copies first (modified or added items)
            if (_workingCopies.TryGetValue(key, out value))
                return true;

            // Check baseline
            if (_baseline.TryGetValue(key, out value))
                return true;

            value = null;
            return false;
        }

        public bool ContainsKey(TKey key)
        {
            if (_deletedKeys.Contains(key))
                return false;

            return _workingCopies.ContainsKey(key) || _baseline.ContainsKey(key);
        }

        public override ItemState GetItemState(object key)
        {
            if (key is TKey typedKey)
                return GetItemState(typedKey);
            return ItemState.Unchanged;
        }

        public ItemState GetItemState(TKey key)
        {
            if (_deletedKeys.Contains(key))
                return ItemState.Deleted;

            if (_addedKeys.Contains(key))
                return ItemState.Added;

            if (_modifiedKeys.Contains(key))
                return ItemState.Modified;

            return ItemState.Unchanged;
        }

        public TData GetWorkingCopy(TKey key)
        {
            // If already have a working copy, return it
            if (_workingCopies.TryGetValue(key, out var existing))
                return existing;

            // Create working copy from baseline
            if (!_baseline.TryGetValue(key, out var baseline))
                throw new KeyNotFoundException($"Item with key '{key}' not found in baseline.");

            var workingCopy = DeepCloner.Clone(baseline);
            _workingCopies[key] = workingCopy;
            return workingCopy;
        }

        public void MarkModified(TKey key)
        {
            if (_addedKeys.Contains(key))
                return; // Added items are already tracked

            if (_deletedKeys.Contains(key))
                return; // Deleted items shouldn't be modified

            if (!_baseline.ContainsKey(key) && !_workingCopies.ContainsKey(key))
                return; // Item doesn't exist

            ExecuteWithNotification(() => _modifiedKeys.Add(key));
        }

        public void TrackPropertyChange(TKey key, string propertyName, object? newValue, out bool isPropertyModified)
        {
            bool hadModifications = HasModifications;
            isPropertyModified = false;

            // Get or create working copy
            if (!_workingCopies.TryGetValue(key, out var workingCopy))
            {
                if (!_baseline.TryGetValue(key, out var baseline))
                {
                    // New item - no baseline to compare
                    if (_addedKeys.Contains(key))
                    {
                        isPropertyModified = true;
                    }
                    return;
                }
                workingCopy = DeepCloner.Clone(baseline);
                _workingCopies[key] = workingCopy;
            }

            // Get baseline value
            object? baselineValue = null;
            if (_baseline.TryGetValue(key, out var baselineItem))
            {
                var propInfo = typeof(TData).GetProperty(propertyName);
                if (propInfo != null)
                    baselineValue = propInfo.GetValue(baselineItem);
            }

            // Compare values
            bool isEqual = DeepEqualsValues(baselineValue, newValue);
            var changeKey = (key, propertyName);

            if (!isEqual)
            {
                _propertyChanges[changeKey] = new PropertyChangeRecord
                {
                    BaselineValue = baselineValue,
                    CurrentValue = newValue
                };
                _modifiedKeys.Add(key);
                isPropertyModified = true;
            }
            else
            {
                _propertyChanges.Remove(changeKey);

                // Check if there are any other property changes for this key
                bool hasOtherChanges = _propertyChanges.Keys.Any(k => k.key.Equals(key));
                if (!hasOtherChanges)
                {
                    _modifiedKeys.Remove(key);
                    // Remove working copy if back to baseline
                    if (!_addedKeys.Contains(key))
                    {
                        _workingCopies.Remove(key);
                    }
                }
            }

            // Update working copy property
            var prop = typeof(TData).GetProperty(propertyName);
            if (prop != null && workingCopy != null)
            {
                prop.SetValue(workingCopy, newValue);
            }

            // Notify if modification state changed
            NotifyIfStateChanged(hadModifications);
        }

        public void Add(TKey key, TData value)
        {
            if (ContainsKey(key))
                throw new InvalidOperationException($"Item with key '{key}' already exists.");

            ExecuteWithNotification(() =>
            {
                _workingCopies[key] = DeepCloner.Clone(value);
                _addedKeys.Add(key);
                _deletedKeys.Remove(key);
            });
        }

        public void Delete(TKey key)
        {
            ExecuteWithNotification(() =>
            {
                if (_addedKeys.Contains(key))
                {
                    // Was added in this session - just remove
                    _addedKeys.Remove(key);
                    _workingCopies.Remove(key);

                    // Remove property changes
                    RemovePropertyChangesForKey(key);
                }
                else if (_baseline.ContainsKey(key))
                {
                    // Exists in baseline - mark for deletion
                    _deletedKeys.Add(key);
                    _modifiedKeys.Remove(key);
                    _workingCopies.Remove(key);

                    // Remove property changes
                    RemovePropertyChangesForKey(key);
                }
            });
        }

        public TData? GetBaselineValue(TKey key)
        {
            return _baseline.TryGetValue(key, out var value) ? DeepCloner.Clone(value) : null;
        }

        public override bool IsPropertyModified(object key, string propertyName)
        {
            if (key is TKey typedKey)
                return IsPropertyModified(typedKey, propertyName);
            return false;
        }

        public bool IsPropertyModified(TKey key, string propertyName)
        {
            return _propertyChanges.ContainsKey((key, propertyName));
        }

        public override IEnumerable<string> GetModifiedProperties(object key)
        {
            if (key is TKey typedKey)
                return GetModifiedProperties(typedKey);
            return Enumerable.Empty<string>();
        }

        public IEnumerable<string> GetModifiedProperties(TKey key)
        {
            return _propertyChanges.Keys
                .Where(k => k.key.Equals(key))
                .Select(k => k.propertyName);
        }

        public override object? GetPropertyBaselineValue(object key, string propertyName)
        {
            if (key is TKey typedKey)
                return GetPropertyBaselineValue(typedKey, propertyName);
            return null;
        }

        public object? GetPropertyBaselineValue(TKey key, string propertyName)
        {
            if (!_baseline.TryGetValue(key, out var baselineItem))
                return null;

            var propInfo = typeof(TData).GetProperty(propertyName);
            return propInfo?.GetValue(baselineItem);
        }

        /// <summary>
        /// Get the key from an item. Supports various item wrapper types.
        /// </summary>
        public override object? GetItemKey(object item)
        {
            if (item == null) return null;

            // Handle KeyValuePair<TKey, TData>
            if (item is KeyValuePair<TKey, TData> kvp)
            {
                return kvp.Key;
            }

            // Handle direct TData item
            if (item is TData data)
            {
                return data.Id;
            }

            // Try to extract from generic KeyValuePair
            var itemType = item.GetType();
            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var keyProp = itemType.GetProperty("Key");
                return keyProp?.GetValue(item);
            }

            // Try to get Id property
            var idProp = item.GetType().GetProperty("Id");
            return idProp?.GetValue(item);
        }

        /// <summary>
        /// Track property change (non-generic version for IEditableDataSource).
        /// </summary>
        public override void TrackPropertyChange(object key, string propertyName, object? newValue, out bool isPropertyModified)
        {
            if (key is TKey typedKey)
            {
                TrackPropertyChange(typedKey, propertyName, newValue, out isPropertyModified);
            }
            else
            {
                isPropertyModified = false;
            }
        }

        #endregion

        #region Revert

        protected override void RevertInternal()
        {
            _workingCopies.Clear();
            _addedKeys.Clear();
            _deletedKeys.Clear();
            _modifiedKeys.Clear();
            _propertyChanges.Clear();
        }

        #endregion

        #region Save

        protected override async Task SaveInternalAsync()
        {
            // Apply deletions
            foreach (var key in _deletedKeys)
            {
                _repository.Remove(key);
            }

            // Apply additions
            foreach (var key in _addedKeys)
            {
                if (_workingCopies.TryGetValue(key, out var addedItem))
                {
                    _repository.Add(addedItem);
                }
            }

            // Apply modifications (update baseline items with working copy values)
            foreach (var key in _modifiedKeys)
            {
                if (_addedKeys.Contains(key))
                    continue; // Already handled

                if (_workingCopies.TryGetValue(key, out var modifiedItem))
                {
                    // Get the actual repository item and update its properties
                    var repoItem = _repository.TryGetLoaded(key);
                    if (repoItem != null)
                    {
                        CopyProperties(modifiedItem, repoItem);
                    }
                }
            }

            // Save to storage
            await _repository.SaveAsync();
        }

        #endregion

        #region Helpers

        private void RemovePropertyChangesForKey(TKey key)
        {
            var keysToRemove = _propertyChanges.Keys.Where(k => k.key.Equals(key)).ToList();
            foreach (var k in keysToRemove)
            {
                _propertyChanges.Remove(k);
            }
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
