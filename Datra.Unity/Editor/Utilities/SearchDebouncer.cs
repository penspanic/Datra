#nullable disable
using System;
using UnityEditor;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Debounces search input to avoid excessive filtering operations
    /// </summary>
    public class SearchDebouncer
    {
        private System.Threading.Timer timer;
        private readonly Action<string> callback;
        private readonly int delayMs;
        private string pendingValue;

        /// <summary>
        /// Creates a new search debouncer
        /// </summary>
        /// <param name="callback">Callback to invoke after delay</param>
        /// <param name="delayMs">Delay in milliseconds (default: 300ms)</param>
        public SearchDebouncer(Action<string> callback, int delayMs = 300)
        {
            this.callback = callback;
            this.delayMs = delayMs;
        }

        /// <summary>
        /// Triggers the debouncer with a new value
        /// </summary>
        public void Trigger(string value)
        {
            pendingValue = value;

            // Cancel previous timer
            timer?.Dispose();

            // Start new timer
            timer = new System.Threading.Timer(_ =>
            {
                // Execute callback on main thread
                EditorApplication.delayCall += () =>
                {
                    callback?.Invoke(pendingValue);
                };
            }, null, delayMs, System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// Immediately executes the pending callback and cancels the timer
        /// </summary>
        public void Flush()
        {
            timer?.Dispose();
            timer = null;

            if (pendingValue != null)
            {
                callback?.Invoke(pendingValue);
            }
        }

        /// <summary>
        /// Cancels any pending callbacks
        /// </summary>
        public void Cancel()
        {
            timer?.Dispose();
            timer = null;
            pendingValue = null;
        }

        /// <summary>
        /// Disposes the debouncer and cancels any pending callbacks
        /// </summary>
        public void Dispose()
        {
            Cancel();
        }
    }
}
