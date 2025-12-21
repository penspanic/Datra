using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.Models
{
    /// <summary>
    /// Master localization key definition
    /// </summary>

    public class LocalizationKeyData : ITableData<string>
    {
        /// <summary>
        /// The unique localization key (e.g., "Button_Start", "Message_Welcome")
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this key is used for
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Category for grouping keys (e.g., "UI", "Dialog", "System")
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the locale key is fixed (non-editable).
        /// When true, the key cannot be modified, but locale values can still be edited.
        /// </summary>
        public bool IsFixedKey { get; set; } = false;
    }
}