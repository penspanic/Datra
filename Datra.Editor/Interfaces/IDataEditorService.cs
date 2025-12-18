#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Interfaces;

namespace Datra.Editor.Interfaces
{
    /// <summary>
    /// Service for data editing operations (load, save, reload).
    /// Abstracts data access for testability and platform independence.
    /// </summary>
    public interface IDataEditorService
    {
        /// <summary>
        /// The underlying data context
        /// </summary>
        IDataContext DataContext { get; }

        /// <summary>
        /// All registered repositories by data type
        /// </summary>
        IReadOnlyDictionary<Type, IDataRepository> Repositories { get; }

        /// <summary>
        /// Get metadata for all registered data types
        /// </summary>
        IReadOnlyList<DataTypeInfo> GetDataTypeInfos();

        /// <summary>
        /// Get repository for a specific data type
        /// </summary>
        IDataRepository? GetRepository(Type dataType);

        /// <summary>
        /// Save data for a specific type
        /// </summary>
        /// <param name="dataType">Type to save</param>
        /// <param name="forceSave">If true, save even if no changes detected</param>
        /// <returns>True if save succeeded</returns>
        Task<bool> SaveAsync(Type dataType, bool forceSave = false);

        /// <summary>
        /// Save all modified data
        /// </summary>
        /// <param name="forceSave">If true, save all types regardless of changes</param>
        /// <returns>True if all saves succeeded</returns>
        Task<bool> SaveAllAsync(bool forceSave = false);

        /// <summary>
        /// Reload data for a specific type from source
        /// </summary>
        Task<bool> ReloadAsync(Type dataType);

        /// <summary>
        /// Reload all data from source
        /// </summary>
        Task<bool> ReloadAllAsync();

        /// <summary>
        /// Check if a specific data type has unsaved changes
        /// </summary>
        bool HasChanges(Type dataType);

        /// <summary>
        /// Check if any data type has unsaved changes
        /// </summary>
        bool HasAnyChanges();

        /// <summary>
        /// Get all data types that have unsaved changes
        /// </summary>
        IEnumerable<Type> GetModifiedTypes();

        /// <summary>
        /// Raised when data changes (after save/reload)
        /// </summary>
        event Action<Type>? OnDataChanged;

        /// <summary>
        /// Raised when modified state changes for a data type
        /// </summary>
        event Action<Type, bool>? OnModifiedStateChanged;
    }
}
