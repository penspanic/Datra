using System.Threading.Tasks;

namespace Datra.Interfaces
{
    /// <summary>
    /// Defines the contract for localization context that manages localized text
    /// </summary>
    public interface ILocalizationContext
    {
        /// <summary>
        /// Gets the current language code
        /// </summary>
        string CurrentLanguage { get; }
        
        /// <summary>
        /// Gets localized text for the specified key
        /// </summary>
        /// <param name="key">The localization key</param>
        /// <returns>Localized text or fallback value if not found</returns>
        string GetText(string key);
        
        /// <summary>
        /// Loads localization data for the specified language
        /// </summary>
        /// <param name="languageCode">The language code to load (e.g., "Korean", "English")</param>
        Task LoadLanguageAsync(string languageCode);
        
        /// <summary>
        /// Checks if a localization key exists
        /// </summary>
        /// <param name="key">The localization key to check</param>
        /// <returns>True if the key exists, false otherwise</returns>
        bool HasKey(string key);
    }
}