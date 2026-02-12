#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Datra.Editor.Interfaces
{
    /// <summary>
    /// 변경 항목 하나의 상세 정보
    /// </summary>
    public class ChangeEntry
    {
        public object Key { get; set; } = default!;
        public ItemState State { get; set; }
        public IReadOnlyList<string> ModifiedProperties { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// DataSource의 전체 변경 요약
    /// </summary>
    public class ChangeSummary
    {
        public IReadOnlyList<ChangeEntry> Entries { get; set; } = Array.Empty<ChangeEntry>();
        public int AddedCount => Entries.Count(e => e.State == ItemState.Added);
        public int ModifiedCount => Entries.Count(e => e.State == ItemState.Modified);
        public int DeletedCount => Entries.Count(e => e.State == ItemState.Deleted);
        public int TotalCount => Entries.Count;
    }

    /// <summary>
    /// Represents the state of an item in the editable data source.
    /// </summary>
    public enum ItemState
    {
        /// <summary>Item exists in repository and is unchanged</summary>
        Unchanged,
        /// <summary>Item exists in repository but has been modified</summary>
        Modified,
        /// <summary>Item was added in this editing session</summary>
        Added,
        /// <summary>Item exists in repository but is marked for deletion</summary>
        Deleted
    }

    /// <summary>
    /// Non-generic base interface for editable data sources.
    /// Provides editor-only overlay over repository data without modifying the repository directly.
    /// Changes are accumulated as deltas and only applied on Save().
    /// </summary>
    public interface IEditableDataSource
    {
        /// <summary>
        /// Whether there are any pending modifications (adds, deletes, or property changes)
        /// </summary>
        bool HasModifications { get; }

        /// <summary>
        /// Number of items (including adds, excluding deletes)
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Enumerate all items as objects (repository + adds - deletes, using modified versions)
        /// </summary>
        IEnumerable<object> EnumerateItems();

        /// <summary>
        /// Get the state of an item by its key
        /// </summary>
        ItemState GetItemState(object key);

        /// <summary>
        /// Check if a specific property is modified for a given key
        /// </summary>
        bool IsPropertyModified(object key, string propertyName);

        /// <summary>
        /// Get all modified property names for a given key
        /// </summary>
        IEnumerable<string> GetModifiedProperties(object key);

        /// <summary>
        /// Get baseline (original) value for a specific property
        /// </summary>
        object? GetPropertyBaselineValue(object key, string propertyName);

        /// <summary>
        /// Revert all pending changes (clears all deltas)
        /// </summary>
        void Revert();

        /// <summary>
        /// Refresh baseline from current data state.
        /// Call after save to reset modification tracking.
        /// </summary>
        void RefreshBaseline();

        /// <summary>
        /// Initialize the data source asynchronously.
        /// For asset data sources, this loads all assets from summaries.
        /// For other data sources, this may be a no-op.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Apply all pending changes to the repository and save
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// Event raised when modification state changes
        /// </summary>
        event Action<bool>? OnModifiedStateChanged;

        /// <summary>
        /// 변경 요약 반환 (추가/수정/삭제 항목 목록)
        /// </summary>
        ChangeSummary GetChangeSummary();

        /// <summary>
        /// Get the key for an item. Each data source type defines its own key semantics:
        /// - KeyValue: ID property value
        /// - Single: constant key (always the same)
        /// - Localization: the locale key string
        /// - Asset: AssetId
        /// </summary>
        object? GetItemKey(object item);

        /// <summary>
        /// Track a property change and update modification state.
        /// This is the non-generic version for use by Views that don't know the key type.
        /// </summary>
        /// <param name="key">The item key (obtained from GetItemKey)</param>
        /// <param name="propertyName">Name of the changed property</param>
        /// <param name="newValue">The new value</param>
        /// <param name="isPropertyModified">Output: true if property differs from baseline</param>
        void TrackPropertyChange(object key, string propertyName, object? newValue, out bool isPropertyModified);
    }

    /// <summary>
    /// Generic interface for editable data sources with key-value semantics.
    /// Provides a transactional editing layer over IKeyValueDataRepository.
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The value type</typeparam>
    public interface IEditableDataSource<TKey, TValue> : IEditableDataSource
        where TKey : notnull
        where TValue : class
    {
        /// <summary>
        /// Enumerate all items (repository + adds - deletes, using modified versions)
        /// </summary>
        new IEnumerable<KeyValuePair<TKey, TValue>> EnumerateItems();

        /// <summary>
        /// Get an item by key (returns modified version if exists)
        /// </summary>
        /// <exception cref="KeyNotFoundException">If key doesn't exist or is deleted</exception>
        TValue GetItem(TKey key);

        /// <summary>
        /// Try to get an item by key
        /// </summary>
        bool TryGetItem(TKey key, out TValue? value);

        /// <summary>
        /// Check if a key exists (in repository or added, and not deleted)
        /// </summary>
        bool ContainsKey(TKey key);

        /// <summary>
        /// Get the state of an item
        /// </summary>
        ItemState GetItemState(TKey key);

        /// <summary>
        /// Get a working copy of an item for modification.
        /// If not already modified, creates a deep clone from repository/baseline.
        /// </summary>
        TValue GetWorkingCopy(TKey key);

        /// <summary>
        /// Mark an item as modified (call after modifying the working copy)
        /// </summary>
        void MarkModified(TKey key);

        /// <summary>
        /// Track a property change and update modification state
        /// </summary>
        void TrackPropertyChange(TKey key, string propertyName, object? newValue, out bool isPropertyModified);

        /// <summary>
        /// Add a new item
        /// </summary>
        void Add(TKey key, TValue value);

        /// <summary>
        /// Delete an item
        /// </summary>
        void Delete(TKey key);

        /// <summary>
        /// Get baseline (original) value for a key
        /// </summary>
        TValue? GetBaselineValue(TKey key);

        /// <summary>
        /// Check if a specific property is modified
        /// </summary>
        bool IsPropertyModified(TKey key, string propertyName);

        /// <summary>
        /// Get modified property names for a key
        /// </summary>
        IEnumerable<string> GetModifiedProperties(TKey key);

        /// <summary>
        /// Get baseline value for a specific property
        /// </summary>
        object? GetPropertyBaselineValue(TKey key, string propertyName);
    }
}
