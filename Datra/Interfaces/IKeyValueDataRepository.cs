#nullable enable
using System;
using System.Collections.Generic;

namespace Datra.Interfaces
{
    /// <summary>
    /// Repository interface for key-value based table data
    /// </summary>
    public interface IKeyValueDataRepository<TKey, TData> : IDataRepository<TKey, TData>
        where TData : class, ITableData<TKey>
    {
        /// <summary>
        /// Get all data as a read-only dictionary
        /// </summary>
        IReadOnlyDictionary<TKey, TData> GetAll();

        /// <summary>
        /// Get data by ID
        /// </summary>
        TData GetById(TKey id);

        /// <summary>
        /// Try to get data by ID
        /// </summary>
        TData? TryGetById(TKey id);

        /// <summary>
        /// Check if data with ID exists
        /// </summary>
        bool Contains(TKey id);

        /// <summary>
        /// Get count of data items
        /// </summary>
        new int Count { get; }

        /// <summary>
        /// Add new data item
        /// </summary>
        void Add(TData data);

        /// <summary>
        /// Remove data item by key
        /// </summary>
        bool Remove(TKey key);

        /// <summary>
        /// Update item's key
        /// </summary>
        bool UpdateKey(TKey oldKey, TKey newKey);

        /// <summary>
        /// Clear all data
        /// </summary>
        void Clear();
    }
}
