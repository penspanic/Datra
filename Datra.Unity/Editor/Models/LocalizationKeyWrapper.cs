using Datra.Interfaces;
using Datra.Models;

namespace Datra.Unity.Editor.Models
{
    /// <summary>
    /// Wrapper class for localization keys to make them compatible with DatraDataView.
    /// Each instance represents a single localization key with its text value for the current language.
    /// </summary>
    public class LocalizationKeyWrapper : ITableData<string>
    {
        /// <summary>
        /// The localization key (e.g., "Button_Start", "Character_Hero_Name")
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The localized text value for the current language
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Context information about where this key is used
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// Whether this is a fixed key that cannot be deleted
        /// </summary>
        public bool IsFixedKey { get; set; }

        /// <summary>
        /// Description of what this key is used for
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Category for grouping keys (e.g., "UI", "Dialog", "System")
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Creates a new LocalizationKeyWrapper
        /// </summary>
        /// <param name="key">The localization key</param>
        /// <param name="text">The localized text value</param>
        /// <param name="keyData">Optional metadata about the key</param>
        /// <param name="context">Optional context information</param>
        public LocalizationKeyWrapper(string key, string text, LocalizationKeyData keyData = null, string context = "")
        {
            Id = key;
            Text = text ?? "";
            Context = context ?? "";
            IsFixedKey = keyData?.IsFixedKey ?? false;
            Description = keyData?.Description ?? "";
            Category = keyData?.Category ?? "";
        }

        /// <summary>
        /// Creates a new empty LocalizationKeyWrapper
        /// </summary>
        public LocalizationKeyWrapper()
        {
            Id = "";
            Text = "";
            Context = "";
            IsFixedKey = false;
            Description = "";
            Category = "";
        }
    }
}
