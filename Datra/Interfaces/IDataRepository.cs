using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datra.Interfaces
{
    /// <summary>
    /// Base interface for data repository
    /// </summary>
    public interface IDataRepository<TKey, TData> : IReadOnlyDictionary<TKey, TData>
        where TData : class, ITableData<TKey>
    {
        /// <summary>
        /// Find data matching the predicate
        /// </summary>
        IEnumerable<TData> Find(System.Func<TData, bool> predicate);
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
