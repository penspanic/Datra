using System;
using System.Collections.Generic;

namespace Datra.Unity.Editor.Services
{
    /// <summary>
    /// Service interface for tracking changes to data.
    /// Provides testable change detection without Unity dependencies.
    /// </summary>
    public interface IChangeTrackingService
    {
        /// <summary>
        /// Check if a specific data type has unsaved changes
        /// </summary>
        bool HasUnsavedChanges(Type dataType);

        /// <summary>
        /// Check if any data type has unsaved changes
        /// </summary>
        bool HasAnyUnsavedChanges();

        /// <summary>
        /// Get all data types that have unsaved changes
        /// </summary>
        IEnumerable<Type> GetModifiedTypes();

        /// <summary>
        /// Initialize baseline for change tracking (call after load/save)
        /// </summary>
        void InitializeBaseline(Type dataType);

        /// <summary>
        /// Initialize baseline for all tracked types
        /// </summary>
        void InitializeAllBaselines();

        /// <summary>
        /// Reset changes for a specific type (discard modifications)
        /// </summary>
        void ResetChanges(Type dataType);

        /// <summary>
        /// Register a data type for change tracking
        /// </summary>
        void RegisterType(Type dataType, object repository);

        /// <summary>
        /// Unregister a data type from change tracking
        /// </summary>
        void UnregisterType(Type dataType);

        /// <summary>
        /// Raised when modified state changes for a data type
        /// </summary>
        event Action<Type, bool> OnModifiedStateChanged;
    }
}
