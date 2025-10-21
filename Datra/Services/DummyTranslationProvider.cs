using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Localization;

namespace Datra.Services
{
    /// <summary>
    /// Default dummy translation provider that doesn't perform actual translation.
    /// This is used as a fallback when no custom translation provider is provided.
    /// Users should implement ITranslationProvider to integrate with real translation services.
    /// </summary>
    public class DummyTranslationProvider : ITranslationProvider
    {
        /// <summary>
        /// Simulates translation by prefixing the text with [AUTO-{language}]
        /// </summary>
        public async Task<string> TranslateAsync(string text, LanguageCode sourceLanguage, LanguageCode targetLanguage)
        {
            // Simulate async operation
            await Task.Delay(100);

            // Return dummy translation with language prefix
            return $"[AUTO-{targetLanguage.ToIsoCode()}] {text}";
        }

        /// <summary>
        /// Dummy provider supports all language pairs
        /// </summary>
        public bool SupportsLanguagePair(LanguageCode sourceLanguage, LanguageCode targetLanguage)
        {
            return true;
        }
    }
}
