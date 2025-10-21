using System.Collections.Generic;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Non-generic interface for RepositoryChangeTracker to avoid reflection
    /// </summary>
    public interface IRepositoryChangeTracker
    {
        /// <summary>
        /// Check if there are any modifications
        /// </summary>
        bool HasModifications { get; }

        /// <summary>
        /// Get baseline value for a given key (returns object)
        /// </summary>
        object GetBaselineValue(object key);

        /// <summary>
        /// Track a change for a given key and value
        /// </summary>
        void TrackChange(object key, object value);

        /// <summary>
        /// Track a property change for a given key, property name, and new value
        /// </summary>
        void TrackPropertyChange(object key, string propertyName, object newValue, out bool isModified);

        /// <summary>
        /// Track an addition
        /// </summary>
        void TrackAdd(object key, object value);

        /// <summary>
        /// Track a deletion
        /// </summary>
        void TrackDelete(object key);

        /// <summary>
        /// Check if a specific key is modified
        /// </summary>
        bool IsModified(object key);

        /// <summary>
        /// Check if a specific property is modified
        /// </summary>
        bool IsPropertyModified(object key, string propertyName);

        /// <summary>
        /// Check if a specific key is added
        /// </summary>
        bool IsAdded(object key);

        /// <summary>
        /// Check if a specific key is deleted
        /// </summary>
        bool IsDeleted(object key);

        /// <summary>
        /// Get all modified property names for a given key
        /// </summary>
        IEnumerable<string> GetModifiedProperties(object key);

        /// <summary>
        /// Get baseline value for a specific property
        /// </summary>
        object GetPropertyBaselineValue(object key, string propertyName);

        /// <summary>
        /// Initialize baseline from repository data
        /// </summary>
        void InitializeBaseline(object repositoryData);

        /// <summary>
        /// Update baseline to current state (call after save)
        /// </summary>
        void UpdateBaseline(object repositoryData);

        /// <summary>
        /// Revert all changes to baseline
        /// </summary>
        void RevertAll();
    }
}
