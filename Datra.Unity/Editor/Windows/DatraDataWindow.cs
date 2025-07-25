using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Unity.Editor.Panels;
using Datra.Unity.Editor.Views;
using UnityEditor.UIElements;

namespace Datra.Unity.Editor.Windows
{
    public class DatraDataWindow : EditorWindow
    {
        private Type dataType;
        private object repository;
        private object dataContext;
        private string windowTitle;
        
        private DatraDataView currentView;
        private VisualElement contentContainer;
        
        // View modes
        public enum ViewMode
        {
            Form,
            Table,
            Split
        }
        
        private ViewMode currentViewMode = ViewMode.Form;
        private bool isDocked = false;
        
        public static DatraDataWindow CreateWindow(Type dataType, object repository, object dataContext, string title = null)
        {
            var window = CreateInstance<DatraDataWindow>();
            window.dataType = dataType;
            window.repository = repository;
            window.dataContext = dataContext;
            window.windowTitle = title ?? dataType.Name;
            
            window.titleContent = new GUIContent(window.windowTitle, EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(600, 400);
            
            window.Show();
            return window;
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
            
            // Create toolbar
            var toolbar = CreateToolbar();
            root.Add(toolbar);
            
            // Create content container
            contentContainer = new VisualElement();
            contentContainer.AddToClassList("data-window-content");
            contentContainer.style.flexGrow = 1;
            root.Add(contentContainer);
            
            // Initialize view
            UpdateView();
        }
        
        private VisualElement CreateToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("data-window-toolbar");
            
            // View mode buttons
            var viewModeGroup = new VisualElement();
            viewModeGroup.style.flexDirection = FlexDirection.Row;
            viewModeGroup.style.marginLeft = 8;
            
            var formButton = new ToolbarButton(() => SetViewMode(ViewMode.Form));
            formButton.text = "📝 Form";
            formButton.tooltip = "Form View";
            formButton.AddToClassList(currentViewMode == ViewMode.Form ? "active" : "");
            viewModeGroup.Add(formButton);
            
            var tableButton = new ToolbarButton(() => SetViewMode(ViewMode.Table));
            tableButton.text = "📊 Table";
            tableButton.tooltip = "Table View";
            tableButton.AddToClassList(currentViewMode == ViewMode.Table ? "active" : "");
            viewModeGroup.Add(tableButton);
            
            var splitButton = new ToolbarButton(() => SetViewMode(ViewMode.Split));
            splitButton.text = "⊟ Split";
            splitButton.tooltip = "Split View";
            splitButton.AddToClassList(currentViewMode == ViewMode.Split ? "active" : "");
            viewModeGroup.Add(splitButton);
            
            toolbar.Add(viewModeGroup);
            
            // Spacer
            toolbar.Add(new ToolbarSpacer() { style = { flexGrow = 1 } });
            
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
        
        public void SetViewMode(ViewMode mode)
        {
            currentViewMode = mode;
            UpdateView();
            
            // Update toolbar buttons
            var toolbar = rootVisualElement.Q<Toolbar>();
            var buttons = toolbar.Query<ToolbarButton>().ToList();
            foreach (var button in buttons)
            {
                button.RemoveFromClassList("active");
            }
            
            int buttonIndex = (int)mode;
            if (buttonIndex < buttons.Count)
            {
                buttons[buttonIndex].AddToClassList("active");
            }
        }
        
        private void UpdateView()
        {
            contentContainer.Clear();
            
            switch (currentViewMode)
            {
                case ViewMode.Form:
                    ShowFormView();
                    break;
                case ViewMode.Table:
                    ShowTableView();
                    break;
                case ViewMode.Split:
                    ShowSplitView();
                    break;
            }
        }
        
        private void ShowFormView()
        {
            // Clean up previous view
            CleanupCurrentView();
            
            currentView = new DatraFormView();
            currentView.SetData(dataType, repository, dataContext);
            currentView.OnSaveRequested += HandleSaveRequest;
            currentView.OnDataModified += HandleDataModified;
            contentContainer.Add(currentView);
        }
        
        private void ShowTableView()
        {
            // Clean up previous view
            CleanupCurrentView();
            
            currentView = new DatraTableView();
            currentView.SetData(dataType, repository, dataContext);
            currentView.OnSaveRequested += HandleSaveRequest;
            currentView.OnDataModified += HandleDataModified;
            contentContainer.Add(currentView);
        }
        
        private void ShowSplitView()
        {
            // Clean up previous view
            CleanupCurrentView();
            
            var splitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            
            // Left pane - Table
            var leftPane = new VisualElement();
            leftPane.style.minWidth = 200;
            
            var tableView = new DatraTableView();
            tableView.ShowActionsColumn = false; // Simplified for split view
            tableView.SetData(dataType, repository, dataContext);
            tableView.OnDataModified += HandleDataModified;
            leftPane.Add(tableView);
            
            // Right pane - Form
            var rightPane = new VisualElement();
            rightPane.style.minWidth = 300;
            
            var formView = new DatraFormView();
            formView.SetData(dataType, repository, dataContext);
            formView.OnSaveRequested += HandleSaveRequest;
            formView.OnDataModified += HandleDataModified;
            rightPane.Add(formView);
            
            splitView.Add(leftPane);
            splitView.Add(rightPane);
            
            contentContainer.Add(splitView);
            
            // Connect selection
            tableView.OnItemSelected += (item) => {
                // Update form to show selected item
                // This would require extending DatraFormView to support single item display
            };
        }
        
        private void CleanupCurrentView()
        {
            if (currentView != null)
            {
                currentView.OnSaveRequested -= HandleSaveRequest;
                currentView.OnDataModified -= HandleDataModified;
                currentView.Cleanup();
                contentContainer.Remove(currentView);
                currentView = null;
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
        
        private void HandleSaveRequest(Type type, object repo)
        {
            // Save logic here
            Debug.Log($"Saving {type.Name}");
        }
        
        
        private void HandleDataModified(Type type)
        {
            // Update title to show modified state
            titleContent.text = windowTitle + " *";
        }
        
        private void OnDestroy()
        {
            // Cleanup
            CleanupCurrentView();
        }
    }
}