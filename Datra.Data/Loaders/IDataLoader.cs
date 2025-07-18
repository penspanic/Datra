using System.Collections.Generic;
using Datra.Data.Interfaces;

namespace Datra.Data.Loaders
{
    /// <summary>
    /// Base interface for data loaders
    /// Implemented for each format like JSON, YAML, CSV, etc.
    /// </summary>
    public interface IDataLoader
    {
        /// <summary>
        /// Load a single data object
        /// </summary>
        T LoadSingle<T>(string text) where T : class, new();
        
        /// <summary>
        /// Load table data (Key-Value Dictionary)
        /// </summary>
        Dictionary<TKey, T> LoadTable<TKey, T>(string text) 
            where T : class, ITableData<TKey>, new();
        
        /// <summary>
        /// Save a single data object as text
        /// </summary>
        string SaveSingle<T>(T data) where T : class;
        
        /// <summary>
        /// Save table data as text
        /// </summary>
        string SaveTable<TKey, T>(Dictionary<TKey, T> table) 
            where T : class, ITableData<TKey>;
    }
}
