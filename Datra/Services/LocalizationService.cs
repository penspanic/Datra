using System;
using System.Threading.Tasks;
using Datra.Interfaces;

namespace Datra.Services
{
    /// <summary>
    /// Service for managing localization and providing localized text
    /// </summary>
    public class LocalizationService : ILocalizationService
    {
        private readonly ILocalizationContext _context;
        private string _currentLanguage;
        
        /// <summary>
        /// Gets the current language code
        /// </summary>
        public string CurrentLanguage => _currentLanguage;
        
        /// <summary>
        /// Creates a new LocalizationService with the specified context
        /// </summary>
        /// <param name="context">The localization context to use</param>
        public LocalizationService(ILocalizationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currentLanguage = context.CurrentLanguage;
        }
        
        /// <summary>
        /// Sets the current language and loads its localization data
        /// </summary>
        /// <param name="languageCode">The language code to set (e.g., "Korean", "English")</param>
        public async Task SetLanguageAsync(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                throw new ArgumentNullException(nameof(languageCode));
            
            await _context.LoadLanguageAsync(languageCode);
            _currentLanguage = languageCode;
        }
        
        /// <summary>
        /// Gets localized text for the specified key
        /// </summary>
        /// <param name="key">The localization key</param>
        /// <returns>Localized text or fallback value if not found</returns>
        public string Localize(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            
            return _context.GetText(key);
        }
        
        /// <summary>
        /// Gets localized text for the specified key with formatting
        /// </summary>
        /// <param name="key">The localization key</param>
        /// <param name="args">Format arguments</param>
        /// <returns>Formatted localized text</returns>
        public string Localize(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            
            var text = _context.GetText(key);
            
            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(text, args);
                }
                catch (FormatException)
                {
                    // Return unformatted text if formatting fails
                    return text;
                }
            }
            
            return text;
        }
        
        /// <summary>
        /// Checks if a localization key exists
        /// </summary>
        /// <param name="key">The localization key to check</param>
        /// <returns>True if the key exists, false otherwise</returns>
        public bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            
            return _context.HasKey(key);
        }
    }
}