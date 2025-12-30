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
    /// Editable data source for key-value (table) data.
    /// Provides a transactional editing layer that doesn't modify the repository until Save().
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TData">The data type (must implement ITableData&lt;TKey&gt;)</typeparam>
    public class EditableKeyValueDataSource<TKey, TData> : IEditableDataSource<TKey, TData>
        where TKey : notnull
        where TData : class, ITableData<TKey>
    {
        private static readonly JsonSerializerSettings _jsonSettings = DatraJsonSettings.CreateForClone();

        private readonly IKeyValueDataRepository<TKey, TData> _repository;

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

        public event Action<bool>? OnModifiedStateChanged;

        public EditableKeyValueDataSource(IKeyValueDataRepository<TKey, TData> repository)
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

            foreach (var kvp in _repository.GetAll())
            {
                _baseline[kvp.Key] = DeepClone(kvp.Value);
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

        public bool HasModifications =>
            _addedKeys.Count > 0 || _deletedKeys.Count > 0 || _modifiedKeys.Count > 0;

        public int Count
        {
            get
            {
                int count = _baseline.Count;
                count += _addedKeys.Count;
                count -= _deletedKeys.Count;
                return count;
            }
        }

        IEnumerable<object> IEditableDataSource.EnumerateItems()
        {
            foreach (var kvp in EnumerateItems())
            {
                yield return kvp.Value;
            }
        }

        public IEnumerable<KeyValuePair<TKey, TData>> EnumerateItems()
        {
            // Items from baseline (excluding deleted, using working copy if modified)
            foreach (var kvp in _baseline)
            {
                if (_deletedKeys.Contains(kvp.Key))
                    continue;

                if (_workingCopies.TryGetValue(kvp.Key, out var workingCopy))
                    yield return new KeyValuePair<TKey, TData>(kvp.Key, workingCopy);
                else
                    yield return kvp;
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

        ItemState IEditableDataSource.GetItemState(object key)
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

            var workingCopy = DeepClone(baseline);
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

            bool hadModifications = HasModifications;

            _modifiedKeys.Add(key);

            if (!hadModifications && HasModifications)
                OnModifiedStateChanged?.Invoke(true);
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
                workingCopy = DeepClone(baseline);
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
            if (hadModifications != HasModifications)
                OnModifiedStateChanged?.Invoke(HasModifications);
        }

        public void Add(TKey key, TData value)
        {
            if (ContainsKey(key))
                throw new InvalidOperationException($"Item with key '{key}' already exists.");

            bool hadModifications = HasModifications;

            _workingCopies[key] = DeepClone(value);
            _addedKeys.Add(key);
            _deletedKeys.Remove(key);

            if (!hadModifications)
                OnModifiedStateChanged?.Invoke(true);
        }

        public void Delete(TKey key)
        {
            bool hadModifications = HasModifications;

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

            if (hadModifications != HasModifications)
                OnModifiedStateChanged?.Invoke(HasModifications);
        }

        public TData? GetBaselineValue(TKey key)
        {
            return _baseline.TryGetValue(key, out var value) ? DeepClone(value) : null;
        }

        bool IEditableDataSource.IsPropertyModified(object key, string propertyName)
        {
            if (key is TKey typedKey)
                return IsPropertyModified(typedKey, propertyName);
            return false;
        }

        public bool IsPropertyModified(TKey key, string propertyName)
        {
            return _propertyChanges.ContainsKey((key, propertyName));
        }

        IEnumerable<string> IEditableDataSource.GetModifiedProperties(object key)
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

        object? IEditableDataSource.GetPropertyBaselineValue(object key, string propertyName)
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

        #endregion

        #region Revert

        public void Revert()
        {
            bool hadModifications = HasModifications;

            _workingCopies.Clear();
            _addedKeys.Clear();
            _deletedKeys.Clear();
            _modifiedKeys.Clear();
            _propertyChanges.Clear();

            if (hadModifications)
                OnModifiedStateChanged?.Invoke(false);
        }

        #endregion

        #region Save

        public async Task SaveAsync()
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
                    var repoItem = _repository.TryGetById(key);
                    if (repoItem != null)
                    {
                        CopyProperties(modifiedItem, repoItem);
                    }
                }
            }

            // Save to storage
            await _repository.SaveAsync();

            // Refresh baseline from repository
            RefreshBaseline();
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
                // Fallback to returning the same reference (not ideal but safe)
                return value;
            }
        }

        private static bool DeepEqualsValues(object? a, object? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            // For value types and string, use Equals
            if (a.GetType().IsValueType || a is string)
                return a.Equals(b);

            // For complex types, use JSON comparison
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
