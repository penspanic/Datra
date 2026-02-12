#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datra.DataTypes;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.Repositories;

namespace Datra.Editor.DataSources
{
    /// <summary>
    /// Editable data source for asset data.
    /// Provides a transactional editing layer that doesn't modify the repository until Save().
    /// Property changes are tracked on the inner Data object, not on Asset&lt;T&gt; wrapper.
    /// </summary>
    /// <typeparam name="T">The asset data type</typeparam>
    public class EditableAssetDataSource<T> : EditableDataSourceBase, IEditableDataSource<AssetId, Asset<T>>
        where T : class
    {
        private readonly IAssetRepository<T> _repository;

        // Baseline snapshot (represents saved state)
        private readonly Dictionary<AssetId, Asset<T>> _baseline = new();

        // Working copies of modified items (Asset<T> with cloned Data)
        private readonly Dictionary<AssetId, Asset<T>> _workingCopies = new();

        // Tracking sets
        private readonly HashSet<AssetId> _addedKeys = new();
        private readonly HashSet<AssetId> _deletedKeys = new();
        private readonly HashSet<AssetId> _modifiedKeys = new();

        // Property-level change tracking (tracks changes on inner Data object)
        private readonly Dictionary<(AssetId key, string propertyName), PropertyChangeRecord> _propertyChanges = new();

        private class PropertyChangeRecord
        {
            public object? BaselineValue { get; set; }
            public object? CurrentValue { get; set; }
        }

        public EditableAssetDataSource(IAssetRepository<T> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            InitializeBaselineInternal();
        }

        #region Initialization

        /// <summary>
        /// Initialize the data source by loading all assets from summaries.
        /// Call this after context.LoadAllAsync() to populate the baseline.
        /// </summary>
        public override async Task InitializeAsync()
        {
            // Load all assets from summaries
            foreach (var summary in _repository.Summaries)
            {
                await _repository.GetAsync(summary.Id);
            }

            // Refresh baseline now that all assets are loaded
            RefreshBaselineInternal();
        }

        private void InitializeBaselineInternal()
        {
            _baseline.Clear();
            _workingCopies.Clear();
            _addedKeys.Clear();
            _deletedKeys.Clear();
            _modifiedKeys.Clear();
            _propertyChanges.Clear();

            foreach (var asset in _repository.LoadedAssets.Values)
            {
                _baseline[asset.Id] = CloneAsset(asset);
            }
        }

        #endregion

        #region EditableDataSourceBase Implementation

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
            foreach (var kvp in EnumerateItemsTyped())
            {
                yield return kvp.Value;
            }
        }

        public override ItemState GetItemState(object key)
        {
            if (key is AssetId typedKey)
                return GetItemState(typedKey);
            return ItemState.Unchanged;
        }

        public override bool IsPropertyModified(object key, string propertyName)
        {
            if (key is AssetId typedKey)
                return IsPropertyModified(typedKey, propertyName);
            return false;
        }

        public override IEnumerable<string> GetModifiedProperties(object key)
        {
            if (key is AssetId typedKey)
                return GetModifiedProperties(typedKey);
            return Enumerable.Empty<string>();
        }

        public override object? GetPropertyBaselineValue(object key, string propertyName)
        {
            if (key is AssetId typedKey)
                return GetPropertyBaselineValue(typedKey, propertyName);
            return null;
        }

        public override ChangeSummary GetChangeSummary()
        {
            var entries = new List<ChangeEntry>();

            foreach (var key in _addedKeys)
                entries.Add(new ChangeEntry { Key = key, State = ItemState.Added });

            foreach (var key in _deletedKeys)
                entries.Add(new ChangeEntry { Key = key, State = ItemState.Deleted });

            foreach (var key in _modifiedKeys)
                entries.Add(new ChangeEntry
                {
                    Key = key,
                    State = ItemState.Modified,
                    ModifiedProperties = GetModifiedProperties(key).ToList()
                });

            return new ChangeSummary { Entries = entries };
        }

        public override object? GetItemKey(object item)
        {
            if (item == null) return null;

            if (item is Asset<T> asset)
                return asset.Id;

            if (item is KeyValuePair<AssetId, Asset<T>> kvp)
                return kvp.Key;

            var idProp = item.GetType().GetProperty("Id");
            if (idProp != null && idProp.PropertyType == typeof(AssetId))
                return idProp.GetValue(item);

            return null;
        }

        public override void TrackPropertyChange(object key, string propertyName, object? newValue, out bool isPropertyModified)
        {
            if (key is AssetId typedKey)
            {
                TrackPropertyChange(typedKey, propertyName, newValue, out isPropertyModified);
            }
            else
            {
                isPropertyModified = false;
            }
        }

        protected override void RevertInternal()
        {
            _workingCopies.Clear();
            _addedKeys.Clear();
            _deletedKeys.Clear();
            _modifiedKeys.Clear();
            _propertyChanges.Clear();
        }

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
                if (_workingCopies.TryGetValue(key, out var addedAsset))
                {
                    _repository.Add(addedAsset.Data, addedAsset.Metadata, addedAsset.FilePath);
                }
            }

            // Apply modifications
            foreach (var key in _modifiedKeys)
            {
                if (_addedKeys.Contains(key))
                    continue;

                if (_workingCopies.TryGetValue(key, out var modifiedAsset))
                {
                    var repoAsset = _repository.TryGetLoaded(key);
                    if (repoAsset != null)
                    {
                        CopyDataProperties(modifiedAsset.Data, repoAsset.Data);
                        _repository.MarkAsModified(key);
                    }
                }
            }

            // Save to storage
            await _repository.SaveAsync();
        }

        protected override void RefreshBaselineInternal()
        {
            InitializeBaselineInternal();
        }

        #endregion

        #region IEditableDataSource<AssetId, Asset<T>> Implementation

        IEnumerable<KeyValuePair<AssetId, Asset<T>>> IEditableDataSource<AssetId, Asset<T>>.EnumerateItems()
        {
            return EnumerateItemsTyped();
        }

        private IEnumerable<KeyValuePair<AssetId, Asset<T>>> EnumerateItemsTyped()
        {
            // Items from baseline (excluding deleted, using working copy if modified)
            foreach (var kvp in _baseline)
            {
                if (_deletedKeys.Contains(kvp.Key))
                    continue;

                if (_workingCopies.TryGetValue(kvp.Key, out var workingCopy))
                    yield return new KeyValuePair<AssetId, Asset<T>>(kvp.Key, workingCopy);
                else
                    yield return kvp;
            }

            // Added items
            foreach (var key in _addedKeys)
            {
                if (_workingCopies.TryGetValue(key, out var addedItem))
                    yield return new KeyValuePair<AssetId, Asset<T>>(key, addedItem);
            }
        }

        public Asset<T> GetItem(AssetId key)
        {
            if (!TryGetItem(key, out var value))
                throw new KeyNotFoundException($"Asset with ID '{key}' not found or deleted.");
            return value!;
        }

        public bool TryGetItem(AssetId key, out Asset<T>? value)
        {
            if (_deletedKeys.Contains(key))
            {
                value = null;
                return false;
            }

            if (_workingCopies.TryGetValue(key, out value))
                return true;

            if (_baseline.TryGetValue(key, out value))
                return true;

            value = null;
            return false;
        }

        public bool ContainsKey(AssetId key)
        {
            if (_deletedKeys.Contains(key))
                return false;

            return _workingCopies.ContainsKey(key) || _baseline.ContainsKey(key);
        }

        public ItemState GetItemState(AssetId key)
        {
            if (_deletedKeys.Contains(key))
                return ItemState.Deleted;

            if (_addedKeys.Contains(key))
                return ItemState.Added;

            if (_modifiedKeys.Contains(key))
                return ItemState.Modified;

            return ItemState.Unchanged;
        }

        public Asset<T> GetWorkingCopy(AssetId key)
        {
            if (_workingCopies.TryGetValue(key, out var existing))
                return existing;

            if (!_baseline.TryGetValue(key, out var baseline))
                throw new KeyNotFoundException($"Asset with ID '{key}' not found in baseline.");

            var workingCopy = CloneAsset(baseline);
            _workingCopies[key] = workingCopy;
            return workingCopy;
        }

        /// <summary>
        /// Get the inner Data object for editing.
        /// Creates a working copy if one doesn't exist.
        /// </summary>
        public T GetDataForEditing(AssetId key)
        {
            return GetWorkingCopy(key).Data;
        }

        public void MarkModified(AssetId key)
        {
            if (_addedKeys.Contains(key))
                return;

            if (_deletedKeys.Contains(key))
                return;

            if (!_baseline.ContainsKey(key) && !_workingCopies.ContainsKey(key))
                return;

            bool hadModifications = HasModifications;

            _modifiedKeys.Add(key);

            NotifyIfStateChanged(hadModifications);
        }

        public void TrackPropertyChange(AssetId key, string propertyName, object? newValue, out bool isPropertyModified)
        {
            bool hadModifications = HasModifications;
            isPropertyModified = false;

            // Get or create working copy
            if (!_workingCopies.TryGetValue(key, out var workingCopy))
            {
                if (!_baseline.TryGetValue(key, out var baseline))
                {
                    if (_addedKeys.Contains(key))
                    {
                        isPropertyModified = true;
                    }
                    return;
                }
                workingCopy = CloneAsset(baseline);
                _workingCopies[key] = workingCopy;
            }

            // Get baseline value from inner Data object
            object? baselineValue = null;
            if (_baseline.TryGetValue(key, out var baselineAsset))
            {
                var propInfo = typeof(T).GetProperty(propertyName);
                if (propInfo != null)
                    baselineValue = propInfo.GetValue(baselineAsset.Data);
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

                bool hasOtherChanges = _propertyChanges.Keys.Any(k => k.key.Equals(key));
                if (!hasOtherChanges)
                {
                    _modifiedKeys.Remove(key);
                    if (!_addedKeys.Contains(key))
                    {
                        _workingCopies.Remove(key);
                    }
                }
            }

            // Update working copy's Data property
            var prop = typeof(T).GetProperty(propertyName);
            if (prop != null && workingCopy?.Data != null)
            {
                prop.SetValue(workingCopy.Data, newValue);
            }

            NotifyIfStateChanged(hadModifications);
        }

        public void Add(AssetId key, Asset<T> value)
        {
            if (ContainsKey(key))
                throw new InvalidOperationException($"Asset with ID '{key}' already exists.");

            bool hadModifications = HasModifications;

            _workingCopies[key] = CloneAsset(value);
            _addedKeys.Add(key);
            _deletedKeys.Remove(key);

            NotifyIfStateChanged(hadModifications);
        }

        /// <summary>
        /// Add a new asset with auto-generated metadata.
        /// </summary>
        public Asset<T> AddNew(T data, string filePath)
        {
            var asset = Asset<T>.Create(data, filePath);
            Add(asset.Id, asset);
            return asset;
        }

        public void Delete(AssetId key)
        {
            bool hadModifications = HasModifications;

            if (_addedKeys.Contains(key))
            {
                _addedKeys.Remove(key);
                _workingCopies.Remove(key);
                RemovePropertyChangesForKey(key);
            }
            else if (_baseline.ContainsKey(key))
            {
                _deletedKeys.Add(key);
                _modifiedKeys.Remove(key);
                _workingCopies.Remove(key);
                RemovePropertyChangesForKey(key);
            }

            NotifyIfStateChanged(hadModifications);
        }

        public Asset<T>? GetBaselineValue(AssetId key)
        {
            return _baseline.TryGetValue(key, out var value) ? CloneAsset(value) : null;
        }

        public bool IsPropertyModified(AssetId key, string propertyName)
        {
            return _propertyChanges.ContainsKey((key, propertyName));
        }

        public IEnumerable<string> GetModifiedProperties(AssetId key)
        {
            return _propertyChanges.Keys
                .Where(k => k.key.Equals(key))
                .Select(k => k.propertyName);
        }

        public object? GetPropertyBaselineValue(AssetId key, string propertyName)
        {
            if (!_baseline.TryGetValue(key, out var baselineAsset))
                return null;

            var propInfo = typeof(T).GetProperty(propertyName);
            return propInfo?.GetValue(baselineAsset.Data);
        }

        #endregion

        #region Helpers

        private void RemovePropertyChangesForKey(AssetId key)
        {
            var keysToRemove = _propertyChanges.Keys.Where(k => k.key.Equals(key)).ToList();
            foreach (var k in keysToRemove)
            {
                _propertyChanges.Remove(k);
            }
        }

        private Asset<T> CloneAsset(Asset<T> asset)
        {
            var clonedData = DeepCloner.Clone(asset.Data);
            return new Asset<T>(asset.Id, asset.Metadata, clonedData, asset.FilePath);
        }

        private static bool DeepEqualsValues(object? a, object? b)
        {
            return DeepCloner.DeepEquals(a, b);
        }

        private static void CopyDataProperties(T source, T target)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
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
