#nullable disable
using Datra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;
using Datra.Editor.DataSources;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Services;

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
        public IReadOnlyDictionary<Type, IEditableRepository> Repositories => _manager.Repositories;

        public event Action<Type> OnDataChanged;
        public event Action<Type, bool> OnModifiedStateChanged;

        public IReadOnlyList<DataTypeInfo> GetDataTypeInfos()
        {
            return DataContext?.GetDataTypeInfos()?.ToList() ?? new List<DataTypeInfo>();
        }

        public IEditableRepository GetRepository(Type dataType)
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
    /// Adapter that wraps EditableLocalizationDataSource to expose ILocaleEditorService.
    /// </summary>
    public class LocalizationEditorServiceAdapter : ILocaleEditorService
    {
        private readonly EditableLocalizationDataSource _dataSource;
        private readonly DatraDataManager _manager;

        public LocalizationEditorServiceAdapter(
            EditableLocalizationDataSource dataSource,
            DatraDataManager manager)
        {
            _dataSource = dataSource;
            _manager = manager;

            if (_dataSource != null)
            {
                _dataSource.OnTextChanged += (key, language) =>
                    OnTextChanged?.Invoke(key, language);

                _dataSource.OnLanguageChanged += language =>
                    OnLanguageChanged?.Invoke(language);

                _dataSource.OnModifiedStateChanged += hasChanges =>
                    OnModifiedStateChanged?.Invoke(hasChanges);
            }
        }

        public LocalizationContext Context => _dataSource?.Context;
        public bool IsAvailable => _dataSource != null;
        public LanguageCode CurrentLanguage => _dataSource?.CurrentLanguage ?? default;

        public IReadOnlyList<LanguageCode> AvailableLanguages =>
            _dataSource?.AvailableLanguages ?? new List<LanguageCode>();

        public IReadOnlyList<LanguageCode> LoadedLanguages =>
            _dataSource?.LoadedLanguages ?? new List<LanguageCode>();

        public event Action<string, LanguageCode> OnTextChanged;
        public event Action<LanguageCode> OnLanguageChanged;
        public event Action<bool> OnModifiedStateChanged;

        public async Task SwitchLanguageAsync(LanguageCode language)
        {
            if (_dataSource == null) return;
            await _dataSource.SwitchLanguageAsync(language);
        }

        public async Task LoadAllLanguagesAsync()
        {
            if (_dataSource == null || Context == null) return;
            await Context.LoadAllAvailableLanguagesAsync();

            foreach (var language in LoadedLanguages)
            {
                InitializeBaseline(language);
            }
        }

        public string GetText(string key) => _dataSource?.GetText(key) ?? string.Empty;

        public string GetText(string key, LanguageCode language) =>
            _dataSource?.GetText(key, language) ?? string.Empty;

        public void SetText(string key, string value, LanguageCode language)
        {
            _dataSource?.SetText(key, value, language);
        }

        public bool HasUnsavedChanges() => _dataSource?.HasModifications ?? false;

        public bool HasUnsavedChanges(LanguageCode language) =>
            _dataSource?.HasLanguageModifications(language) ?? false;

        public async Task<bool> SaveAsync(bool forceSave = false)
        {
            // Use manager's save which handles localization
            return await _manager.SaveAsync(typeof(LocalizationContext), forceSave);
        }

        public async Task<bool> SaveAsync(LanguageCode language, bool forceSave = false)
        {
            if (_dataSource == null) return false;

            try
            {
                await _dataSource.SaveCurrentLanguageAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void InitializeBaseline(LanguageCode language)
        {
            if (_dataSource != null && !_dataSource.IsLanguageInitialized(language))
            {
                _dataSource.InitializeBaseline(language);
            }
        }

        public void InitializeAllBaselines()
        {
            if (_dataSource == null) return;

            foreach (var language in LoadedLanguages)
            {
                InitializeBaseline(language);
            }
        }
    }
}
