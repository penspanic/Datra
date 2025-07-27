using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datra.Interfaces
{
    /// <summary>
    /// Base interface for data repository
    /// </summary>
    public interface IDataRepository<TKey, TData> 
        where TData : class, ITableData<TKey>
    {
        /// <summary>
        /// Get all data
        /// </summary>
        IReadOnlyDictionary<TKey, TData> GetAll();
        
        /// <summary>
        /// Get data by ID
        /// </summary>
        TData GetById(TKey id);
        
        /// <summary>
        /// Get data by ID (returns null if not found)
        /// </summary>
        TData? TryGetById(TKey id);
        
        /// <summary>
        /// Find data matching the predicate
        /// </summary>
        IEnumerable<TData> Find(System.Func<TData, bool> predicate);
        
        /// <summary>
        /// Check if data exists
        /// </summary>
        bool Contains(TKey id);
        
        /// <summary>
        /// Data count
        /// </summary>
        int Count { get; }
    }
    
    /// <summary>
    /// Repository interface for single data
    /// </summary>
    public interface ISingleDataRepository<TData> where TData : class
    {
        /// <summary>
        /// Get data
        /// </summary>
        TData Get();
        
        /// <summary>
        /// Whether data is loaded
        /// </summary>
        bool IsLoaded { get; }
    }
}
