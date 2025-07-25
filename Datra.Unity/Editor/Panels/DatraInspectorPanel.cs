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
        
        // Current view
        private DatraDataView currentView;
        
        // View mode
        public enum ViewMode { Form, Table }
        private ViewMode currentViewMode = ViewMode.Form;
        
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
            
            var formViewButton = new Button(() => SetViewMode(ViewMode.Form));
            formViewButton.text = "📝";
            formViewButton.tooltip = "Form View";
            formViewButton.AddToClassList("view-mode-button");
            if (currentViewMode == ViewMode.Form)
                formViewButton.AddToClassList("active");
            viewModeContainer.Add(formViewButton);
            
            var tableViewButton = new Button(() => SetViewMode(ViewMode.Table));
            tableViewButton.text = "📊";
            tableViewButton.tooltip = "Table View";
            tableViewButton.AddToClassList("view-mode-button");
            if (currentViewMode == ViewMode.Table)
                tableViewButton.AddToClassList("active");
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
            
            // Show empty state initially
            ShowEmptyState();
        }
        
        public void SetDataContext(object dataContext, object repository, Type dataType)
        {
            currentDataContext = dataContext;
            currentRepository = repository;
            currentType = dataType;
            
            UpdateHeader();
            RefreshContent();
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
        
        public void RefreshContent()
        {
            // Clean up current view
            if (currentView != null)
            {
                currentView.OnDataModified -= HandleDataModified;
                currentView.OnSaveRequested -= HandleSaveRequested;
                currentView.OnAddNewItem -= HandleAddNewItem;
                currentView.OnItemDeleted -= HandleItemDeleted;
                currentView.Cleanup();
                contentContainer.Remove(currentView);
                currentView = null;
            }
            
            if (currentRepository == null || currentType == null)
            {
                ShowEmptyState();
                return;
            }
            
            // Clear content container before adding new view
            contentContainer.Clear();
            
            // Create appropriate view
            if (currentViewMode == ViewMode.Table)
            {
                currentView = new DatraTableView();
            }
            else
            {
                currentView = new DatraFormView();
            }
            
            // Wire up events
            currentView.OnDataModified += HandleDataModified;
            currentView.OnSaveRequested += HandleSaveRequested;
            currentView.OnAddNewItem += HandleAddNewItem;
            currentView.OnItemDeleted += HandleItemDeleted;
            
            // Set data and add to content
            currentView.SetData(currentType, currentRepository, currentDataContext);
            contentContainer.Add(currentView);
            
            // Force style update
            currentView.style.flexGrow = 1;
        }
        
        private void SetViewMode(ViewMode mode)
        {
            currentViewMode = mode;
            
            // Update button states
            var viewButtons = headerContainer.Query<Button>(className: "view-mode-button").ToList();
            foreach (var button in viewButtons)
            {
                button.RemoveFromClassList("active");
            }
            
            if (mode == ViewMode.Form)
                viewButtons[0]?.AddToClassList("active");
            else
                viewButtons[1]?.AddToClassList("active");
            
            RefreshContent();
        }
        
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
            // Forward to current view if available
            if (currentView != null && !isModified)
            {
                currentView.RefreshContent();
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
    }
}