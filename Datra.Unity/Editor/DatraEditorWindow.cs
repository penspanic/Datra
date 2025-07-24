using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Datra.Interfaces;
using Datra.Unity.Editor.Panels;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor
{
    public class DatraEditorWindow : EditorWindow
    {
        // UI Components
        private DatraToolbarPanel toolbar;
        private DatraNavigationPanel navigationPanel;
        private DatraInspectorPanel inspectorPanel;
        private TwoPaneSplitView splitView;
        
        // Data Management
        private IDataContext dataContext;
        private Dictionary<Type, object> repositories = new Dictionary<Type, object>();
        private HashSet<Type> modifiedTypes = new HashSet<Type>();
        
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
            root.Add(mainContainer);
            
            // Create toolbar
            toolbar = new DatraToolbarPanel();
            toolbar.OnSaveAllClicked += SaveAllData;
            toolbar.OnReloadClicked += ReloadData;
            toolbar.OnSettingsClicked += ShowSettings;
            mainContainer.Add(toolbar);
            
            // Create content container with split view
            var contentContainer = new VisualElement();
            contentContainer.AddToClassList("content-container");
            mainContainer.Add(contentContainer);
            
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
            
            // Create inspector panel (right)
            inspectorPanel = new DatraInspectorPanel();
            inspectorPanel.style.minWidth = 400;
            inspectorPanel.OnDataModified += OnDataModified;
            inspectorPanel.OnSaveRequested += SaveCurrentData;
            splitView.Add(inspectorPanel);
            
            // Initialize data
            EditorApplication.delayCall += InitializeData;
        }
        
        private void LoadStyleSheets()
        {
            var stylePaths = new[]
            {
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraEditorWindow.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraNavigationPanel.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraInspectorPanel.uss"
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
            
            var dataTypes = new List<Type>();
            var properties = dataContext.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                var propertyType = property.PropertyType;
                if (propertyType.IsGenericType)
                {
                    var genericDef = propertyType.GetGenericTypeDefinition();
                    if (genericDef == typeof(IDataRepository<,>))
                    {
                        var repository = property.GetValue(dataContext);
                        if (repository != null)
                        {
                            var dataType = propertyType.GetGenericArguments()[1];
                            repositories[dataType] = repository;
                            dataTypes.Add(dataType);
                        }
                    }
                    else if (genericDef == typeof(ISingleDataRepository<>))
                    {
                        var repository = property.GetValue(dataContext);
                        if (repository != null)
                        {
                            var dataType = propertyType.GetGenericArguments()[0];
                            repositories[dataType] = repository;
                            dataTypes.Add(dataType);
                        }
                    }
                }
            }
            
            // Update navigation panel with data types
            navigationPanel.SetDataTypes(dataTypes, OnDataTypeSelected);
        }
        
        private void OnDataTypeSelected(Type dataType)
        {
            if (repositories.TryGetValue(dataType, out var repository))
            {
                inspectorPanel.SetDataContext(dataContext, repository, dataType);
            }
        }
        
        private void OnDataModified(Type dataType)
        {
            modifiedTypes.Add(dataType);
            navigationPanel.MarkTypeAsModified(dataType, true);
            toolbar.SetModifiedState(modifiedTypes.Count > 0);
        }
        
        private async void SaveAllData()
        {
            if (dataContext == null) return;
            
            try
            {
                toolbar.SetSaveButtonEnabled(false);
                await dataContext.SaveAllAsync();
                
                // Clear modified states
                foreach (var type in modifiedTypes)
                {
                    navigationPanel.MarkTypeAsModified(type, false);
                }
                modifiedTypes.Clear();
                toolbar.SetModifiedState(false);
                
                EditorUtility.DisplayDialog("Success", "All data saved successfully!", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to save data: {e.Message}", "OK");
                Debug.LogError($"Failed to save data: {e}");
            }
            finally
            {
                toolbar.SetSaveButtonEnabled(true);
            }
        }
        
        private async void SaveCurrentData()
        {
            if (dataContext == null) return;
            
            try
            {
                await dataContext.SaveAllAsync();
                EditorUtility.DisplayDialog("Success", "Data saved successfully!", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to save data: {e.Message}", "OK");
                Debug.LogError($"Failed to save data: {e}");
            }
        }
        
        private async void ReloadData()
        {
            if (dataContext == null) return;
            
            if (modifiedTypes.Count > 0)
            {
                var result = EditorUtility.DisplayDialogComplex(
                    "Unsaved Changes",
                    "You have unsaved changes. What would you like to do?",
                    "Save and Reload",
                    "Cancel",
                    "Discard and Reload"
                );
                
                if (result == 1) return; // Cancel
                if (result == 0) await dataContext.SaveAllAsync(); // Save first
            }
            
            try
            {
                toolbar.SetReloadButtonEnabled(false);
                await dataContext.LoadAllAsync();
                
                // Clear modified states
                modifiedTypes.Clear();
                toolbar.SetModifiedState(false);
                
                // Refresh current view
                inspectorPanel.SetDataContext(dataContext, inspectorPanel.CurrentRepository, inspectorPanel.CurrentType);
                
                EditorUtility.DisplayDialog("Success", "Data reloaded successfully!", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to reload data: {e.Message}", "OK");
                Debug.LogError($"Failed to reload data: {e}");
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
        }
        
        private void OnFocus()
        {
            // Refresh data when window gains focus
            if (isInitialized && inspectorPanel != null)
            {
                inspectorPanel.RefreshContent();
            }
        }
    }
}