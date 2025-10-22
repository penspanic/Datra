using System;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Services;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Wrapper for LocalizationContext to implement IDataRepository interface.
    /// This allows localization to use the same save/load infrastructure as other data types.
    /// </summary>
    public class LocalizationRepository : IDataRepository
    {
        private readonly LocalizationContext _localizationContext;
        private readonly string _localizationDataPath;

        public LocalizationContext Context => _localizationContext;

        public LocalizationRepository(LocalizationContext localizationContext, string localizationDataPath = "Localizations")
        {
            _localizationContext = localizationContext ?? throw new ArgumentNullException(nameof(localizationContext));
            _localizationDataPath = localizationDataPath;
        }

        /// <summary>
        /// Load is not needed as LocalizationContext is already loaded
        /// </summary>
        public Task LoadAsync()
        {
            // LocalizationContext is already loaded when it's created
            // No additional loading needed
            return Task.CompletedTask;
        }

        /// <summary>
        /// Saves both the current language data and key repository
        /// </summary>
        public async Task SaveAsync()
        {
            // Save current language data (e.g., en.csv, ko.csv, etc.)
            await _localizationContext.SaveCurrentLanguageAsync();

            // Save key repository (LocalizationKeys.csv)
            var keyRepo = _localizationContext.KeyRepository;
            if (keyRepo != null)
            {
                await keyRepo.SaveAsync();
            }
        }

        /// <summary>
        /// Gets the path to the currently loaded language file
        /// </summary>
        public string GetLoadedFilePath()
        {
            // Return the path to the current language file
            var currentLanguageCode = _localizationContext.CurrentLanguageCode;
            var fileName = currentLanguageCode.GetFileName();
            return System.IO.Path.Combine(_localizationDataPath, fileName);
        }
    }
}
