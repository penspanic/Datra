#nullable enable
using System;
using System.Collections.Generic;

namespace Datra.Editor.Interfaces
{
    /// <summary>
    /// Service for tracking changes to files.
    /// Provides testable change detection without platform dependencies.
    /// Tracks by file path for flexibility with multi-file scenarios.
    /// </summary>
    public interface IChangeTrackingService
    {
        /// <summary>
        /// Check if a specific file has unsaved changes
        /// </summary>
        bool HasUnsavedChanges(DataFilePath filePath);

        /// <summary>
        /// Check if any tracked file has unsaved changes
        /// </summary>
        bool HasAnyUnsavedChanges();

        /// <summary>
        /// Get all files that have unsaved changes
        /// </summary>
        IEnumerable<DataFilePath> GetModifiedFiles();

        /// <summary>
        /// Initialize baseline for change tracking (call after load/save)
        /// </summary>
        /// <param name="filePath">File path to track</param>
        /// <param name="contentProvider">Function to get current content for hashing</param>
        void InitializeBaseline(DataFilePath filePath, Func<string> contentProvider);

        /// <summary>
        /// Initialize baselines for all tracked files
        /// </summary>
        void InitializeAllBaselines();

        /// <summary>
        /// Reset changes for a specific file (accept current state as baseline)
        /// </summary>
        void ResetChanges(DataFilePath filePath);

        /// <summary>
        /// Register a file for change tracking
        /// </summary>
        /// <param name="filePath">The file path to track</param>
        /// <param name="contentProvider">Function to get current content for hashing</param>
        void RegisterFile(DataFilePath filePath, Func<string> contentProvider);

        /// <summary>
        /// Unregister a file from change tracking
        /// </summary>
        void UnregisterFile(DataFilePath filePath);

        /// <summary>
        /// Check if a file is being tracked
        /// </summary>
        bool IsTracking(DataFilePath filePath);

        /// <summary>
        /// Raised when modified state changes for a file
        /// </summary>
        event Action<DataFilePath, bool>? OnModifiedStateChanged;
    }
}
