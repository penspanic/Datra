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
        public string Id { get; set; }
        
        /// <summary>
        /// Description of what this key is used for
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Category for grouping keys (e.g., "UI", "Dialog", "System")
        /// </summary>
        public string Category { get; set; }
    }
}