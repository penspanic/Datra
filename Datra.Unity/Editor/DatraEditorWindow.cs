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
using Datra.Unity.Editor.Utilities;
using Datra.Editor.Interfaces;
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
        private Dictionary<Type, IDataRepository> repositories = new();
        private Dictionary<Type, DataTypeInfo> dataTypeInfoMap = new();
        private DatraDataManager dataManager;
        private LocalizationContext localizationContext;
        private LocalizationChangeTracker localizationChangeTracker;

        // Change trackers for each data type
        internal Dictionary<Type, IRepositoryChangeTracker> changeTrackers = new Dictionary<Type, IRepositoryChangeTracker>();

        // ViewModel and Services (new architecture)
        private DatraEditorViewModel viewModel;
        private DatraDataManagerAdapter dataServiceAdapter;
        private LocalizationEditorServiceAdapter localizationServiceAdapter;

        // Public accessors for navigation panel
        public IDataContext DataContext => dataContext;
        public IReadOnlyDictionary<Type, IDataRepository> Repositories => repositories;
        public IReadOnlyDictionary<Type, DataTypeInfo> DataTypeInfoMap => dataTypeInfoMap;

        // Public accessor for ViewModel (enables testing and external access)
        public DatraEditorViewModel ViewModel => viewModel;
        
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
                        changeTrackers,
                        localizationContext,
                        localizationChangeTracker);

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

            // Always refresh LocalizationInspectorPanel when LocalizationContext changes
            // This is important because LocaleEditPopup can be opened from DataInspectorPanel (e.g., TableView)
            // and we need to ensure the LocalizationInspectorPanel shows updated values when the user switches to it
            if (dataType == typeof(LocalizationContext))
            {
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
            if (localizationContext != null && localizationChangeTracker != null)
            {
                localizationServiceAdapter = new LocalizationEditorServiceAdapter(
                    localizationContext,
                    localizationChangeTracker,
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

        private void CreateChangeTrackerForRepository(Type dataType, object repository)
        {
            try
            {
                // Get repository type
                var repoType = repository.GetType();

                // Check for ISingleDataRepository<TData>
                var singleRepoInterface = repoType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Datra.Interfaces.ISingleDataRepository<>));

                if (singleRepoInterface != null)
                {
                    var valueType = singleRepoInterface.GetGenericArguments()[0];
                    var keyType = typeof(string);

                    // Create RepositoryChangeTracker<string, TValue>
                    var trackerType = typeof(Datra.Unity.Editor.Utilities.RepositoryChangeTracker<,>).MakeGenericType(keyType, valueType);
                    var tracker = Activator.CreateInstance(trackerType) as IRepositoryChangeTracker;

                    if (tracker != null)
                    {
                        // Use interface to get data
                        var singleRepo = repository as dynamic;
                        var data = singleRepo.Get();

                        // Create a dictionary with single item using "single" as key
                        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                        var dict = Activator.CreateInstance(dictType) as System.Collections.IDictionary;

                        if (dict != null && data != null)
                        {
                            dict.Add("single", data);
                            tracker.InitializeBaseline(dict);
                        }

                        // Store tracker
                        changeTrackers[dataType] = tracker;
                    }
                    return;
                }

                // Check for IKeyValueDataRepository<TKey, TValue>
                var keyValueRepoInterface = repoType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Datra.Interfaces.IKeyValueDataRepository<,>));

                if (keyValueRepoInterface != null)
                {
                    var genericArgs = keyValueRepoInterface.GetGenericArguments();
                    var keyType = genericArgs[0];
                    var valueType = genericArgs[1];

                    // Create RepositoryChangeTracker<TKey, TValue>
                    var trackerType = typeof(Datra.Unity.Editor.Utilities.RepositoryChangeTracker<,>).MakeGenericType(keyType, valueType);
                    var tracker = Activator.CreateInstance(trackerType) as IRepositoryChangeTracker;

                    if (tracker != null)
                    {
                        // Use interface to get data
                        var keyValueRepo = repository as dynamic;
                        var data = keyValueRepo.GetAll();

                        // Initialize baseline
                        tracker.InitializeBaseline(data);

                        // Store tracker
                        changeTrackers[dataType] = tracker;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to create change tracker for {dataType.Name}: {e.Message}");
            }
        }

        private void LoadDataTypes()
        {
            repositories.Clear();
            dataTypeInfoMap.Clear();
            changeTrackers.Clear();
            
            // Get data type infos from context
            var dataTypeInfos = dataContext.GetDataTypeInfos();
            
            // Get properties to access repositories
            var properties = dataContext.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            // Check for LocalizationContext property
            var localizationProperty = properties.FirstOrDefault(p => p.PropertyType == typeof(LocalizationContext));
            if (localizationProperty != null)
            {
                localizationContext = localizationProperty.GetValue(dataContext) as LocalizationContext;

                // Create and register LocalizationChangeTracker and LocalizationRepository
                if (localizationContext != null)
                {
                    localizationChangeTracker = new LocalizationChangeTracker(localizationContext);

                    // Create LocalizationRepository wrapper and register it
                    var localizationRepository = new LocalizationRepository(localizationContext);
                    repositories[typeof(LocalizationContext)] = localizationRepository;

                    // Register LocalizationRepository's change tracker
                    changeTrackers[typeof(LocalizationContext)] = localizationChangeTracker;

                    // Load all available languages for editor (allows editing multiple languages without switching)
                    localizationContext.LoadAllAvailableLanguagesAsync().Wait();

                    // Initialize LocalizationChangeTracker for all loaded languages
                    // This is important because LocaleEditPopup can edit multiple languages at once
                    var loadedLanguages = localizationContext.GetLoadedLanguages();
                    foreach (var languageCode in loadedLanguages)
                    {
                        if (!localizationChangeTracker.IsLanguageInitialized(languageCode))
                        {
                            localizationChangeTracker.InitializeLanguage(languageCode);
                        }
                    }

                    // Initialize toolbar language dropdown
                    var availableLanguages = localizationContext.GetAvailableLanguages();
                    toolbar.SetupLanguages(availableLanguages, localizationContext.CurrentLanguageCode);

                    // Pass localizationContext to DataInspectorPanel for FixedLocale property support
                    dataInspectorPanel.SetLocalizationContext(localizationContext, localizationChangeTracker);
                }
            }
            
            foreach (var dataTypeInfo in dataTypeInfos)
            {
                // Find matching property by name
                var property = properties.FirstOrDefault(p => p.Name == dataTypeInfo.PropertyName);
                if (property != null)
                {
                    var repository = property.GetValue(dataContext) as IDataRepository;
                    if (repository != null)
                    {
                        repositories[dataTypeInfo.DataType] = repository;
                        dataTypeInfoMap[dataTypeInfo.DataType] = dataTypeInfo;

                        // Create RepositoryChangeTracker for this data type
                        CreateChangeTrackerForRepository(dataTypeInfo.DataType, repository);
                    }
                }
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

                // Set change tracker before setting context
                if (localizationChangeTracker != null)
                {
                    localizationInspectorPanel.SetChangeTracker(localizationChangeTracker);
                }

                // Get LocalizationRepository from repositories
                IDataRepository localizationRepository = null;
                repositories.TryGetValue(typeof(LocalizationContext), out localizationRepository);

                localizationInspectorPanel.SetLocalizationContext(localizationContext, localizationRepository, dataContext);

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

                // Get change tracker for this data type
                changeTrackers.TryGetValue(dataType, out var tracker);

                // Pass tracker to inspector panel
                dataInspectorPanel.SetDataContext(dataContext, repository, dataType, tracker);
                UpdateCurrentDataModifiedState();
            }
        }
        
        public void AddDataTab(Type dataType, IDataRepository repository, IDataContext context)
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
                dataInspectorPanel.SetDataContext(tab.DataContext, tab.Repository, tab.DataType, changeTrackers.GetValueOrDefault(tab.DataType));
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
        
        private async void OnInspectorSaveRequested(Type dataType, IDataRepository repository)
        {
            await SaveSpecificData(dataType);
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
                        dataInspectorPanel.SetDataContext(dataContext, dataInspectorPanel.CurrentRepository, dataInspectorPanel.CurrentType, changeTrackers.GetValueOrDefault(dataInspectorPanel.CurrentType));
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