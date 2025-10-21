using System.Threading.Tasks;

namespace Datra.Interfaces
{
    /// <summary>
    /// Interface for editable data repository that supports CRUD operations
    /// </summary>
    public interface IEditableDataRepository<TKey, TData> : IDataRepository<TKey, TData>
        where TData : class, ITableData<TKey>
    {
        /// <summary>
        /// Add new data
        /// </summary>
        void Add(TData data);
        
        /// <summary>
        /// Remove data by key
        /// </summary>
        bool Remove(TKey key);
        
        /// <summary>
        /// Update the key of existing data
        /// </summary>
        bool UpdateKey(TKey oldKey, TKey newKey);
        
        /// <summary>
        /// Clear all data
        /// </summary>
        void Clear();
    }
    
    /// <summary>
    /// Interface for editable single data repository
    /// </summary>
    public interface IEditableSingleDataRepository<TData> : ISingleDataRepository<TData>
        where TData : class
    {
        // Set() is already defined in ISingleDataRepository<TData>
    }
}