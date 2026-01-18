using Datra;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.Services;

namespace Datra.Unity.Editor.ViewModels
{
    /// <summary>
    /// ViewModel for DatraEditorWindow.
    /// Pure C# class with no Unity dependencies, fully testable.
    /// </summary>
    public class DatraEditorViewModel : INotifyPropertyChanged
    {
        // Services
        private readonly IDataEditorService _dataService;
        private readonly ILocaleEditorService? _localization;

        // State
        private Type? _selectedDataType;
        private bool _isLocalizationSelected;
        private bool _isInitialized;
        private string _projectName = "";
        private readonly List<TabViewModel> _openTabs = new List<TabViewModel>();
        private TabViewModel? _activeTab;

        // Commands (simple delegate-based)
        public Func<Task<bool>> SaveCommand { get; }
        public Func<Task<bool>> SaveAllCommand { get; }
        public Func<Task<bool>> ReloadCommand { get; }
        public Action<Type> SelectDataTypeCommand { get; }
        public Action SelectLocalizationCommand { get; }

        // Properties
        public Type? SelectedDataType
        {
            get => _selectedDataType;
            private set => SetField(ref _selectedDataType, value);
        }

        public bool IsLocalizationSelected
        {
            get => _isLocalizationSelected;
            private set => SetField(ref _isLocalizationSelected, value);
        }

        public bool IsInitialized
        {
            get => _isInitialized;
            private set => SetField(ref _isInitialized, value);
        }

        public string ProjectName
        {
            get => _projectName;
            set => SetField(ref _projectName, value);
        }

        public bool HasAnyUnsavedChanges =>
            _dataService?.HasAnyChanges() == true ||
            _localization?.HasUnsavedChanges() == true;

        public bool HasCurrentDataUnsavedChanges
        {
            get
            {
                if (IsLocalizationSelected)
                    return _localization?.HasUnsavedChanges() == true;
                if (SelectedDataType != null)
                    return _dataService?.HasChanges(SelectedDataType) == true;
                return false;
            }
        }

        public IReadOnlyList<TabViewModel> OpenTabs => _openTabs;
        public TabViewModel? ActiveTab
        {
            get => _activeTab;
            private set => SetField(ref _activeTab, value);
        }

        public IReadOnlyList<DataTypeInfo> DataTypes => _dataService.GetDataTypeInfos();
        public IDataEditorService DataService => _dataService;
        public ILocaleEditorService? Localization => _localization;

        // Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<string> OnOperationCompleted;
        public event Action<string> OnOperationFailed;
        public event Action<Type, bool> OnModifiedStateChanged;
        public event Action<TabViewModel> OnTabOpened;
        public event Action<TabViewModel> OnTabClosed;
        public event Action<TabViewModel> OnActiveTabChanged;

        public DatraEditorViewModel(
            IDataEditorService dataService,
            ILocaleEditorService localization = null)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _localization = localization;

            // Setup commands
            SaveCommand = SaveCurrentAsync;
            SaveAllCommand = SaveAllAsync;
            ReloadCommand = ReloadAsync;
            SelectDataTypeCommand = SelectDataType;
            SelectLocalizationCommand = SelectLocalization;

            // Subscribe to service events
            SubscribeToEvents();

            IsInitialized = true;
        }

        private void SubscribeToEvents()
        {
            if (_dataService != null)
            {
                _dataService.OnModifiedStateChanged += (type, hasChanges) =>
                {
                    OnModifiedStateChanged?.Invoke(type, hasChanges);
                    OnPropertyChanged(nameof(HasAnyUnsavedChanges));
                    OnPropertyChanged(nameof(HasCurrentDataUnsavedChanges));
                };
            }

            if (_localization != null)
            {
                _localization.OnModifiedStateChanged += (hasChanges) =>
                {
                    OnModifiedStateChanged?.Invoke(typeof(LocalizationContext), hasChanges);
                    OnPropertyChanged(nameof(HasAnyUnsavedChanges));
                    OnPropertyChanged(nameof(HasCurrentDataUnsavedChanges));
                };
            }
        }

        // Commands Implementation

        public void SelectDataType(Type dataType)
        {
            SelectedDataType = dataType;
            IsLocalizationSelected = false;
            OnPropertyChanged(nameof(HasCurrentDataUnsavedChanges));
        }

        public void SelectLocalization()
        {
            SelectedDataType = null;
            IsLocalizationSelected = true;
            OnPropertyChanged(nameof(HasCurrentDataUnsavedChanges));
        }

        public async Task<bool> SaveCurrentAsync()
        {
            if (IsLocalizationSelected)
            {
                return await SaveLocalizationAsync();
            }

            if (SelectedDataType == null)
            {
                OnOperationFailed?.Invoke("No data type selected.");
                return false;
            }

            var success = await _dataService.SaveAsync(SelectedDataType);
            if (success)
            {
                OnOperationCompleted?.Invoke($"{SelectedDataType.Name} saved successfully!");
            }
            else
            {
                OnOperationFailed?.Invoke($"Failed to save {SelectedDataType.Name}.");
            }
            return success;
        }

        public async Task<bool> SaveAllAsync()
        {
            var dataSuccess = await _dataService.SaveAllAsync();
            var locSuccess = _localization != null ? await _localization.SaveAsync() : true;

            var success = dataSuccess && locSuccess;
            if (success)
            {
                OnOperationCompleted?.Invoke("All data saved successfully!");
            }
            else
            {
                OnOperationFailed?.Invoke("Some data failed to save.");
            }
            return success;
        }

        public async Task<bool> ForceSaveCurrentAsync()
        {
            if (IsLocalizationSelected)
            {
                return await ForceSaveLocalizationAsync();
            }

            if (SelectedDataType == null)
            {
                OnOperationFailed?.Invoke("No data type selected.");
                return false;
            }

            var success = await _dataService.SaveAsync(SelectedDataType, forceSave: true);
            if (success)
            {
                OnOperationCompleted?.Invoke($"{SelectedDataType.Name} force saved successfully!");
            }
            else
            {
                OnOperationFailed?.Invoke($"Failed to force save {SelectedDataType.Name}.");
            }
            return success;
        }

        public async Task<bool> ForceSaveAllAsync()
        {
            var dataSuccess = await _dataService.SaveAllAsync(forceSave: true);
            var locSuccess = _localization != null ? await _localization.SaveAsync(forceSave: true) : true;

            var success = dataSuccess && locSuccess;
            if (success)
            {
                OnOperationCompleted?.Invoke("All data force saved successfully!");
            }
            else
            {
                OnOperationFailed?.Invoke("Some data failed to force save.");
            }
            return success;
        }

        public async Task<bool> ReloadAsync()
        {
            if (HasAnyUnsavedChanges)
            {
                // Caller should handle confirmation dialog
                // For now, just proceed
            }

            var success = await _dataService.ReloadAllAsync();
            if (success)
            {
                OnOperationCompleted?.Invoke("Data reloaded successfully!");
            }
            else
            {
                OnOperationFailed?.Invoke("Failed to reload data.");
            }
            return success;
        }

        private async Task<bool> SaveLocalizationAsync()
        {
            if (_localization == null)
            {
                OnOperationFailed?.Invoke("Localization service not available.");
                return false;
            }
            var success = await _localization.SaveAsync();
            if (success)
            {
                OnOperationCompleted?.Invoke("Localization saved successfully!");
            }
            else
            {
                OnOperationFailed?.Invoke("Failed to save localization.");
            }
            return success;
        }

        private async Task<bool> ForceSaveLocalizationAsync()
        {
            if (_localization == null)
            {
                OnOperationFailed?.Invoke("Localization service not available.");
                return false;
            }
            var success = await _localization.SaveAsync(forceSave: true);
            if (success)
            {
                OnOperationCompleted?.Invoke("Localization force saved successfully!");
            }
            else
            {
                OnOperationFailed?.Invoke("Failed to force save localization.");
            }
            return success;
        }

        // Tab Management

        public TabViewModel OpenTab(Type dataType)
        {
            var existingTab = _openTabs.FirstOrDefault(t => t.DataType == dataType);
            if (existingTab != null)
            {
                ActivateTab(existingTab);
                return existingTab;
            }

            var repository = _dataService.GetRepository(dataType);
            var tab = new TabViewModel(dataType, repository, _dataService.DataContext);
            _openTabs.Add(tab);
            OnPropertyChanged(nameof(OpenTabs));
            OnTabOpened?.Invoke(tab);

            ActivateTab(tab);
            return tab;
        }

        public void ActivateTab(TabViewModel tab)
        {
            if (tab == null || ActiveTab == tab) return;

            ActiveTab = tab;
            OnActiveTabChanged?.Invoke(tab);
        }

        public void CloseTab(TabViewModel tab)
        {
            var index = _openTabs.IndexOf(tab);
            if (index < 0) return;

            _openTabs.Remove(tab);
            OnPropertyChanged(nameof(OpenTabs));
            OnTabClosed?.Invoke(tab);

            // If closing active tab, activate another one
            if (ActiveTab == tab)
            {
                if (_openTabs.Count > 0)
                {
                    var newIndex = Math.Min(index, _openTabs.Count - 1);
                    ActivateTab(_openTabs[newIndex]);
                }
                else
                {
                    ActiveTab = null;
                    OnActiveTabChanged?.Invoke(null);
                }
            }
        }

        public bool HasUnsavedChanges(Type dataType)
        {
            if (dataType == typeof(LocalizationContext))
                return _localization?.HasUnsavedChanges() == true;
            return _dataService?.HasChanges(dataType) == true;
        }

        // INotifyPropertyChanged Implementation

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// ViewModel for a single data tab
    /// </summary>
    public class TabViewModel
    {
        public Type DataType { get; }
        public IEditableRepository Repository { get; }
        public IDataContext DataContext { get; }
        public string DisplayName => DataType?.Name ?? "Unknown";
        public bool IsModified { get; set; }

        public TabViewModel(Type dataType, IEditableRepository repository, IDataContext dataContext)
        {
            DataType = dataType;
            Repository = repository;
            DataContext = dataContext;
        }
    }
}
