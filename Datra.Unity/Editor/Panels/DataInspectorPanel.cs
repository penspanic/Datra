using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.Interfaces;
using Datra.Unity.Editor.Controllers;
using Datra.Unity.Editor.Utilities;
using Datra.Editor.Interfaces;
using Datra.Unity.Editor.Views;

namespace Datra.Unity.Editor.Panels
{
    public class DataInspectorPanel : BaseInspectorPanel
    {
        private Type currentType;
        private IDataRepository currentRepository;
        private IDataContext currentDataContext;
        private IRepositoryChangeTracker currentChangeTracker;

        // Localization support
        private Datra.Services.LocalizationContext localizationContext;
        private LocalizationChangeTracker localizationChangeTracker;

        // View mode controller
        private DatraViewModeController viewModeController;

        // Properties
        public Type CurrentType => currentType;
        public IDataRepository CurrentRepository => currentRepository;
        
        public DataInspectorPanel() : base()
        {
            AddToClassList("datra-data-inspector-panel");
        }
        
        protected override VisualElement CreateHeaderActions()
        {
            var headerActions = new VisualElement();
            
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
            
            return headerActions;
        }
        
        protected override void InitializePanel()
        {
            // Initialize view mode controller
            viewModeController = new DatraViewModeController(contentContainer, headerContainer);
            viewModeController.OnViewModeChanged += OnViewModeChanged;
            viewModeController.OnSaveRequested += (type, repo) => InvokeSaveRequested(type, repo);
            viewModeController.OnDataModified += (type, isModified) => InvokeDataModified(type, isModified);
            
            // Update view mode toggle buttons to use controller
            var formButton = headerContainer.Q<Button>("form-view-button");
            var tableButton = headerContainer.Q<Button>("table-view-button");
            
            if (formButton != null)
            {
                formButton.clickable = new Clickable(() => viewModeController.SetViewMode(DatraViewModeController.ViewMode.Form));
                formButton.tooltip = "Form View (1)";
            }
            
            if (tableButton != null)
            {
                tableButton.clickable = new Clickable(() => viewModeController.SetViewMode(DatraViewModeController.ViewMode.Table));
                tableButton.tooltip = "Table View (2)";
            }
        }
        
        public void SetDataContext(IDataContext dataContext, IDataRepository repository, Type dataType, IRepositoryChangeTracker changeTracker)
        {
            currentDataContext = dataContext;
            currentRepository = repository;
            currentChangeTracker = changeTracker;
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

            UpdateDataHeader();

            // Set data in controller
            viewModeController.SetData(dataType, repository, dataContext, changeTracker, localizationContext, localizationChangeTracker);
        }

        private void UpdateDataHeader()
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
            viewModeController.SetData(currentType, currentRepository, currentDataContext, currentChangeTracker, localizationContext, localizationChangeTracker);
        }
        
        protected override string GetEmptyStateMessage()
        {
            return "Select a data type to view and edit";
        }
        
        private bool IsTableData(Type type)
        {
            var interfaces = type.GetInterfaces();
            return interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITableData<>));
        }

        /// <summary>
        /// Refresh the modified state of the current view (e.g., after save)
        /// </summary>
        public void RefreshModifiedState()
        {
            viewModeController?.RefreshModifiedState();
        }

        public override void Cleanup()
        {
            // Cleanup view controller
            if (viewModeController != null)
            {
                viewModeController.OnViewModeChanged -= OnViewModeChanged;
                viewModeController.OnSaveRequested -= ((type, repo) => InvokeSaveRequested(type, repo));
                viewModeController.OnDataModified -= ((type, isModified) => InvokeDataModified(type, isModified));
                viewModeController.Cleanup();
            }
        }

        /// <summary>
        /// Sets the localization context for FixedLocale property support
        /// </summary>
        public void SetLocalizationContext(Datra.Services.LocalizationContext context, LocalizationChangeTracker tracker)
        {
            localizationContext = context;
            localizationChangeTracker = tracker;
        }
    }
}