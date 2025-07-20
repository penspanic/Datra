using System;

namespace Datra.DataTypes
{
    /// <summary>
    /// Interface for data reference types
    /// </summary>
    public interface IDataRef
    {
        /// <summary>
        /// The type of the referenced data
        /// </summary>
        Type DataType { get; }
        
        /// <summary>
        /// The type of the key used for reference
        /// </summary>
        Type KeyType { get; }
        
        /// <summary>
        /// Whether the reference has a value
        /// </summary>
        bool HasValue { get; }
        
        /// <summary>
        /// Gets the key value as object
        /// </summary>
        object? GetKeyValue();
    }
    
    /// <summary>
    /// Generic interface for data reference types
    /// </summary>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <typeparam name="TData">The type of the referenced data</typeparam>
    public interface IDataRef<TKey, TData> : IDataRef
        where TData : class
    {
        /// <summary>
        /// The key value for the reference
        /// </summary>
        TKey Value { get; set; }
        
        /// <summary>
        /// Evaluates the reference and returns the actual data
        /// </summary>
        TData? Evaluate(BaseDataContext dataContext);
    }
}