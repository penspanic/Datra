using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Interfaces;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Unity.Editor.Panels;
using Datra.Unity.Editor.Views;
using Datra.Unity.Editor.Controllers;
using Datra.Unity.Editor.Utilities;
using Datra.Editor.Interfaces;
using UnityEditor.UIElements;

namespace Datra.Unity.Editor.Windows
{
    public class DatraDataWindow : EditorWindow
    {
        private Type dataType;
        private IDataRepository repository;
        private IDataContext dataContext;
        private IEditableDataSource dataSource;
        private string windowTitle;

        private VisualElement contentContainer;
        private DatraViewModeController viewModeController;
        private bool isDocked = false;

        private DatraViewModeController.ViewMode? initialViewMode;
        private bool hasModifications = false;

        public static DatraDataWindow CreateWindow(Type dataType, IDataRepository repository, IDataContext dataContext, IEditableDataSource dataSource, string title = null)
        {
            var window = CreateInstance<DatraDataWindow>();
            window.dataType = dataType;
            window.repository = repository;
            window.dataContext = dataContext;
            window.dataSource = dataSource;
            window.windowTitle = title ?? dataType.Name;

            window.titleContent = new GUIContent(window.windowTitle, EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(600, 400);

            window.Show();
            return window;
        }
        
        public void SetInitialViewMode(DatraViewModeController.ViewMode mode)
        {
            initialViewMode = mode;
            if (viewModeController != null)
            {
                viewModeController.SetViewMode(mode);
            }
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            // Load stylesheets
            var stylePaths = new[]
            {
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraDataWindow.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraInspectorPanel.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraTableView.uss",
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraPropertyField.uss"
            };
            
            foreach (var path in stylePaths)
            {
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (styleSheet != null)
                {
                    root.styleSheets.Add(styleSheet);
                }
            }
            // Create content container
            contentContainer = new VisualElement();
            contentContainer.AddToClassList("data-window-content");
            contentContainer.style.flexGrow = 1;
            root.Add(contentContainer);

            // Initialize view mode controller
            viewModeController = new DatraViewModeController(contentContainer, headerContainer: root);
            viewModeController.OnViewModeChanged += OnViewModeChanged;
            viewModeController.OnSaveRequested += HandleSaveRequest;
            viewModeController.OnDataModified += HandleDataModified;
            viewModeController.SetData(dataType, repository, dataContext, dataSource);
            
            // Create toolbar
            var toolbar = CreateToolbar();
            root.Add(toolbar);
            
            
            
            // Apply initial view mode if set
            if (initialViewMode.HasValue)
            {
                viewModeController.SetViewMode(initialViewMode.Value);
            }
        }
        
        private VisualElement CreateToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("data-window-toolbar");
            
            // View mode buttons
            var viewModeGroup = new VisualElement();
            viewModeGroup.style.flexDirection = FlexDirection.Row;
            viewModeGroup.style.marginLeft = 8;
            
            var formButton = new ToolbarButton(() => viewModeController.SetViewMode(DatraViewModeController.ViewMode.Form));
            formButton.text = "📝 Form";
            formButton.tooltip = "Form View (1)";
            formButton.name = "form-button";
            formButton.AddToClassList(viewModeController.CurrentMode == DatraViewModeController.ViewMode.Form ? "active" : "");
            viewModeGroup.Add(formButton);
            
            var tableButton = new ToolbarButton(() => viewModeController.SetViewMode(DatraViewModeController.ViewMode.Table));
            tableButton.text = "📊 Table";
            tableButton.tooltip = "Table View (2)";
            tableButton.name = "table-button";
            tableButton.AddToClassList(viewModeController.CurrentMode == DatraViewModeController.ViewMode.Table ? "active" : "");
            viewModeGroup.Add(tableButton);
            
            var splitButton = new ToolbarButton(() => viewModeController.SetViewMode(DatraViewModeController.ViewMode.Split));
            splitButton.text = "⊟ Split";
            splitButton.tooltip = "Split View (3)";
            splitButton.name = "split-button";
            splitButton.AddToClassList(viewModeController.CurrentMode == DatraViewModeController.ViewMode.Split ? "active" : "");
            viewModeGroup.Add(splitButton);
            
            toolbar.Add(viewModeGroup);
            
            // Spacer
            toolbar.Add(new ToolbarSpacer() { style = { flexGrow = 1 } });
            
            // Save button
            var saveButton = new ToolbarButton(async () => await SaveData());
            saveButton.text = "💾 Save";
            saveButton.tooltip = "Save Changes (Ctrl+S)";
            saveButton.name = "save-button";
            toolbar.Add(saveButton);
            
            // Reload button
            var reloadButton = new ToolbarButton(async () => await ReloadData());
            reloadButton.text = "🔄 Reload";
            reloadButton.tooltip = "Reload Data";
            reloadButton.name = "reload-button";
            toolbar.Add(reloadButton);
            
            // Dock/Undock button
            var dockButton = new ToolbarButton(ToggleDocking);
            dockButton.text = isDocked ? "📌 Docked" : "🔓 Floating";
            dockButton.tooltip = "Toggle docking";
            toolbar.Add(dockButton);
            
            // Settings button
            var settingsButton = new ToolbarButton(ShowSettings);
            settingsButton.text = "⚙";
            settingsButton.tooltip = "Settings";
            toolbar.Add(settingsButton);
            
            return toolbar;
        }
        
        private void OnViewModeChanged(DatraViewModeController.ViewMode mode)
        {
            // Update toolbar buttons
            var toolbar = rootVisualElement.Q<Toolbar>();
            var formButton = toolbar.Q<ToolbarButton>("form-button");
            var tableButton = toolbar.Q<ToolbarButton>("table-button");
            var splitButton = toolbar.Q<ToolbarButton>("split-button");
            
            // Remove active class from all buttons
            formButton?.RemoveFromClassList("active");
            tableButton?.RemoveFromClassList("active");
            splitButton?.RemoveFromClassList("active");
            
            // Add active class to current mode button
            switch (mode)
            {
                case DatraViewModeController.ViewMode.Form:
                    formButton?.AddToClassList("active");
                    break;
                case DatraViewModeController.ViewMode.Table:
                    tableButton?.AddToClassList("active");
                    break;
                case DatraViewModeController.ViewMode.Split:
                    splitButton?.AddToClassList("active");
                    break;
            }
        }
        
        public void SetData(Type type, IDataRepository repo, IDataContext context, IEditableDataSource source = null)
        {
            dataType = type;
            repository = repo;
            dataContext = context;
            dataSource = source;

            if (viewModeController != null)
            {
                viewModeController.SetData(type, repo, context, dataSource);
            }
        }
        
        private bool IsTableData(Type type)
        {
            var interfaces = type.GetInterfaces();
            return interfaces.Any(i => i.IsGenericType && 
                i.GetGenericTypeDefinition() == typeof(Datra.Interfaces.ITableData<>));
        }
        
        private void ToggleDocking()
        {
            isDocked = !isDocked;
            
            if (isDocked)
            {
                // Dock to main window
                var mainWindow = GetWindow<DatraEditorWindow>();
                if (mainWindow != null)
                {
                    // Add as tab to main window
                    this.Close();
                    mainWindow.AddDataTab(dataType, repository, dataContext);
                }
            }
            else
            {
                // Already floating
                var button = rootVisualElement.Q<ToolbarButton>();
                button.text = "🔓 Floating";
            }
        }
        
        private void ShowSettings()
        {
            var menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Auto Save"), EditorPrefs.GetBool("Datra.AutoSave", false), () => {
                EditorPrefs.SetBool("Datra.AutoSave", !EditorPrefs.GetBool("Datra.AutoSave", false));
            });
            
            menu.AddItem(new GUIContent("Show Modified Indicator"), true, () => {});
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Export Data/JSON"), false, () => ExportData("json"));
            menu.AddItem(new GUIContent("Export Data/CSV"), false, () => ExportData("csv"));
            
            menu.ShowAsContext();
        }
        
        private void ExportData(string format)
        {
            Debug.Log($"Export as {format} - Not implemented yet");
        }
        
        private async void HandleSaveRequest(Type type, IDataRepository repo)
        {
            try
            {
                await repo.SaveAsync();
                hasModifications = false;
                titleContent.text = windowTitle;
                EditorUtility.DisplayDialog("Success", $"{type.Name} saved successfully!", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to save {type.Name}: {e.Message}", "OK");
            }
        }

        private void HandleDataModified(Type type, bool isModified)
        {
            hasModifications = isModified;
            if (isModified)
            {
                titleContent.text = windowTitle + " *";
            }
            else
            {
                titleContent.text = windowTitle;
            }
        }

        private async Task SaveData()
        {
            try
            {
                await repository.SaveAsync();
                hasModifications = false;
                titleContent.text = windowTitle;
                EditorUtility.DisplayDialog("Success", $"{dataType.Name} saved successfully!", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to save: {e.Message}", "OK");
            }
        }

        private async Task ReloadData()
        {
            try
            {
                await dataContext.LoadAllAsync();
                hasModifications = false;
                titleContent.text = windowTitle;
                viewModeController.SetData(dataType, repository, dataContext, dataSource);
                EditorUtility.DisplayDialog("Success", "Data reloaded successfully!", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to reload: {e.Message}", "OK");
            }
        }

        private void OnDestroy()
        {
            // Check for unsaved changes
            if (hasModifications)
            {
                var result = EditorUtility.DisplayDialogComplex(
                    "Unsaved Changes",
                    $"The {windowTitle} window has unsaved changes. What would you like to do?",
                    "Save",
                    "Cancel",
                    "Don't Save"
                );

                if (result == 0) // Save
                {
                    // Save synchronously before closing
                    _ = SaveData();
                }
                else if (result == 1) // Cancel
                {
                    // This won't prevent the window from closing, but at least we tried
                    Debug.LogWarning("Window closed with unsaved changes.");
                }
            }
            
            // Cleanup
            if (viewModeController != null)
            {
                viewModeController.OnViewModeChanged -= OnViewModeChanged;
                viewModeController.OnSaveRequested -= HandleSaveRequest;
                viewModeController.OnDataModified -= HandleDataModified;
                viewModeController.Cleanup();
            }
        }
    }
}