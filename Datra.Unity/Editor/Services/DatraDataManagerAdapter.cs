using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Services;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Services
{
    /// <summary>
    /// Adapter that wraps existing DatraDataManager to expose IDataEditorService.
    /// This enables gradual migration to the new architecture.
    /// </summary>
    public class DatraDataManagerAdapter : IDataEditorService
    {
        private readonly DatraDataManager _manager;

        public DatraDataManagerAdapter(DatraDataManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));

            // Forward events
            _manager.OnDataChanged += type => OnDataChanged?.Invoke(type);
            _manager.OnModifiedStateChanged += (type, hasChanges) => OnModifiedStateChanged?.Invoke(type, hasChanges);
        }

        #region IDataEditorService Implementation

        public IDataContext DataContext => _manager.DataContext;
        public IReadOnlyDictionary<Type, IDataRepository> Repositories => _manager.Repositories;

        public event Action<Type> OnDataChanged;
        public event Action<Type, bool> OnModifiedStateChanged;

        public IReadOnlyList<DataTypeInfo> GetDataTypeInfos()
        {
            return DataContext?.GetDataTypeInfos()?.ToList() ?? new List<DataTypeInfo>();
        }

        public IDataRepository GetRepository(Type dataType)
        {
            return _manager.GetRepository(dataType);
        }

        public Task<bool> SaveAsync(Type dataType, bool forceSave = false)
        {
            return _manager.SaveAsync(dataType, forceSave);
        }

        public Task<bool> SaveAllAsync(bool forceSave = false)
        {
            return _manager.SaveAllAsync(forceSave);
        }

        public async Task<bool> ReloadAsync(Type dataType)
        {
            // DatraDataManager doesn't have single type reload, use ReloadAll
            return await _manager.ReloadAllAsync(checkModified: false);
        }

        public Task<bool> ReloadAllAsync()
        {
            return _manager.ReloadAllAsync(checkModified: false);
        }

        public bool HasChanges(Type dataType)
        {
            return _manager.HasUnsavedChanges(dataType);
        }

        public bool HasAnyChanges()
        {
            return _manager.Repositories.Keys.Any(t => _manager.HasUnsavedChanges(t));
        }

        public IEnumerable<Type> GetModifiedTypes()
        {
            return _manager.Repositories.Keys.Where(t => _manager.HasUnsavedChanges(t));
        }

        #endregion

        /// <summary>
        /// Get the underlying DatraDataManager for legacy access
        /// </summary>
        public DatraDataManager UnderlyingManager => _manager;
    }

    /// <summary>
    /// Adapter that wraps LocalizationChangeTracker to expose ILocaleEditorService.
    /// </summary>
    public class LocalizationEditorServiceAdapter : ILocaleEditorService
    {
        private readonly LocalizationContext _context;
        private readonly LocalizationChangeTracker _changeTracker;
        private readonly DatraDataManager _manager;

        public LocalizationEditorServiceAdapter(
            LocalizationContext context,
            LocalizationChangeTracker changeTracker,
            DatraDataManager manager)
        {
            _context = context;
            _changeTracker = changeTracker;
            _manager = manager;

            if (_context != null)
            {
                SubscribeToContextEvents();
            }

            if (_changeTracker is INotifyModifiedStateChanged notifyTracker)
            {
                notifyTracker.OnModifiedStateChanged += hasChanges =>
                    OnModifiedStateChanged?.Invoke(hasChanges);
            }
        }

        private void SubscribeToContextEvents()
        {
            _context.SubscribeToEditorEvents(
                onTextChanged: (key, language) => OnTextChanged?.Invoke(key, language),
                onKeyAdded: key => OnTextChanged?.Invoke(key, CurrentLanguage),
                onKeyDeleted: key => OnTextChanged?.Invoke(key, CurrentLanguage)
            );
        }

        public LocalizationContext Context => _context;
        public bool IsAvailable => _context != null;
        public LanguageCode CurrentLanguage => _context?.CurrentLanguageCode ?? default;

        public IReadOnlyList<LanguageCode> AvailableLanguages =>
            _context?.GetAvailableLanguages()?.ToList() ?? new List<LanguageCode>();

        public IReadOnlyList<LanguageCode> LoadedLanguages =>
            _context?.GetLoadedLanguages()?.ToList() ?? new List<LanguageCode>();

        public event Action<string, LanguageCode> OnTextChanged;
        public event Action<LanguageCode> OnLanguageChanged;
        public event Action<bool> OnModifiedStateChanged;

        public async Task SwitchLanguageAsync(LanguageCode language)
        {
            if (_context == null) return;
            await _context.LoadLanguageAsync(language);
            OnLanguageChanged?.Invoke(language);
        }

        public async Task LoadAllLanguagesAsync()
        {
            if (_context == null) return;
            await _context.LoadAllAvailableLanguagesAsync();

            foreach (var language in LoadedLanguages)
            {
                InitializeBaseline(language);
            }
        }

        public string GetText(string key) => _context?.GetText(key) ?? string.Empty;

        public string GetText(string key, LanguageCode language) =>
            _context?.GetText(key, language) ?? string.Empty;

        public void SetText(string key, string value, LanguageCode language)
        {
            _context?.SetText(key, value, language);
        }

        public bool HasUnsavedChanges() => _changeTracker?.HasModifications() ?? false;

        public bool HasUnsavedChanges(LanguageCode language) =>
            _changeTracker?.HasModifications(language) ?? false;

        public async Task<bool> SaveAsync(bool forceSave = false)
        {
            // Use manager's save which handles localization
            return await _manager.SaveAsync(typeof(LocalizationContext), forceSave);
        }

        public async Task<bool> SaveAsync(LanguageCode language, bool forceSave = false)
        {
            if (_context == null) return false;

            try
            {
                await _context.SaveLanguageAsync(language);
                _changeTracker?.UpdateBaseline(language);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void InitializeBaseline(LanguageCode language)
        {
            if (_changeTracker != null && !_changeTracker.IsLanguageInitialized(language))
            {
                _changeTracker.InitializeLanguage(language);
            }
        }

        public void InitializeAllBaselines()
        {
            if (_changeTracker == null) return;

            foreach (var language in LoadedLanguages)
            {
                InitializeBaseline(language);
            }
        }
    }
}
