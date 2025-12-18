#nullable enable
using System;

namespace Datra.Editor.Interfaces
{
    /// <summary>
    /// Interface for components that can notify when modified state changes.
    /// Used by change trackers to signal when data becomes dirty or clean.
    /// </summary>
    public interface INotifyModifiedStateChanged
    {
        /// <summary>
        /// Raised when the modified state changes.
        /// Parameter is true if there are unsaved changes, false otherwise.
        /// </summary>
        event Action<bool>? OnModifiedStateChanged;
    }
}
