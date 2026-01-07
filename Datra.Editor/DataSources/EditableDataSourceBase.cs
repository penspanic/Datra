#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;

namespace Datra.Editor.DataSources
{
    /// <summary>
    /// Abstract base class for editable data sources.
    /// Provides standardized modification state tracking and event notification.
    ///
    /// Key design principle: All state-changing operations should use ExecuteWithNotification
    /// to ensure OnModifiedStateChanged events are never missed.
    /// </summary>
    public abstract class EditableDataSourceBase : IEditableDataSource
    {
        public event Action<bool>? OnModifiedStateChanged;

        #region Abstract Members

        /// <summary>
        /// Returns true if there are any pending modifications.
        /// Implementation should check adds, deletes, and property changes.
        /// </summary>
        public abstract bool HasModifications { get; }

        /// <summary>
        /// Number of items (including adds, excluding deletes)
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// Enumerate all items as objects
        /// </summary>
        public abstract IEnumerable<object> EnumerateItems();

        /// <summary>
        /// Get the state of an item by its key
        /// </summary>
        public abstract ItemState GetItemState(object key);

        /// <summary>
        /// Check if a specific property is modified for a given key
        /// </summary>
        public abstract bool IsPropertyModified(object key, string propertyName);

        /// <summary>
        /// Get all modified property names for a given key
        /// </summary>
        public abstract IEnumerable<string> GetModifiedProperties(object key);

        /// <summary>
        /// Get baseline (original) value for a specific property
        /// </summary>
        public abstract object? GetPropertyBaselineValue(object key, string propertyName);

        /// <summary>
        /// Revert all pending changes (internal implementation)
        /// </summary>
        protected abstract void RevertInternal();

        /// <summary>
        /// Apply all pending changes and save (internal implementation)
        /// </summary>
        protected abstract Task SaveInternalAsync();

        /// <summary>
        /// Refresh baseline from current data state (internal implementation).
        /// Called after save to reset modification tracking.
        /// </summary>
        protected abstract void RefreshBaselineInternal();

        /// <summary>
        /// Get the key for an item. Implementation must be provided by derived class.
        /// </summary>
        public abstract object? GetItemKey(object item);

        /// <summary>
        /// Track a property change (non-generic version).
        /// Implementation must be provided by derived class.
        /// </summary>
        public abstract void TrackPropertyChange(object key, string propertyName, object? newValue, out bool isPropertyModified);

        #endregion

        #region State Change Notification Helpers

        /// <summary>
        /// Execute an action that may change modification state, and notify if state changed.
        /// Use this wrapper for ALL state-changing operations.
        /// </summary>
        protected void ExecuteWithNotification(Action action)
        {
            bool hadModifications = HasModifications;
            action();
            NotifyIfStateChanged(hadModifications);
        }

        /// <summary>
        /// Execute an async action that may change modification state, and notify if state changed.
        /// Use this wrapper for ALL async state-changing operations.
        /// </summary>
        protected async Task ExecuteWithNotificationAsync(Func<Task> action)
        {
            bool hadModifications = HasModifications;
            await action();
            NotifyIfStateChanged(hadModifications);
        }

        /// <summary>
        /// Notify listeners if modification state changed.
        /// </summary>
        protected void NotifyIfStateChanged(bool hadModifications)
        {
            bool hasModifications = HasModifications;
            if (hadModifications != hasModifications)
            {
                OnModifiedStateChanged?.Invoke(hasModifications);
            }
        }

        /// <summary>
        /// Force notify that modifications have been cleared.
        /// Call this after RefreshBaselineInternal to ensure UI updates.
        /// </summary>
        protected void NotifyCleared()
        {
            OnModifiedStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Force notify current modification state.
        /// Use sparingly - prefer ExecuteWithNotification.
        /// </summary>
        protected void NotifyCurrentState()
        {
            OnModifiedStateChanged?.Invoke(HasModifications);
        }

        #endregion

        #region Public API (Final implementations using internal methods)

        /// <summary>
        /// Revert all pending changes. Automatically notifies state change.
        /// </summary>
        public void Revert()
        {
            ExecuteWithNotification(RevertInternal);
        }

        /// <summary>
        /// Refresh baseline from current data state.
        /// Call after external reload to reset modification tracking.
        /// </summary>
        public void RefreshBaseline()
        {
            RefreshBaselineInternal();
            NotifyCleared();
        }

        /// <summary>
        /// Apply all pending changes and save. Automatically refreshes baseline and notifies.
        /// </summary>
        public async Task SaveAsync()
        {
            await SaveInternalAsync();
            RefreshBaselineInternal();
            NotifyCleared();
        }

        #endregion
    }
}
