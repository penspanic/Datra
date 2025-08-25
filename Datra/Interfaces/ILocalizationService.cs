using System.Threading.Tasks;

namespace Datra.Interfaces
{
    /// <summary>
    /// Defines the contract for localization service that provides localized text
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>
        /// Gets the current language code
        /// </summary>
        string CurrentLanguage { get; }
        
        /// <summary>
        /// Sets the current language and loads its localization data
        /// </summary>
        /// <param name="languageCode">The language code to set (e.g., "Korean", "English")</param>
        Task SetLanguageAsync(string languageCode);
        
        /// <summary>
        /// Gets localized text for the specified key
        /// </summary>
        /// <param name="key">The localization key</param>
        /// <returns>Localized text or fallback value if not found</returns>
        string Localize(string key);
        
        /// <summary>
        /// Gets localized text for the specified key with formatting
        /// </summary>
        /// <param name="key">The localization key</param>
        /// <param name="args">Format arguments</param>
        /// <returns>Formatted localized text</returns>
        string Localize(string key, params object[] args);
        
        /// <summary>
        /// Checks if a localization key exists
        /// </summary>
        /// <param name="key">The localization key to check</param>
        /// <returns>True if the key exists, false otherwise</returns>
        bool HasKey(string key);
    }
}