using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Editor-only change tracker that observes repository changes externally.
    /// Does NOT modify runtime code. Tracks baseline and current state separately.
    /// Tracks changes at property level for granular change detection.
    /// </summary>
    public class RepositoryChangeTracker<TKey, TValue> : IRepositoryChangeTracker
        where TKey : notnull
        where TValue : class
    {
        // Baseline snapshot (taken at load time)
        private Dictionary<TKey, TValue> _baseline = new Dictionary<TKey, TValue>();

        // Current snapshot (updated when tracking changes)
        private Dictionary<TKey, TValue> _current = new Dictionary<TKey, TValue>();

        // Property-level change tracking
        private Dictionary<(TKey key, string propertyName), PropertyChangeRecord> _propertyChanges =
            new Dictionary<(TKey, string), PropertyChangeRecord>();

        // Entity-level tracking for additions and deletions
        private HashSet<TKey> _addedKeys = new HashSet<TKey>();
        private HashSet<TKey> _deletedKeys = new HashSet<TKey>();

        private class PropertyChangeRecord
        {
            public object BaselineValue { get; set; }
            public object CurrentValue { get; set; }
        }

        /// <summary>
        /// Initialize baseline from repository (call after Load)
        /// </summary>
        public void InitializeBaseline(IReadOnlyDictionary<TKey, TValue> repositoryData)
        {
            _baseline.Clear();
            _current.Clear();
            _propertyChanges.Clear();
            _addedKeys.Clear();
            _deletedKeys.Clear();

            foreach (var kvp in repositoryData)
            {
                var cloned = DeepClone(kvp.Value);
                _baseline[kvp.Key] = cloned;
                _current[kvp.Key] = DeepClone(cloned);
            }
        }

        /// <summary>
        /// Track a change (called by View after Repository update)
        /// Compares all properties and tracks individual property changes
        /// </summary>
        public void TrackChange(TKey key, TValue newValue)
        {
            if (!_baseline.TryGetValue(key, out var baselineValue))
            {
                // New key - mark as added
                TrackAdd(key, newValue);
                return;
            }

            // Update current snapshot
            _current[key] = DeepClone(newValue);

            // Compare all properties
            var properties = typeof(TValue).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);

            foreach (var prop in properties)
            {
                var baselinePropValue = prop.GetValue(baselineValue);
                var newPropValue = prop.GetValue(newValue);

                TrackPropertyChange(key, prop.Name, newPropValue);
            }

            Debug.Log($"[TrackChange] key={key}, total property changes: {_propertyChanges.Count}, HasModifications={HasModifications}");
        }

        /// <summary>
        /// Track a property change for specific key and property
        /// </summary>
        public void TrackPropertyChange(TKey key, string propertyName, object newValue)
        {
            if (!_baseline.TryGetValue(key, out var baselineEntity))
            {
                // Key doesn't exist in baseline - it's an added entity
                Debug.Log($"[TrackPropertyChange] key={key}, property={propertyName} - entity is added, not tracking property-level changes");
                return;
            }

            // Get baseline property value using reflection
            var propInfo = typeof(TValue).GetProperty(propertyName);
            if (propInfo == null)
            {
                Debug.LogWarning($"[TrackPropertyChange] Property '{propertyName}' not found on type {typeof(TValue).Name}");
                return;
            }

            var baselineValue = propInfo.GetValue(baselineEntity);

            // Compare values
            bool isEqual = DeepEqualsValues(baselineValue, newValue);
            var changeKey = (key, propertyName);

            Debug.Log($"[TrackPropertyChange] key={key}, property={propertyName}, baseline={baselineValue}, new={newValue}, equal={isEqual}");

            if (!isEqual)
            {
                _propertyChanges[changeKey] = new PropertyChangeRecord
                {
                    BaselineValue = baselineValue,
                    CurrentValue = newValue
                };
                Debug.Log($"[TrackPropertyChange] Added property change. Total property changes: {_propertyChanges.Count}");
            }
            else
            {
                // Back to baseline - remove change
                _propertyChanges.Remove(changeKey);
                Debug.Log($"[TrackPropertyChange] Removed property change (back to baseline). Total: {_propertyChanges.Count}");
            }

            // Also update current entity snapshot
            if (_current.TryGetValue(key, out var currentEntity))
            {
                propInfo.SetValue(currentEntity, newValue);
            }
        }

        /// <summary>
        /// Track an addition (called by View after Repository.Add)
        /// </summary>
        public void TrackAdd(TKey key, TValue value)
        {
            _current[key] = DeepClone(value);

            if (!_baseline.ContainsKey(key))
            {
                _addedKeys.Add(key);
                _deletedKeys.Remove(key);
                Debug.Log($"[TrackAdd] key={key}, _addedKeys.Count={_addedKeys.Count}");
            }
            else
            {
                // Was in baseline, treat as modification
                TrackChange(key, value);
            }
        }

        /// <summary>
        /// Track a deletion (called by View after Repository.Remove)
        /// </summary>
        public void TrackDelete(TKey key)
        {
            _current.Remove(key);

            if (_addedKeys.Contains(key))
            {
                // Was added in this session
                _addedKeys.Remove(key);
            }
            else if (_baseline.ContainsKey(key))
            {
                // Was in baseline
                _deletedKeys.Add(key);

                // Remove all property changes for this key
                var keysToRemove = _propertyChanges.Keys.Where(k => k.key.Equals(key)).ToList();
                foreach (var k in keysToRemove)
                {
                    _propertyChanges.Remove(k);
                }
            }

            Debug.Log($"[TrackDelete] key={key}, _deletedKeys.Count={_deletedKeys.Count}");
        }

        /// <summary>
        /// Synchronize with repository (call periodically or on-demand)
        /// </summary>
        public void SyncWithRepository(IReadOnlyDictionary<TKey, TValue> repositoryData)
        {
            foreach (var kvp in repositoryData)
            {
                TrackChange(kvp.Key, kvp.Value);
            }

            // Detect deletions
            var repoKeys = new HashSet<TKey>(repositoryData.Keys);
            var currentKeys = new HashSet<TKey>(_current.Keys);

            foreach (var key in currentKeys)
            {
                if (!repoKeys.Contains(key))
                {
                    TrackDelete(key);
                }
            }
        }

        /// <summary>
        /// Check if any property of the entity is modified
        /// </summary>
        public bool IsModified(TKey key)
        {
            return _propertyChanges.Keys.Any(k => k.key.Equals(key));
        }

        /// <summary>
        /// Check if a specific property is modified
        /// </summary>
        public bool IsPropertyModified(TKey key, string propertyName)
        {
            return _propertyChanges.ContainsKey((key, propertyName));
        }

        public bool IsAdded(TKey key) => _addedKeys.Contains(key);
        public bool IsDeleted(TKey key) => _deletedKeys.Contains(key);

        /// <summary>
        /// Get all keys that have at least one modified property
        /// </summary>
        public IEnumerable<TKey> GetModifiedKeys()
        {
            return _propertyChanges.Keys.Select(k => k.key).Distinct();
        }

        /// <summary>
        /// Get all modified properties for a given key
        /// </summary>
        public IEnumerable<string> GetModifiedProperties(TKey key)
        {
            return _propertyChanges.Keys
                .Where(k => k.key.Equals(key))
                .Select(k => k.propertyName);
        }

        /// <summary>
        /// Get baseline value for a specific property
        /// </summary>
        public object GetPropertyBaselineValue(TKey key, string propertyName)
        {
            if (!_baseline.TryGetValue(key, out var baselineEntity))
                return null;

            var propInfo = typeof(TValue).GetProperty(propertyName);
            if (propInfo == null)
                return null;

            return propInfo.GetValue(baselineEntity);
        }

        public IEnumerable<TKey> GetAddedKeys() => _addedKeys;
        public IEnumerable<TKey> GetDeletedKeys() => _deletedKeys;

        public bool HasModifications =>
            _propertyChanges.Count > 0 || _addedKeys.Count > 0 || _deletedKeys.Count > 0;

        // IRepositoryChangeTracker implementation (non-generic wrappers)
        object IRepositoryChangeTracker.GetBaselineValue(object key)
        {
            return GetBaselineValue((TKey)key);
        }

        void IRepositoryChangeTracker.TrackChange(object key, object value)
        {
            TrackChange((TKey)key, (TValue)value);
        }

        void IRepositoryChangeTracker.TrackPropertyChange(object key, string propertyName, object newValue)
        {
            TrackPropertyChange((TKey)key, propertyName, newValue);
        }

        void IRepositoryChangeTracker.TrackAdd(object key, object value)
        {
            TrackAdd((TKey)key, (TValue)value);
        }

        void IRepositoryChangeTracker.TrackDelete(object key)
        {
            TrackDelete((TKey)key);
        }

        bool IRepositoryChangeTracker.IsModified(object key)
        {
            return IsModified((TKey)key);
        }

        bool IRepositoryChangeTracker.IsPropertyModified(object key, string propertyName)
        {
            return IsPropertyModified((TKey)key, propertyName);
        }

        bool IRepositoryChangeTracker.IsAdded(object key)
        {
            return IsAdded((TKey)key);
        }

        bool IRepositoryChangeTracker.IsDeleted(object key)
        {
            return IsDeleted((TKey)key);
        }

        IEnumerable<string> IRepositoryChangeTracker.GetModifiedProperties(object key)
        {
            return GetModifiedProperties((TKey)key);
        }

        object IRepositoryChangeTracker.GetPropertyBaselineValue(object key, string propertyName)
        {
            return GetPropertyBaselineValue((TKey)key, propertyName);
        }

        void IRepositoryChangeTracker.InitializeBaseline(object repositoryData)
        {
            InitializeBaseline((IReadOnlyDictionary<TKey, TValue>)repositoryData);
        }

        void IRepositoryChangeTracker.UpdateBaseline(object repositoryData)
        {
            UpdateBaseline((IReadOnlyDictionary<TKey, TValue>)repositoryData);
        }

        void IRepositoryChangeTracker.RevertAll()
        {
            // Clear all changes and revert to baseline
            _propertyChanges.Clear();
            _addedKeys.Clear();
            _deletedKeys.Clear();

            // Reset current to baseline
            _current.Clear();
            foreach (var kvp in _baseline)
            {
                _current[kvp.Key] = DeepClone(kvp.Value);
            }
        }

        /// <summary>
        /// Get baseline value for revert (entire entity)
        /// </summary>
        public TValue GetBaselineValue(TKey key)
        {
            return _baseline.TryGetValue(key, out var value) ? DeepClone(value) : null;
        }

        /// <summary>
        /// Revert to baseline (returns baseline data for Repository to apply)
        /// </summary>
        public Dictionary<TKey, TValue> GetBaselineData()
        {
            var result = new Dictionary<TKey, TValue>();
            foreach (var kvp in _baseline)
            {
                result[kvp.Key] = DeepClone(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Update baseline to current state (call after save)
        /// </summary>
        public void UpdateBaseline(IReadOnlyDictionary<TKey, TValue> repositoryData)
        {
            InitializeBaseline(repositoryData);
        }

        private TValue DeepClone(TValue value)
        {
            if (value == null) return null;

            // For primitive wrapper types, use direct copy
            if (value is string str)
                return str as TValue;

            // Use Newtonsoft.Json for deep clone (supports properties)
            try
            {
                var json = JsonConvert.SerializeObject(value);
                return JsonConvert.DeserializeObject<TValue>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not deep clone {typeof(TValue).Name}: {e.Message}, using reference copy");
                return value;
            }
        }

        private bool DeepEquals(TValue a, TValue b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            // For string comparison
            if (a is string strA && b is string strB)
                return strA == strB;

            // Use Newtonsoft.Json for comparison (supports properties)
            try
            {
                var jsonA = JsonConvert.SerializeObject(a);
                var jsonB = JsonConvert.SerializeObject(b);
                bool areEqual = jsonA == jsonB;

                Debug.Log($"[DeepEquals] Type={typeof(TValue).Name}\nbaselineJson={jsonA}\ncurrentJson={jsonB}\nequal={areEqual}");

                return areEqual;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DeepEquals] JSON comparison failed: {e.Message}");
                // Fallback: reference equality
                return ReferenceEquals(a, b);
            }
        }

        /// <summary>
        /// Deep equals for individual values (not full entities)
        /// </summary>
        private bool DeepEqualsValues(object a, object b)
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
            catch (Exception)
            {
                // Fallback: reference equality
                return ReferenceEquals(a, b);
            }
        }
    }
}
