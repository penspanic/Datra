#nullable disable
using Datra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Services;
using Datra.Unity.Editor.Panels;
using Datra.Unity.Editor.Services;
using Datra.Editor.Interfaces;
using Datra.Editor.DataSources;
using Datra.Unity.Editor.Utilities;
using Datra.Unity.Editor.ViewModels;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor
{
    public class DatraEditorWindow : EditorWindow
    {
        // UI Components
        private DatraToolbarPanel toolbar;
        private DatraNavigationPanel navigationPanel;
        private DataInspectorPanel dataInspectorPanel;
        private LocalizationInspectorPanel localizationInspectorPanel;
        private BaseInspectorPanel currentInspectorPanel;
        private TwoPaneSplitView splitView;
        private VisualElement tabContainer;
        private VisualElement contentArea;
        private VisualElement inspectorContainer;
        
        // Tab Management - UI mapping for ViewModel tabs
        private class DataTabUI
        {
            public TabViewModel ViewModel { get; set; }
            public VisualElement TabButton { get; set; }
        }

        private Dictionary<TabViewModel, DataTabUI> tabUIMap = new Dictionary<TabViewModel, DataTabUI>();
        
        // Data Management
        private IDataContext dataContext;
        private Dictionary<Type, IEditableRepository> repositories = new();
        private Dictionary<Type, DataTypeInfo> dataTypeInfoMap = new();
        private DatraDataManager dataManager;
        private LocalizationContext localizationContext;
        private EditableLocalizationDataSource localizationDataSource;

        // Editable data sources for transactional editing
        internal Dictionary<Type, IEditableDataSource> dataSources = new Dictionary<Type, IEditableDataSource>();

        // ViewModel and Services (new architecture)
        private DatraEditorViewModel viewModel;
        private DatraDataManagerAdapter dataServiceAdapter;
        private LocalizationEditorServiceAdapter localizationServiceAdapter;

        // Public accessors for navigation panel
        public IDataContext DataContext => dataContext;
        public IReadOnlyDictionary<Type, IEditableRepository> Repositories => repositories;
        public IReadOnlyDictionary<Type, DataTypeInfo> DataTypeInfoMap => dataTypeInfoMap;

        // Public accessor for ViewModel (enables testing and external access)
        public DatraEditorViewModel ViewModel => viewModel;

        // Public accessor for LocalizationContext (enables PropertyDrawer access)
        public LocalizationContext LocalizationContext => localizationContext;

        /// <summary>
        /// Gets the currently open DatraEditorWindow instance, if any.
        /// Returns null if no window is open.
        /// </summary>
        public static DatraEditorWindow GetOpenedWindow()
        {
            return Resources.FindObjectsOfTypeAll<DatraEditorWindow>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the currently open DatraEditorWindow, or opens a new one if none exists.
        /// </summary>
        public static DatraEditorWindow GetOrOpenWindow()
        {
            var window = GetOpenedWindow();
            if (window == null)
            {
                ShowWindow();
                window = GetOpenedWindow();
            }
            return window;
        }

        /// <summary>
        /// Selects a data type and updates the inspector view.
        /// This is the public API for programmatic selection (used for testing).
        /// </summary>
        public void SelectDataType(Type dataType)
        {
            OnDataTypeSelected(dataType);
        }
        
        // Window State
        private string currentProjectName;
        private bool isInitialized = false;
        private DatraBootstrapper.InitializerInfo assignedInitializer;

        [MenuItem("Window/Datra/Data Editor %#d")]
        public static void ShowWindow()
        {
            var initializers = DatraBootstrapper.FindInitializers(forceRefresh: true);

            if (initializers.Count == 0)
            {
                EditorUtility.DisplayDialog("Datra Editor", "No DataContext initializers found.\nAdd a method with [DatraEditorInit] attribute.", "OK");
                return;
            }

            if (initializers.Count == 1)
            {
                // Only one initializer - open directly
                ShowWindowForInitializer(initializers[0]);
            }
            else
            {
                // Multiple initializers - show selection menu
                var menu = new GenericMenu();
                foreach (var initializer in initializers)
                {
                    var init = initializer; // Capture for closure
                    menu.AddItem(new GUIContent(init.DisplayName), false, () => ShowWindowForInitializer(init));
                }
                menu.ShowAsContext();
            }
        }

        /// <summary>
        /// Open a new editor window for a specific initializer
        /// </summary>
        public static DatraEditorWindow ShowWindowForInitializer(DatraBootstrapper.InitializerInfo initializer)
        {
            // Create new instance (allows multiple windows)
            var window = CreateInstance<DatraEditorWindow>();
            window.assignedInitializer = initializer;
            window.titleContent = new GUIContent($"Datra - {initializer.DisplayName}", EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(1200, 600);
            window.Show();
            return window;
        }
        
        private void CreateGUI()
        {
            // Load stylesheets
            LoadStyleSheets();
            
            // Create main container
            var root = rootVisualElement;
            root.AddToClassList("datra-editor-window");
            
            var mainContainer = new VisualElement();
            mainContainer.AddToClassList("main-container");
            mainContainer.style.flexDirection = FlexDirection.Column;
            root.Add(mainContainer);
            
            // Create toolbar
            toolbar = new DatraToolbarPanel();
            toolbar.OnSaveClicked += SaveCurrentData;
            toolbar.OnSaveAllClicked += SaveAllData;
            toolbar.OnForceSaveClicked += ForceSaveCurrentData;
            toolbar.OnForceSaveAllClicked += ForceSaveAllData;
            toolbar.OnReloadClicked += ReloadData;
            toolbar.OnSettingsClicked += ShowSettings;
            toolbar.OnLanguageChanged += OnToolbarLanguageChanged;
            mainContainer.Add(toolbar);
            
            // Create tab container
            tabContainer = new VisualElement();
            tabContainer.AddToClassList("tab-container");
            tabContainer.style.display = DisplayStyle.None; // Hidden by default
            tabContainer.style.flexDirection = FlexDirection.Row;
            tabContainer.style.height = 32;
            tabContainer.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            mainContainer.Add(tabContainer);
            
            // Create content area
            contentArea = new VisualElement();
            contentArea.AddToClassList("content-area");
            contentArea.style.flexGrow = 1;
            mainContainer.Add(contentArea);
            
            // Create main content container with split view
            var contentContainer = new VisualElement();
            contentContainer.AddToClassList("content-container");
            contentContainer.style.flexGrow = 1;
            contentArea.Add(contentContainer);
            
            // Create split view with proper initial position
            splitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            splitView.fixedPaneIndex = 0; // Left pane is fixed
            splitView.fixedPaneInitialDimension = 300; // Initial width of left panel
            contentContainer.Add(splitView);
            
            // Create navigation panel (left)
            navigationPanel = new DatraNavigationPanel();
            navigationPanel.style.minWidth = 200;
            navigationPanel.style.maxWidth = 500;
            splitView.Add(navigationPanel);
            
            // Create inspector container (right)
            inspectorContainer = new VisualElement();
            inspectorContainer.style.minWidth = 400;
            inspectorContainer.style.flexGrow = 1;
            splitView.Add(inspectorContainer);
            
            // Create both inspector panels
            dataInspectorPanel = new DataInspectorPanel();
            dataInspectorPanel.OnDataModified += OnDataModified;
            dataInspectorPanel.OnSaveRequested += OnInspectorSaveRequested;
            
            localizationInspectorPanel = new LocalizationInspectorPanel();
            localizationInspectorPanel.OnDataModified += OnDataModified;
            localizationInspectorPanel.OnSaveRequested += OnInspectorSaveRequested;
            localizationInspectorPanel.OnSyncFixedLocaleKeysRequested += ShowFixedLocaleKeySync;
            localizationInspectorPanel.OnLanguageChanged += OnLocalizationPanelLanguageChanged;
            
            // Initialize data
            EditorApplication.delayCall += InitializeData;
        }
        
        private void LoadStyleSheets()
        {
            var stylePaths = new[]
            {
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraEditorWindow.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraNavigationPanel.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraInspectorPanel.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraPropertyField.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraDataView.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraTableView.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraFormView.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraLocalizationView.uss"
            };
            
            foreach (var path in stylePaths)
            {
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet != null)
                {
                    rootVisualElement.styleSheets.Add(styleSheet);
                }
                else
                {
                    Debug.LogWarning($"Could not load stylesheet: {path}");
                }
            }
        }
        
        private void InitializeData()
        {
            if (isInitialized) return;

            try
            {
                // Use assigned initializer if available, otherwise auto-initialize
                if (assignedInitializer != null)
                {
                    dataContext = DatraBootstrapper.ExecuteInitializer(assignedInitializer);
                }
                else
                {
                    dataContext = DatraBootstrapper.AutoInitialize();
                }

                if (dataContext != null)
                {
                    // First load data types to populate repositories and change trackers
                    LoadDataTypes();

                    // Initialize data manager with all required dependencies
                    dataManager = new DatraDataManager(
                        dataContext,
                        repositories,
                        dataSources,
                        localizationDataSource);

                    // Subscribe to manager events
                    dataManager.OnModifiedStateChanged += OnManagerModifiedStateChanged;
                    dataManager.OnOperationCompleted += (message) => {
                        EditorUtility.DisplayDialog("Success", message, "OK");
                    };
                    dataManager.OnOperationFailed += (message) => {
                        EditorUtility.DisplayDialog("Error", message, "OK");
                    };
                    dataManager.OnDataChanged += OnManagerDataChanged;
                    dataManager.OnLocalizationChanged += OnManagerLocalizationChanged;

                    // Create service adapters and ViewModel (new architecture)
                    InitializeViewModel();

                    isInitialized = true;

                    // Set project name
                    currentProjectName = Application.productName;
                    toolbar.SetProjectName(currentProjectName);
                }
                else
                {
                    ShowInitializationError("No DataContext found. Please ensure Datra is properly initialized.");
                }
            }
            catch (Exception e)
            {
                ShowInitializationError($"Failed to initialize: {e.Message}");
                Debug.LogError($"Datra Editor initialization failed: {e}");
            }
        }

        private void OnManagerModifiedStateChanged(Type dataType, bool hasChanges)
        {
            // Update navigation panel indicator
            navigationPanel.MarkTypeAsModified(dataType, hasChanges);

            // Update toolbar modified state using ViewModel (check if ANY type is modified)
            var anyModified = viewModel?.HasAnyUnsavedChanges ?? false;
            toolbar.SetModifiedState(anyModified);

            // Update current data modified state if it's the active panel
            UpdateCurrentDataModifiedState();
        }

        private void OnManagerDataChanged(Type dataType)
        {
            // Refresh the currently displayed view if it matches the changed data type
            if (currentInspectorPanel == dataInspectorPanel && dataInspectorPanel.CurrentType == dataType)
            {
                // Optionally refresh the view - but be careful not to lose focus/selection
                // For now, just let the view handle its own updates
            }

            // Handle LocalizationContext changes
            if (dataType == typeof(LocalizationContext))
            {
                // Don't refresh if localization panel is currently active and the user is editing
                // The view handles its own updates during editing, and RefreshContent would
                // destroy the TextField being edited (causing input loss)
                if (currentInspectorPanel == localizationInspectorPanel)
                {
                    // Skip refresh - the view is already updated by the editing code
                    return;
                }

                // Refresh when the change came from elsewhere (e.g., LocaleEditPopup in DataInspectorPanel)
                // so that LocalizationInspectorPanel shows updated values when user switches to it
                localizationInspectorPanel.RefreshContent();
            }
        }

        private void OnManagerLocalizationChanged(string key, LanguageCode language)
        {
            // The LocalizationView subscribes to LocalizationContext events directly
            // This event is for other components that might need to react to localization changes
        }

        /// <summary>
        /// Initialize the ViewModel and service adapters.
        /// This enables the new MVVM architecture while maintaining backward compatibility.
        /// </summary>
        private void InitializeViewModel()
        {
            if (dataManager == null) return;

            // Create service adapters that wrap the existing DatraDataManager
            dataServiceAdapter = new DatraDataManagerAdapter(dataManager);

            // Create localization service adapter if available
            if (localizationDataSource != null)
            {
                localizationServiceAdapter = new LocalizationEditorServiceAdapter(
                    localizationDataSource,
                    dataManager);
            }

            // Create the ViewModel with service adapters
            viewModel = new DatraEditorViewModel(
                dataServiceAdapter,
                localizationServiceAdapter);

            // Set project name on ViewModel
            viewModel.ProjectName = Application.productName;

            // Subscribe to tab events
            viewModel.OnTabOpened += OnViewModelTabOpened;
            viewModel.OnTabClosed += OnViewModelTabClosed;
            viewModel.OnActiveTabChanged += OnViewModelActiveTabChanged;
        }

        private void CreateEditableDataSourceForRepository(Type dataType, object repository)
        {
            try
            {
                var repoType = repository.GetType();

                // Check for ISingleRepository<TData>
                var singleRepoInterface = repoType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISingleRepository<>));

                if (singleRepoInterface != null)
                {
                    var valueType = singleRepoInterface.GetGenericArguments()[0];

                    // Create EditableSingleDataSource<TData>
                    var dataSourceType = typeof(EditableSingleDataSource<>).MakeGenericType(valueType);
                    var dataSource = Activator.CreateInstance(dataSourceType, repository) as IEditableDataSource;

                    if (dataSource != null)
                    {
                        dataSources[dataType] = dataSource;
                    }
                    return;
                }

                // Check for ITableRepository<TKey, TValue>
                var keyValueRepoInterface = repoType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITableRepository<,>));

                if (keyValueRepoInterface != null)
                {
                    var genericArgs = keyValueRepoInterface.GetGenericArguments();
                    var keyType = genericArgs[0];
                    var valueType = genericArgs[1];

                    // Create EditableKeyValueDataSource<TKey, TData>
                    var dataSourceType = typeof(EditableKeyValueDataSource<,>).MakeGenericType(keyType, valueType);
                    var dataSource = Activator.CreateInstance(dataSourceType, repository) as IEditableDataSource;

                    if (dataSource != null)
                    {
                        dataSources[dataType] = dataSource;
                    }
                    return;
                }

                // Check for IAssetRepository<T>
                var assetRepoInterface = repoType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAssetRepository<>));

                if (assetRepoInterface != null)
                {
                    var assetDataType = assetRepoInterface.GetGenericArguments()[0];

                    // Create EditableAssetDataSource<T>
                    var dataSourceType = typeof(EditableAssetDataSource<>).MakeGenericType(assetDataType);
                    var dataSource = Activator.CreateInstance(dataSourceType, repository) as IEditableDataSource;

                    if (dataSource != null)
                    {
                        dataSources[dataType] = dataSource;
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to create editable data source for {dataType.Name}: {e.Message}");
            }
        }

        private void LoadDataTypes()
        {
            repositories.Clear();
            dataTypeInfoMap.Clear();
            dataSources.Clear();
            
            // Get data type infos from context
            var dataTypeInfos = dataContext.GetDataTypeInfos();
            
            // Get properties to access repositories
            var properties = dataContext.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            // Check for LocalizationContext property
            var localizationProperty = properties.FirstOrDefault(p => p.PropertyType == typeof(LocalizationContext));
            if (localizationProperty != null)
            {
                localizationContext = localizationProperty.GetValue(dataContext) as LocalizationContext;

                // Create EditableLocalizationDataSource and LocalizationRepository
                if (localizationContext != null)
                {
                    localizationDataSource = new EditableLocalizationDataSource(localizationContext);

                    // Create LocalizationRepository wrapper and register it
                    var localizationRepository = new LocalizationRepository(localizationContext);
                    repositories[typeof(LocalizationContext)] = localizationRepository;

                    // Load all available languages for editor (allows editing multiple languages without switching)
                    localizationContext.LoadAllAvailableLanguagesAsync().Wait();

                    // Initialize baseline for all loaded languages
                    // This is important because LocaleEditPopup can edit multiple languages at once
                    var loadedLanguages = localizationContext.GetLoadedLanguages();
                    foreach (var languageCode in loadedLanguages)
                    {
                        if (!localizationDataSource.IsLanguageInitialized(languageCode))
                        {
                            localizationDataSource.InitializeBaseline(languageCode);
                        }
                    }

                    // Initialize toolbar language dropdown
                    var availableLanguages = localizationContext.GetAvailableLanguages();
                    toolbar.SetupLanguages(availableLanguages, localizationContext.CurrentLanguageCode);

                    // Pass localizationDataSource to DataInspectorPanel for FixedLocale property support
                    dataInspectorPanel.SetLocalizationContext(localizationContext, localizationDataSource);
                }
            }
            
            foreach (var dataTypeInfo in dataTypeInfos)
            {
                // Find matching property by name
                var property = properties.FirstOrDefault(p => p.Name == dataTypeInfo.PropertyName);
                if (property != null)
                {
                    var repository = property.GetValue(dataContext) as IEditableRepository;
                    if (repository != null)
                    {
                        repositories[dataTypeInfo.DataType] = repository;
                        dataTypeInfoMap[dataTypeInfo.DataType] = dataTypeInfo;

                        // Create EditableDataSource for this data type
                        CreateEditableDataSourceForRepository(dataTypeInfo.DataType, repository);
                    }
                }
            }

            // Initialize all data sources (loads assets for AssetRepository-based sources)
            foreach (var dataSource in dataSources.Values)
            {
                dataSource.InitializeAsync().Wait();
            }

            // Update navigation panel with data type infos and localization context
            navigationPanel.SetDataTypeInfos(dataTypeInfos, OnDataTypeSelected, localizationContext);
            navigationPanel.SetLocalizationCallback(OnLocalizationSelected);
        }
        
        private void OnLocalizationSelected()
        {
            if (localizationContext != null)
            {
                // Update ViewModel state
                viewModel?.SelectLocalization();

                ShowLocalizationInspector();

                // Get LocalizationRepository from repositories
                IEditableRepository localizationRepository = null;
                repositories.TryGetValue(typeof(LocalizationContext), out localizationRepository);

                // Set context with unified data source pattern
                localizationInspectorPanel.SetLocalizationContext(
                    localizationContext,
                    localizationRepository,
                    dataContext,
                    localizationDataSource);

                UpdateCurrentDataModifiedState();
            }
        }
        
        private void ShowDataInspector()
        {
            if (currentInspectorPanel != dataInspectorPanel)
            {
                inspectorContainer.Clear();
                inspectorContainer.Add(dataInspectorPanel);
                currentInspectorPanel = dataInspectorPanel;
            }
        }
        
        private void ShowLocalizationInspector()
        {
            if (currentInspectorPanel != localizationInspectorPanel)
            {
                inspectorContainer.Clear();
                inspectorContainer.Add(localizationInspectorPanel);
                currentInspectorPanel = localizationInspectorPanel;
            }
        }
        
        private void OnDataTypeSelected(Type dataType)
        {
            if (repositories.TryGetValue(dataType, out var repository))
            {
                // Update ViewModel state
                viewModel?.SelectDataType(dataType);

                ShowDataInspector();

                // Get data source for this data type
                dataSources.TryGetValue(dataType, out var source);

                // Pass data source to inspector panel
                dataInspectorPanel.SetDataContext(dataContext, repository, dataType, source);
                UpdateCurrentDataModifiedState();
            }
        }
        
        public void AddDataTab(Type dataType, IEditableRepository repository, IDataContext context)
        {
            // Delegate to ViewModel - it will fire events that we handle
            viewModel?.OpenTab(dataType);
        }

        private void OnViewModelTabOpened(TabViewModel tab)
        {
            // Create UI for the new tab
            var tabButton = new Button(() => viewModel.ActivateTab(tab));
            tabButton.AddToClassList("tab-button");
            tabButton.style.flexDirection = FlexDirection.Row;

            var tabLabel = new Label(tab.DisplayName);
            tabLabel.style.marginRight = 8;
            tabButton.Add(tabLabel);

            // Close button
            var closeButton = new Button(() => RequestCloseTab(tab));
            closeButton.text = "×";
            closeButton.AddToClassList("tab-close-button");
            tabButton.Add(closeButton);

            tabContainer.Add(tabButton);

            // Store UI mapping
            tabUIMap[tab] = new DataTabUI { ViewModel = tab, TabButton = tabButton };

            // Show tab container if this is the first tab
            if (tabUIMap.Count == 1)
            {
                tabContainer.style.display = DisplayStyle.Flex;
            }
        }

        private void OnViewModelActiveTabChanged(TabViewModel tab)
        {
            // Update UI for all tabs
            foreach (var kvp in tabUIMap)
            {
                if (kvp.Key == tab)
                {
                    kvp.Value.TabButton.AddToClassList("active");
                }
                else
                {
                    kvp.Value.TabButton.RemoveFromClassList("active");
                }
            }

            // Update inspector with tab data
            if (tab != null)
            {
                ShowDataInspector();
                dataInspectorPanel.SetDataContext(tab.DataContext, tab.Repository, tab.DataType, dataSources.GetValueOrDefault(tab.DataType));
            }
        }

        private void RequestCloseTab(TabViewModel tab)
        {
            // Check for unsaved changes before closing
            if (viewModel?.HasUnsavedChanges(tab.DataType) == true)
            {
                if (!EditorUtility.DisplayDialog("Unsaved Changes",
                    $"The {tab.DisplayName} tab has unsaved changes. Close anyway?",
                    "Close", "Cancel"))
                {
                    return;
                }
            }

            viewModel?.CloseTab(tab);
        }

        private void OnViewModelTabClosed(TabViewModel tab)
        {
            // Remove UI for the closed tab
            if (tabUIMap.TryGetValue(tab, out var tabUI))
            {
                tabContainer.Remove(tabUI.TabButton);
                tabUIMap.Remove(tab);
            }

            // Hide tab container if no tabs
            if (tabUIMap.Count == 0)
            {
                tabContainer.style.display = DisplayStyle.None;
            }
        }
        
        private void OnDataModified(Type dataType, bool isModified)
        {
            // The manager now handles modified state tracking automatically via change tracker events
            // Just update the navigation panel
            navigationPanel.MarkTypeAsModified(dataType, isModified);
        }

        private async void SaveAllData()
        {
            if (viewModel == null) return;

            // Check if there are any modifications
            if (!viewModel.HasAnyUnsavedChanges)
            {
                // No modifications - suggest Force Save All
                if (EditorUtility.DisplayDialog("No Changes",
                    "No data has unsaved changes.\n\nWould you like to Force Save All anyway?",
                    "Force Save All", "Cancel"))
                {
                    ForceSaveAllData();
                }
                return;
            }

            try
            {
                toolbar.SetSaveButtonEnabled(false);
                var success = await viewModel.SaveAllAsync();

                // Refresh current view's modified state if it was saved
                if (success)
                {
                    RefreshCurrentInspectorPanel();
                }
            }
            finally
            {
                toolbar.SetSaveButtonEnabled(true);
            }
        }
        
        private async void OnInspectorSaveRequested(Type dataType, IEditableRepository repository)
        {
            if (viewModel?.DataService == null) return;

            // Perform actual save through DatraDataManager
            var success = await viewModel.DataService.SaveAsync(dataType, forceSave: false);

            // Notify view that save completed
            currentInspectorPanel?.NotifySaveCompleted(success);

            if (success)
            {
                RefreshCurrentInspectorPanel();
            }
        }

        private async void SaveCurrentData()
        {
            if (viewModel == null) return;

            // Check if there are modifications in current data
            if (!viewModel.HasCurrentDataUnsavedChanges)
            {
                // No modifications - suggest Force Save
                var typeName = viewModel.IsLocalizationSelected ? "Localization" : viewModel.SelectedDataType?.Name ?? "Data";
                if (EditorUtility.DisplayDialog("No Changes",
                    $"{typeName} has no unsaved changes.\n\nWould you like to Force Save anyway?",
                    "Force Save", "Cancel"))
                {
                    await viewModel.ForceSaveCurrentAsync();
                    RefreshCurrentInspectorPanel();
                }
                return;
            }

            var success = await viewModel.SaveCurrentAsync();
            if (success)
            {
                RefreshCurrentInspectorPanel();
            }
        }

        private async Task<bool> SaveSpecificData(Type dataType)
        {
            if (viewModel?.DataService == null) return false;

            var success = await viewModel.DataService.SaveAsync(dataType, forceSave: false);
            if (success)
            {
                RefreshCurrentInspectorPanel();
            }
            return success;
        }

        private async void ForceSaveCurrentData()
        {
            if (viewModel == null) return;

            var success = await viewModel.ForceSaveCurrentAsync();
            if (success)
            {
                RefreshCurrentInspectorPanel();
            }
        }

        private async void ForceSaveAllData()
        {
            if (viewModel == null) return;

            try
            {
                toolbar.SetSaveButtonEnabled(false);
                var success = await viewModel.ForceSaveAllAsync();

                if (success)
                {
                    RefreshCurrentInspectorPanel();
                }
            }
            finally
            {
                toolbar.SetSaveButtonEnabled(true);
            }
        }

        private void RefreshCurrentInspectorPanel()
        {
            if (currentInspectorPanel == dataInspectorPanel)
            {
                dataInspectorPanel.RefreshModifiedState();
            }
            else if (currentInspectorPanel == localizationInspectorPanel)
            {
                localizationInspectorPanel.RefreshContent();
            }
        }

        private void UpdateCurrentDataModifiedState()
        {
            // Update the Save button state based on current data using ViewModel
            var isModified = viewModel?.HasCurrentDataUnsavedChanges ?? false;
            toolbar.SetCurrentDataModified(isModified);
        }

        private async void ReloadData()
        {
            if (viewModel == null) return;

            // Check for unsaved changes and prompt user
            if (viewModel.HasAnyUnsavedChanges)
            {
                if (!EditorUtility.DisplayDialog("Unsaved Changes",
                    "There are unsaved changes. Reload anyway?",
                    "Reload", "Cancel"))
                {
                    return;
                }
            }

            try
            {
                toolbar.SetReloadButtonEnabled(false);

                if (await viewModel.ReloadAsync())
                {
                    // Refresh current view if data inspector is active
                    if (currentInspectorPanel == dataInspectorPanel && dataInspectorPanel.CurrentType != null)
                    {
                        dataInspectorPanel.SetDataContext(dataContext, dataInspectorPanel.CurrentRepository, dataInspectorPanel.CurrentType, dataSources.GetValueOrDefault(dataInspectorPanel.CurrentType));
                    }
                    else if (currentInspectorPanel == localizationInspectorPanel)
                    {
                        localizationInspectorPanel.RefreshContent();
                    }
                }
            }
            finally
            {
                toolbar.SetReloadButtonEnabled(true);
            }
        }
        
        private void ShowSettings()
        {
            // TODO: Implement settings window
            EditorUtility.DisplayDialog("Settings", "Settings window coming soon!", "OK");
        }

        /// <summary>
        /// Analyzes and syncs FixedLocale keys with data.
        /// Shows a dialog with missing/orphan keys and allows user to sync them.
        /// </summary>
        public void ShowFixedLocaleKeySync()
        {
            if (dataContext == null || localizationContext == null)
            {
                EditorUtility.DisplayDialog("Error", "DataContext or LocalizationContext not initialized.", "OK");
                return;
            }

            try
            {
                // Create analyzer and run analysis
                var analyzer = new FixedLocaleKeyAnalyzer(dataContext, localizationContext, repositories);
                var result = analyzer.Analyze();

                // Show sync window with results
                FixedLocaleKeySyncWindow.Show(result, localizationContext, () =>
                {
                    // After sync, refresh localization data source
                    localizationDataSource?.RefreshBaseline();

                    // Refresh navigation panel and current view
                    navigationPanel?.MarkTypeAsModified(typeof(LocalizationContext), false);
                    RefreshCurrentInspectorPanel();
                });
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to analyze FixedLocale keys:\n{e.Message}", "OK");
                Debug.LogError($"FixedLocale sync failed: {e}");
            }
        }

        private void OnToolbarLanguageChanged(LanguageCode newLanguage)
        {
            if (localizationContext == null) return;

            // Update LocalizationContext current language
            localizationContext.LoadLanguageAsync(newLanguage).Wait();

            // Update LocalizationView (handles language switch internally)
            localizationInspectorPanel.SwitchLanguage(newLanguage);

            // Refresh DataInspectorPanel to update LocaleRef fields in tables
            if (currentInspectorPanel == dataInspectorPanel)
            {
                dataInspectorPanel.RefreshContent();
            }
        }

        private void OnLocalizationPanelLanguageChanged(LanguageCode newLanguage)
        {
            // Sync toolbar dropdown when language is changed from localization panel (badge click)
            toolbar.SetCurrentLanguage(newLanguage);

            // Also update LocalizationContext current language
            if (localizationContext != null)
            {
                localizationContext.LoadLanguageAsync(newLanguage).Wait();
            }

            // Refresh DataInspectorPanel to update LocaleRef fields in tables
            if (currentInspectorPanel == dataInspectorPanel)
            {
                dataInspectorPanel.RefreshContent();
            }
        }
        
        private void ShowInitializationError(string message)
        {
            var errorContainer = new VisualElement();
            errorContainer.AddToClassList("initialization-error");
            
            var errorLabel = new Label(message);
            errorLabel.AddToClassList("error-message");
            errorContainer.Add(errorLabel);
            
            var retryButton = new Button(() => {
                errorContainer.RemoveFromHierarchy();
                EditorApplication.delayCall += InitializeData;
            });
            retryButton.text = "Retry";
            retryButton.AddToClassList("retry-button");
            errorContainer.Add(retryButton);
            
            rootVisualElement.Add(errorContainer);
        }
        
        private void OnDestroy()
        {
            // Clean up if needed
            isInitialized = false;
            
            if (dataInspectorPanel != null)
            {
                dataInspectorPanel.Cleanup();
            }
            
            if (localizationInspectorPanel != null)
            {
                localizationInspectorPanel.Cleanup();
            }
        }
        
        private void OnFocus()
        {
            // Don't refresh content on focus - it clears all UI state including modified indicators
            // If refresh is needed, it should be done explicitly by user action
        }
    }
}