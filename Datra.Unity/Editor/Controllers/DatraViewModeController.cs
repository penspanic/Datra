using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Unity.Editor.Views;
using Datra.Unity.Editor.Panels;
using Datra.Interfaces;
using Datra.Unity.Editor.Utilities;
using Datra.Editor.Interfaces;

namespace Datra.Unity.Editor.Controllers
{
    /// <summary>
    /// Controller for managing view mode switching and related functionality
    /// </summary>
    public class DatraViewModeController
    {
        public enum ViewMode
        {
            Form,
            Table,
            Split
        }

        private ViewMode currentViewMode = ViewMode.Form;
        private VisualElement contentContainer;
        private VisualElement headerContainer;
        private DatraDataView currentView;

        // Cache views to preserve modification state when switching modes
        private DatraFormView cachedFormView;
        private DatraTableView cachedTableView;
        private SplitViewWrapper cachedSplitView;

        private Type dataType;
        private IDataRepository repository;
        private IDataContext dataContext;
        private bool isReadOnly;
        private IRepositoryChangeTracker changeTracker;

        // Localization support
        private Datra.Services.LocalizationContext localizationContext;
        private LocalizationChangeTracker localizationChangeTracker;

        // Events
        public event Action<ViewMode> OnViewModeChanged;
        public event Action<Type, IDataRepository> OnSaveRequested;
        public event Action<Type, bool> OnDataModified;  // Type, isModified
        
        // Properties
        public ViewMode CurrentMode => currentViewMode;
        
        public DatraViewModeController(VisualElement contentContainer, VisualElement headerContainer)
        {
            this.contentContainer = contentContainer;
            this.headerContainer = headerContainer;
        }
        
        public void SetData(
            Type type,
            IDataRepository repo,
            IDataContext context,
            IRepositoryChangeTracker changeTracker,
            Datra.Services.LocalizationContext localizationCtx = null,
            LocalizationChangeTracker localizationTracker = null,
            bool readOnly = false)
        {
            // Clear cached views if data type changed
            if (this.dataType != type)
            {
                ClearCachedViews();
            }

            this.dataType = type;
            this.repository = repo;
            this.dataContext = context;
            this.changeTracker = changeTracker;
            this.localizationContext = localizationCtx;
            this.localizationChangeTracker = localizationTracker;
            this.isReadOnly = readOnly;

            UpdateView();
        }

        private void ClearCachedViews()
        {
            cachedFormView?.Cleanup();
            cachedTableView?.Cleanup();
            cachedSplitView?.Cleanup();
            cachedFormView = null;
            cachedTableView = null;
            cachedSplitView = null;
        }
        
        public void SetViewMode(ViewMode mode)
        {
            if (currentViewMode == mode) return;
            
            currentViewMode = mode;
            UpdateView();
            OnViewModeChanged?.Invoke(mode);
        }
        
        public void CycleViewMode()
        {
            // Cycle through view modes
            var nextMode = (ViewMode)(((int)currentViewMode + 1) % Enum.GetValues(typeof(ViewMode)).Length);
            SetViewMode(nextMode);
        }
        
        public void CyclePreviousViewMode()
        {
            // Cycle backwards through view modes
            var values = Enum.GetValues(typeof(ViewMode));
            var currentIndex = (int)currentViewMode;
            var previousIndex = (currentIndex - 1 + values.Length) % values.Length;
            SetViewMode((ViewMode)previousIndex);
        }
        
        private void UpdateView()
        {
            CleanupCurrentView();
            
            if (dataType == null || repository == null) return;
            
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
            // Reuse cached view if available
            if (cachedFormView == null)
            {
                cachedFormView = new DatraFormView();
                cachedFormView.OnSaveRequested += HandleSaveRequest;
                cachedFormView.OnDataModified += HandleDataModified;
            }

            currentView = cachedFormView;
            currentView.SetData(dataType, repository, dataContext, changeTracker, localizationContext, localizationChangeTracker);
            currentView.IsReadOnly = isReadOnly;

            contentContainer.Add(currentView);
        }

        private void ShowTableView()
        {
            // Reuse cached view if available
            if (cachedTableView == null)
            {
                cachedTableView = new DatraTableView();
                cachedTableView.OnSaveRequested += HandleSaveRequest;
                cachedTableView.OnDataModified += HandleDataModified;
            }

            currentView = cachedTableView;
            currentView.SetData(dataType, repository, dataContext, changeTracker, localizationContext, localizationChangeTracker);
            currentView.IsReadOnly = isReadOnly;

            contentContainer.Add(currentView);
        }
        
        private void ShowSplitView()
        {
            // Reuse cached split view if available
            if (cachedSplitView == null)
            {
                var splitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);

                // Left pane - Table
                var leftPane = new VisualElement();
                leftPane.style.minWidth = 200;

                var tableView = new DatraTableView();
                tableView.ShowActionsColumn = false; // Simplified for split view
                tableView.OnDataModified += HandleDataModified;
                leftPane.Add(tableView);

                // Right pane - Form
                var rightPane = new VisualElement();
                rightPane.style.minWidth = 300;

                var formView = new DatraFormView();
                formView.OnSaveRequested += HandleSaveRequest;
                formView.OnDataModified += HandleDataModified;
                rightPane.Add(formView);

                splitView.Add(leftPane);
                splitView.Add(rightPane);

                cachedSplitView = new SplitViewWrapper(tableView, formView, splitView);
            }

            // Update data for both views
            cachedSplitView.tableView.SetData(dataType, repository, dataContext, changeTracker, localizationContext, localizationChangeTracker);
            cachedSplitView.tableView.IsReadOnly = isReadOnly;
            cachedSplitView.formView.SetData(dataType, repository, dataContext, changeTracker, localizationContext, localizationChangeTracker);
            cachedSplitView.formView.IsReadOnly = isReadOnly;

            contentContainer.Add(cachedSplitView.splitView);

            currentView = cachedSplitView;
        }
        
        private void CleanupCurrentView()
        {
            if (currentView != null)
            {
                // Don't cleanup or unsubscribe from cached views - just remove from UI
                // Safely remove from parent
                if (currentView.parent == contentContainer)
                {
                    contentContainer.Remove(currentView);
                }
                else if (currentView is SplitViewWrapper wrapper)
                {
                    if (wrapper.splitView.parent == contentContainer)
                    {
                        contentContainer.Remove(wrapper.splitView);
                    }
                }

                currentView = null;
            }

            contentContainer.Clear();
        }
        
        private void HandleSaveRequest(Type type, IDataRepository repo)
        {
            OnSaveRequested?.Invoke(type, repo);
        }
        
        private void HandleDataModified(Type type, bool isModified)
        {
            OnDataModified?.Invoke(type, isModified);
        }

        /// <summary>
        /// Refresh the modified state of the current view (e.g., after save)
        /// </summary>
        public void RefreshModifiedState()
        {
            if (currentView != null)
            {
                currentView.UpdateModifiedState();
            }
        }

        public void Cleanup()
        {
            CleanupCurrentView();
            ClearCachedViews();
        }
        
        // Wrapper class to handle split view cleanup
        private class SplitViewWrapper : DatraDataView
        {
            public DatraTableView tableView;
            public DatraFormView formView;
            public TwoPaneSplitView splitView;
            
            public SplitViewWrapper(DatraTableView table, DatraFormView form, TwoPaneSplitView split)
            {
                tableView = table;
                formView = form;
                splitView = split;
            }
            
            protected override void InitializeView()
            {
                // Not needed for wrapper - views are already initialized
            }
            
            public override void RefreshContent()
            {
                tableView?.RefreshContent();
                formView?.RefreshContent();
            }
            
            protected override void FilterItems(string searchTerm)
            {
                // Not needed for wrapper - each view handles its own filtering
            }
            
            public override void Cleanup()
            {
                tableView?.Cleanup();
                formView?.Cleanup();
            }
        }
    }
}