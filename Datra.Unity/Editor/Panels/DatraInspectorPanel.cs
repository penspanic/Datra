using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.Interfaces;
using Datra.Unity.Editor.Views;
using Datra.Unity.Editor.Controllers;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Panels
{
    public class DatraInspectorPanel : VisualElement
    {
        private VisualElement headerContainer;
        private VisualElement breadcrumbContainer;
        private new VisualElement contentContainer;
        private Label titleLabel;
        private Label subtitleLabel;
        
        private Type currentType;
        private object currentRepository;
        private object currentDataContext;
        
        // View mode controller
        private DatraViewModeController viewModeController;
        
        // Properties
        public Type CurrentType => currentType;
        public object CurrentRepository => currentRepository;
        
        // Events
        public event Action<Type> OnDataModified;
        public event Action<Type, object> OnSaveRequested;
        
        public DatraInspectorPanel()
        {
            AddToClassList("datra-inspector-panel");
            Initialize();
        }
        
        private void Initialize()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            
            // Header Section
            headerContainer = new VisualElement();
            headerContainer.AddToClassList("inspector-header");
            
            // Breadcrumb navigation
            breadcrumbContainer = new VisualElement();
            breadcrumbContainer.AddToClassList("breadcrumb-container");
            headerContainer.Add(breadcrumbContainer);
            
            // Title section
            var titleSection = new VisualElement();
            titleSection.AddToClassList("title-section");
            
            titleLabel = new Label();
            titleLabel.AddToClassList("inspector-title");
            titleSection.Add(titleLabel);
            
            subtitleLabel = new Label();
            subtitleLabel.AddToClassList("inspector-subtitle");
            titleSection.Add(subtitleLabel);
            
            headerContainer.Add(titleSection);
            
            // Action buttons in header
            var headerActions = new VisualElement();
            headerActions.AddToClassList("header-actions");
            
            // View mode toggle
            var viewModeContainer = new VisualElement();
            viewModeContainer.style.flexDirection = FlexDirection.Row;
            viewModeContainer.style.marginRight = 12;
            
            var formViewButton = new Button();
            formViewButton.text = "📝";
            formViewButton.tooltip = "Form View (1)";
            formViewButton.AddToClassList("view-mode-button");
            formViewButton.name = "form-view-button";
            viewModeContainer.Add(formViewButton);
            
            var tableViewButton = new Button();
            tableViewButton.text = "📊";
            tableViewButton.tooltip = "Table View (2)";
            tableViewButton.AddToClassList("view-mode-button");
            tableViewButton.name = "table-view-button";
            viewModeContainer.Add(tableViewButton);
            
            headerActions.Add(viewModeContainer);
            
            var refreshButton = new Button(() => RefreshContent());
            refreshButton.text = "↻";
            refreshButton.tooltip = "Refresh";
            refreshButton.AddToClassList("icon-button");
            headerActions.Add(refreshButton);
            
            headerContainer.Add(headerActions);
            
            Add(headerContainer);
            
            // Content Section
            contentContainer = new VisualElement();
            contentContainer.AddToClassList("inspector-content");
            contentContainer.style.flexGrow = 1;
            Add(contentContainer);
            
            // Initialize view mode controller
            viewModeController = new DatraViewModeController(contentContainer, headerContainer);
            viewModeController.OnViewModeChanged += OnViewModeChanged;
            viewModeController.OnSaveRequested += (type, repo) => OnSaveRequested?.Invoke(type, repo);
            viewModeController.OnDataModified += (type) => OnDataModified?.Invoke(type);
            
            // Update view mode toggle buttons to use controller
            var formButton = headerContainer.Q<Button>("form-view-button");
            var tableButton = headerContainer.Q<Button>("table-view-button");
            
            if (formButton != null)
            {
                // Clear existing click handlers
                formButton.clickable = new Clickable(() => viewModeController.SetViewMode(DatraViewModeController.ViewMode.Form));
                formButton.tooltip = "Form View (1)";
            }
            
            if (tableButton != null)
            {
                // Clear existing click handlers
                tableButton.clickable = new Clickable(() => viewModeController.SetViewMode(DatraViewModeController.ViewMode.Table));
                tableButton.tooltip = "Table View (2)";
            }
            
            // Show empty state initially
            ShowEmptyState();
        }
        
        public void SetDataContext(object dataContext, object repository, Type dataType)
        {
            currentDataContext = dataContext;
            currentRepository = repository;
            currentType = dataType;
            
            // Determine the default view mode based on data type
            if (dataType != null)
            {
                var isTableData = IsTableData(dataType);
                var defaultMode = isTableData ? DatraViewModeController.ViewMode.Table : DatraViewModeController.ViewMode.Form;
                
                // Get user's preferred view mode for this type
                var savedMode = DatraUserPreferences.GetViewMode(dataType);
                if (!string.IsNullOrEmpty(savedMode))
                {
                    if (Enum.TryParse<DatraViewModeController.ViewMode>(savedMode, out var mode))
                    {
                        viewModeController.SetViewMode(mode);
                    }
                    else
                    {
                        viewModeController.SetViewMode(defaultMode);
                    }
                }
                else
                {
                    viewModeController.SetViewMode(defaultMode);
                }
            }
            
            UpdateHeader();
            
            // Set data in controller
            viewModeController.SetData(dataType, repository, dataContext);
        }
        
        private void UpdateHeader()
        {
            if (currentType == null)
            {
                titleLabel.text = "No Selection";
                subtitleLabel.text = "";
                breadcrumbContainer.Clear();
                return;
            }
            
            // Update title
            titleLabel.text = currentType.Name;
            
            // Update subtitle with additional info
            var isTableData = IsTableData(currentType);
            subtitleLabel.text = isTableData ? "Table Data" : "Single Data";
            
            // Update breadcrumb
            UpdateBreadcrumb();
            
            // Update view mode buttons
            UpdateViewModeButtons();
        }
        
        private void UpdateBreadcrumb()
        {
            breadcrumbContainer.Clear();
            
            var homeButton = new Button(() => ShowEmptyState());
            homeButton.text = "Data Types";
            homeButton.AddToClassList("breadcrumb-item");
            breadcrumbContainer.Add(homeButton);
            
            var separator = new Label("›");
            separator.AddToClassList("breadcrumb-separator");
            breadcrumbContainer.Add(separator);
            
            var currentLabel = new Label(currentType.Name);
            currentLabel.AddToClassList("breadcrumb-current");
            breadcrumbContainer.Add(currentLabel);
        }
        
        private void UpdateViewModeButtons()
        {
            // Buttons are now updated by OnViewModeChanged callback
        }
        
        private void OnViewModeChanged(DatraViewModeController.ViewMode mode)
        {
            var formButton = headerContainer.Q<Button>("form-view-button");
            var tableButton = headerContainer.Q<Button>("table-view-button");
            
            if (formButton != null)
            {
                if (mode == DatraViewModeController.ViewMode.Form)
                    formButton.AddToClassList("active");
                else
                    formButton.RemoveFromClassList("active");
            }
            
            if (tableButton != null)
            {
                if (mode == DatraViewModeController.ViewMode.Table)
                    tableButton.AddToClassList("active");
                else
                    tableButton.RemoveFromClassList("active");
            }
            
            // Save user preference
            if (currentType != null)
            {
                DatraUserPreferences.SetViewMode(currentType, mode.ToString());
            }
        }
        
        public void RefreshContent()
        {
            if (currentRepository == null || currentType == null)
            {
                ShowEmptyState();
                return;
            }
            
            // Let the controller handle the view refresh
            viewModeController.SetData(currentType, currentRepository, currentDataContext);
        }
        
        // SetViewMode method removed - now handled by viewModeController
        
        private void ShowEmptyState()
        {
            contentContainer.Clear();
            
            var emptyState = new VisualElement();
            emptyState.AddToClassList("empty-state");
            
            var icon = new VisualElement();
            icon.AddToClassList("empty-state-icon");
            emptyState.Add(icon);
            
            var message = new Label("Select a data type to view and edit");
            message.AddToClassList("empty-state-message");
            emptyState.Add(message);
            
            contentContainer.Add(emptyState);
            
            titleLabel.text = "Inspector";
            subtitleLabel.text = "";
            breadcrumbContainer.Clear();
        }
        
        private bool IsTableData(Type type)
        {
            var interfaces = type.GetInterfaces();
            return interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITableData<>));
        }
        
        public void MarkTypeAsModified(Type type, bool isModified)
        {
            // Refresh content when modifications are saved
            if (!isModified && currentType == type)
            {
                RefreshContent();
            }
        }
        
        private void HandleDataModified(Type type)
        {
            OnDataModified?.Invoke(type);
        }
        
        private void HandleSaveRequested(Type type, object repo)
        {
            OnSaveRequested?.Invoke(type, repo);
        }

        private void HandleAddNewItem()
        {
            // Mark the type as modified in the navigation tree
            OnDataModified?.Invoke(currentType);

            // Optionally refresh the entire content to show the new item
            // This is useful if the view needs to be completely refreshed
            // RefreshContent();
        }


        private void HandleItemDeleted(object item)
        {
            // Mark the type as modified in the navigation tree
            OnDataModified?.Invoke(currentType);
            
            // The view will refresh itself after deletion,
            // but we need to ensure the tree shows the modified state
        }
        public void Cleanup()
        {
            // Cleanup view controller
            if (viewModeController != null)
            {
                viewModeController.OnViewModeChanged -= OnViewModeChanged;
                viewModeController.OnSaveRequested -= ((type, repo) => OnSaveRequested?.Invoke(type, repo));
                viewModeController.OnDataModified -= ((type) => OnDataModified?.Invoke(type));
                viewModeController.Cleanup();
            }
        }
    }
}