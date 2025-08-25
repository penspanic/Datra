using System.Threading.Tasks;
using Datra.Localization;

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
        /// <param name="languageCode">The language code to load</param>
        Task LoadLanguageAsync(LanguageCode languageCode);
        
        /// <summary>
        /// Loads localization data for the specified language (string overload)
        /// </summary>
        /// <param name="languageCode">The language code string to load (e.g., "en", "ko", "ja")</param>
        Task LoadLanguageAsync(string languageCode);
        
        /// <summary>
        /// Checks if a localization key exists
        /// </summary>
        /// <param name="key">The localization key to check</param>
        /// <returns>True if the key exists, false otherwise</returns>
        bool HasKey(string key);
    }
}