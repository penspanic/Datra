using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Services;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Wrapper for LocalizationContext to implement IEditableRepository interface.
    /// This allows localization to use the same save/load infrastructure as other data types.
    /// </summary>
    public class LocalizationRepository : IEditableRepository
    {
        private readonly LocalizationContext _localizationContext;
        private readonly string _localizationDataPath;
        private bool _isInitialized;

        public LocalizationContext Context => _localizationContext;
        public bool IsInitialized => _isInitialized;
        public string? LoadedFilePath => GetLoadedFilePath();

        public LocalizationRepository(LocalizationContext localizationContext, string localizationDataPath = "Localizations")
        {
            _localizationContext = localizationContext ?? throw new ArgumentNullException(nameof(localizationContext));
            _localizationDataPath = localizationDataPath;
        }

        /// <summary>
        /// Initialize is not needed as LocalizationContext is already loaded
        /// </summary>
        public Task InitializeAsync()
        {
            // LocalizationContext is already loaded when it's created
            // No additional loading needed
            _isInitialized = true;
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

        /// <summary>
        /// Enumerate all localization key data items
        /// </summary>
        public IEnumerable<object> EnumerateItems()
        {
            var keyRepo = _localizationContext.KeyRepository;
            if (keyRepo != null)
            {
                return keyRepo.LoadedItems.Values.Cast<object>();
            }
            return Enumerable.Empty<object>();
        }

        /// <summary>
        /// Number of localization keys
        /// </summary>
        public int ItemCount
        {
            get
            {
                var keyRepo = _localizationContext.KeyRepository;
                return keyRepo?.Count ?? 0;
            }
        }
    }
}
