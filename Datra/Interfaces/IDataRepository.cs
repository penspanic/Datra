using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datra.Interfaces
{
    public interface IDataRepository
    {
        Task LoadAsync();
        Task SaveAsync();
        string GetLoadedFilePath();
    }

    /// <summary>
    /// Base interface for data repository
    /// </summary>
    public interface IDataRepository<TKey, TData>
        : IDataRepository, IReadOnlyDictionary<TKey, TData>
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
    public interface ISingleDataRepository<TData>
        : IDataRepository
        where TData : class
    {
        /// <summary>
        /// Get the single data object
        /// </summary>
        TData Get();

        /// <summary>
        /// Set the single data object
        /// </summary>
        void Set(TData data);

        /// <summary>
        /// Whether data is loaded
        /// </summary>
        bool IsLoaded { get; }
    }
}
