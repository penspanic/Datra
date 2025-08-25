using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Datra.Interfaces;
using Datra.Services;
using Datra.Unity.Editor.Panels;
using Datra.Unity.Editor.Utilities;
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
        
        // Tab Management
        private class DataTab
        {
            public Type DataType { get; set; }
            public object Repository { get; set; }
            public object DataContext { get; set; }
            public VisualElement TabButton { get; set; }
            public VisualElement Content { get; set; }
            public bool IsModified { get; set; }
        }
        
        private List<DataTab> openTabs = new List<DataTab>();
        private DataTab activeTab;
        
        // Data Management
        private IDataContext dataContext;
        private Dictionary<Type, object> repositories = new Dictionary<Type, object>();
        private Dictionary<Type, DataTypeInfo> dataTypeInfoMap = new Dictionary<Type, DataTypeInfo>();
        private DatraDataManager dataManager;
        private LocalizationContext localizationContext;
        
        // Public accessors for navigation panel
        public IDataContext DataContext => dataContext;
        public IReadOnlyDictionary<Type, object> Repositories => repositories;
        public IReadOnlyDictionary<Type, DataTypeInfo> DataTypeInfoMap => dataTypeInfoMap;
        
        // Window State
        private string currentProjectName;
        private bool isInitialized = false;
        
        [MenuItem("Window/Datra/Data Editor %#d")]
        public static void ShowWindow()
        {
            var window = GetWindow<DatraEditorWindow>();
            window.titleContent = new GUIContent("Datra Editor", EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(1200, 600);
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
            toolbar.OnSaveAllClicked += SaveAllData;
            toolbar.OnReloadClicked += ReloadData;
            toolbar.OnSettingsClicked += ShowSettings;
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
            dataInspectorPanel.OnSaveRequested += SaveCurrentData;
            
            localizationInspectorPanel = new LocalizationInspectorPanel();
            localizationInspectorPanel.OnDataModified += OnDataModified;
            localizationInspectorPanel.OnSaveRequested += SaveCurrentData;
            
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
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraTableView.uss"
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
                // Execute bootstrapper to initialize data context
                dataContext = DatraBootstrapper.AutoInitialize();
                if (dataContext != null)
                {
                    // Initialize data manager
                    dataManager = new DatraDataManager(dataContext);
                    dataManager.OnDataModified += OnDataModified;
                    dataManager.OnModifiedStateChanged += (hasModified) => {
                        toolbar.SetModifiedState(hasModified);
                    };
                    dataManager.OnOperationCompleted += (message) => {
                        EditorUtility.DisplayDialog("Success", message, "OK");
                    };
                    dataManager.OnOperationFailed += (message) => {
                        EditorUtility.DisplayDialog("Error", message, "OK");
                    };
                    
                    LoadDataTypes();
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
        
        private void LoadDataTypes()
        {
            repositories.Clear();
            dataTypeInfoMap.Clear();
            
            // Get data type infos from context
            var dataTypeInfos = dataContext.GetDataTypeInfos();
            
            // Get properties to access repositories
            var properties = dataContext.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            // Check for LocalizationContext property
            var localizationProperty = properties.FirstOrDefault(p => p.PropertyType == typeof(LocalizationContext));
            if (localizationProperty != null)
            {
                localizationContext = localizationProperty.GetValue(dataContext) as LocalizationContext;
            }
            
            foreach (var dataTypeInfo in dataTypeInfos)
            {
                // Find matching property by name
                var property = properties.FirstOrDefault(p => p.Name == dataTypeInfo.PropertyName);
                if (property != null)
                {
                    var repository = property.GetValue(dataContext);
                    if (repository != null)
                    {
                        repositories[dataTypeInfo.DataType] = repository;
                        dataTypeInfoMap[dataTypeInfo.DataType] = dataTypeInfo;
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
                ShowLocalizationInspector();
                localizationInspectorPanel.SetLocalizationContext(localizationContext);
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
                ShowDataInspector();
                dataInspectorPanel.SetDataContext(dataContext, repository, dataType);
            }
        }
        
        public void AddDataTab(Type dataType, object repository, object context)
        {
            // Check if tab already exists
            var existingTab = openTabs.FirstOrDefault(t => t.DataType == dataType);
            if (existingTab != null)
            {
                ActivateTab(existingTab);
                return;
            }
            
            // Create new tab
            var tab = new DataTab
            {
                DataType = dataType,
                Repository = repository,
                DataContext = context
            };
            
            // Create tab button
            var tabButton = new Button(() => ActivateTab(tab));
            tabButton.AddToClassList("tab-button");
            tabButton.style.flexDirection = FlexDirection.Row;
            
            var tabLabel = new Label(dataType.Name);
            tabLabel.style.marginRight = 8;
            tabButton.Add(tabLabel);
            
            // Close button
            var closeButton = new Button(() => CloseTab(tab));
            closeButton.text = "×";
            closeButton.AddToClassList("tab-close-button");
            tabButton.Add(closeButton);
            
            tab.TabButton = tabButton;
            tabContainer.Add(tabButton);
            
            // Show tab container if this is the first tab
            if (openTabs.Count == 0)
            {
                tabContainer.style.display = DisplayStyle.Flex;
            }
            
            openTabs.Add(tab);
            ActivateTab(tab);
        }
        
        private void ActivateTab(DataTab tab)
        {
            // Deactivate current tab
            if (activeTab != null)
            {
                activeTab.TabButton.RemoveFromClassList("active");
            }
            
            activeTab = tab;
            tab.TabButton.AddToClassList("active");
            
            // Update inspector with tab data
            ShowDataInspector();
            dataInspectorPanel.SetDataContext(tab.DataContext, tab.Repository, tab.DataType);
        }
        
        private void CloseTab(DataTab tab)
        {
            if (dataManager != null && dataManager.ModifiedTypes.Contains(tab.DataType))
            {
                if (!EditorUtility.DisplayDialog("Unsaved Changes", 
                    $"The {tab.DataType.Name} tab has unsaved changes. Close anyway?", 
                    "Close", "Cancel"))
                {
                    return;
                }
            }
            
            openTabs.Remove(tab);
            tabContainer.Remove(tab.TabButton);
            
            // Hide tab container if no tabs
            if (openTabs.Count == 0)
            {
                tabContainer.style.display = DisplayStyle.None;
                activeTab = null;
            }
            else if (activeTab == tab)
            {
                // Activate another tab
                ActivateTab(openTabs[0]);
            }
        }
        
        private void OnDataModified(Type dataType)
        {
            dataManager.MarkAsModified(dataType);
            navigationPanel.MarkTypeAsModified(dataType, true);
        }
        
        private async void SaveAllData()
        {
            if (dataManager == null) return;
            
            try
            {
                toolbar.SetSaveButtonEnabled(false);
                var success = await dataManager.SaveAllAsync(repositories);
                
                // Update navigation panel modified states
                if (success)
                {
                    foreach (var type in repositories.Keys)
                    {
                        if (!dataManager.ModifiedTypes.Contains(type))
                        {
                            navigationPanel.MarkTypeAsModified(type, false);
                        }
                    }
                }
            }
            finally
            {
                toolbar.SetSaveButtonEnabled(true);
            }
        }
        
        private async void SaveCurrentData(Type dataType, object repository)
        {
            if (dataManager == null || repository == null) return;
            
            var success = await dataManager.SaveAsync(dataType, repository);
            if (success)
            {
                navigationPanel.MarkTypeAsModified(dataType, false);
            }
        }
        
        private async void ReloadData()
        {
            if (dataManager == null) return;
            
            // Handle unsaved changes
            if (dataManager.HasModifiedData)
            {
                var result = await dataManager.CheckUnsavedChangesAsync("reloading");
                if (!result) // User wants to save first
                {
                    await dataManager.SaveAllAsync(repositories);
                }
            }
            
            try
            {
                toolbar.SetReloadButtonEnabled(false);
                
                if (await dataManager.ReloadAllAsync(false))
                {
                    // Clear all modified states in navigation panel
                    foreach (var type in repositories.Keys)
                    {
                        navigationPanel.MarkTypeAsModified(type, false);
                    }
                    
                    // Refresh current view if data inspector is active
                    if (currentInspectorPanel == dataInspectorPanel)
                    {
                        dataInspectorPanel.SetDataContext(dataContext, dataInspectorPanel.CurrentRepository, dataInspectorPanel.CurrentType);
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