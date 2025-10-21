using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Editor-only change tracker that observes repository changes externally.
    /// Does NOT modify runtime code. Tracks baseline and current state separately.
    /// </summary>
    public class RepositoryChangeTracker<TKey, TValue>
        where TKey : notnull
        where TValue : class
    {
        // Baseline snapshot (taken at load time)
        private Dictionary<TKey, TValue> _baseline = new Dictionary<TKey, TValue>();

        // Current snapshot (updated when tracking changes)
        private Dictionary<TKey, TValue> _current = new Dictionary<TKey, TValue>();

        // Change sets
        private Dictionary<TKey, ChangeRecord> _changes = new Dictionary<TKey, ChangeRecord>();
        private HashSet<TKey> _addedKeys = new HashSet<TKey>();
        private HashSet<TKey> _deletedKeys = new HashSet<TKey>();

        private class ChangeRecord
        {
            public TValue BaselineValue { get; set; }
            public TValue CurrentValue { get; set; }
        }

        /// <summary>
        /// Initialize baseline from repository (call after Load)
        /// </summary>
        public void InitializeBaseline(IReadOnlyDictionary<TKey, TValue> repositoryData)
        {
            _baseline.Clear();
            _current.Clear();
            _changes.Clear();
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
        /// Manually track a change (called by View after Repository update)
        /// </summary>
        public void TrackChange(TKey key, TValue newValue)
        {
            // Update current snapshot
            _current[key] = DeepClone(newValue);

            if (_baseline.TryGetValue(key, out var baselineValue))
            {
                // Existing key - check if modified
                if (!DeepEquals(baselineValue, newValue))
                {
                    _changes[key] = new ChangeRecord
                    {
                        BaselineValue = baselineValue,
                        CurrentValue = DeepClone(newValue)
                    };
                }
                else
                {
                    // Back to baseline
                    _changes.Remove(key);
                }
            }
            else
            {
                // New key
                _addedKeys.Add(key);
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
            }
            else
            {
                // Was in baseline, treat as change
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
                _changes.Remove(key);
            }
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

        public bool IsModified(TKey key) => _changes.ContainsKey(key);
        public bool IsAdded(TKey key) => _addedKeys.Contains(key);
        public bool IsDeleted(TKey key) => _deletedKeys.Contains(key);

        public IEnumerable<TKey> GetModifiedKeys() => _changes.Keys;
        public IEnumerable<TKey> GetAddedKeys() => _addedKeys;
        public IEnumerable<TKey> GetDeletedKeys() => _deletedKeys;

        public bool HasModifications =>
            _changes.Count > 0 || _addedKeys.Count > 0 || _deletedKeys.Count > 0;

        /// <summary>
        /// Get baseline value for revert
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

            // Use JSON serialization for deep clone
            try
            {
                var json = JsonUtility.ToJson(value);
                return JsonUtility.FromJson<TValue>(json);
            }
            catch
            {
                // Fallback: return reference (not ideal but safe)
                Debug.LogWarning($"Could not deep clone {typeof(TValue).Name}, using reference copy");
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

            // Use JSON comparison for complex objects
            try
            {
                var jsonA = JsonUtility.ToJson(a);
                var jsonB = JsonUtility.ToJson(b);
                return jsonA == jsonB;
            }
            catch
            {
                // Fallback: reference equality
                return ReferenceEquals(a, b);
            }
        }
    }
}
