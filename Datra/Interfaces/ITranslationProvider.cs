using System.Threading.Tasks;
using Datra.Localization;

namespace Datra.Interfaces
{
    /// <summary>
    /// Interface for translation providers that can translate text between languages.
    /// Users can implement this interface to integrate with translation services like Google Translate, DeepL, etc.
    /// </summary>
    public interface ITranslationProvider
    {
        /// <summary>
        /// Translate text from source language to target language
        /// </summary>
        /// <param name="text">The text to translate</param>
        /// <param name="sourceLanguage">The source language code</param>
        /// <param name="targetLanguage">The target language code</param>
        /// <returns>The translated text</returns>
        Task<string> TranslateAsync(string text, LanguageCode sourceLanguage, LanguageCode targetLanguage);

        /// <summary>
        /// Check if this provider supports translation between the given language pair
        /// </summary>
        /// <param name="sourceLanguage">The source language code</param>
        /// <param name="targetLanguage">The target language code</param>
        /// <returns>True if the language pair is supported, false otherwise</returns>
        bool SupportsLanguagePair(LanguageCode sourceLanguage, LanguageCode targetLanguage);
    }
}
