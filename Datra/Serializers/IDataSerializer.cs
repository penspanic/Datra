using System.Collections.Generic;
using Datra.Interfaces;

namespace Datra.Serializers
{
    /// <summary>
    /// Base interface for data serializers
    /// Implemented for each format like JSON, YAML, etc.
    /// </summary>
    public interface IDataSerializer
    {
        /// <summary>
        /// Deserialize a single data object
        /// </summary>
        T DeserializeSingle<T>(string text) where T : class, new();
        
        /// <summary>
        /// Deserialize table data (Key-Value Dictionary)
        /// </summary>
        Dictionary<TKey, T> DeserializeTable<TKey, T>(string text) 
            where T : class, ITableData<TKey>, new();
        
        /// <summary>
        /// Serialize a single data object to text
        /// </summary>
        string SerializeSingle<T>(T data) where T : class;
        
        /// <summary>
        /// Serialize table data to text
        /// </summary>
        string SerializeTable<TKey, T>(Dictionary<TKey, T> table) 
            where T : class, ITableData<TKey>;
    }
}
