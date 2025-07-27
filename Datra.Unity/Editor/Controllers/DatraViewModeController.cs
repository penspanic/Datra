using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Unity.Editor.Views;
using Datra.Unity.Editor.Panels;

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
        
        private Type dataType;
        private object repository;
        private object dataContext;
        private bool isReadOnly;
        
        // Events
        public event Action<ViewMode> OnViewModeChanged;
        public event Action<Type, object> OnSaveRequested;
        public event Action<Type> OnDataModified;
        
        // Properties
        public ViewMode CurrentMode => currentViewMode;
        
        public DatraViewModeController(VisualElement contentContainer, VisualElement headerContainer)
        {
            this.contentContainer = contentContainer;
            this.headerContainer = headerContainer;
        }
        
        public void SetData(Type type, object repo, object context, bool readOnly = false)
        {
            this.dataType = type;
            this.repository = repo;
            this.dataContext = context;
            this.isReadOnly = readOnly;
            
            UpdateView();
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
            currentView = new DatraFormView();
            currentView.SetData(dataType, repository, dataContext);
            currentView.OnSaveRequested += HandleSaveRequest;
            currentView.OnDataModified += HandleDataModified;
            currentView.IsReadOnly = isReadOnly;
            contentContainer.Add(currentView);
        }
        
        private void ShowTableView()
        {
            currentView = new DatraTableView();
            currentView.SetData(dataType, repository, dataContext);
            currentView.OnSaveRequested += HandleSaveRequest;
            currentView.OnDataModified += HandleDataModified;
            currentView.IsReadOnly = isReadOnly;
            contentContainer.Add(currentView);
        }
        
        private void ShowSplitView()
        {
            var splitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            
            // Left pane - Table
            var leftPane = new VisualElement();
            leftPane.style.minWidth = 200;
            
            var tableView = new DatraTableView();
            tableView.ShowActionsColumn = false; // Simplified for split view
            tableView.SetData(dataType, repository, dataContext);
            tableView.OnDataModified += HandleDataModified;
            tableView.IsReadOnly = isReadOnly;
            leftPane.Add(tableView);
            
            // Right pane - Form
            var rightPane = new VisualElement();
            rightPane.style.minWidth = 300;
            
            var formView = new DatraFormView();
            formView.SetData(dataType, repository, dataContext);
            formView.OnSaveRequested += HandleSaveRequest;
            formView.OnDataModified += HandleDataModified;
            formView.IsReadOnly = isReadOnly;
            rightPane.Add(formView);
            
            splitView.Add(leftPane);
            splitView.Add(rightPane);
            
            contentContainer.Add(splitView);
            
            // Store both views for cleanup
            currentView = new SplitViewWrapper(tableView, formView, splitView);
        }
        
        private void CleanupCurrentView()
        {
            if (currentView != null)
            {
                currentView.OnSaveRequested -= HandleSaveRequest;
                currentView.OnDataModified -= HandleDataModified;
                currentView.Cleanup();
                
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
        
        private void HandleSaveRequest(Type type, object repo)
        {
            OnSaveRequested?.Invoke(type, repo);
        }
        
        private void HandleDataModified(Type type)
        {
            OnDataModified?.Invoke(type);
        }
        
        public void Cleanup()
        {
            CleanupCurrentView();
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
            
            public override void SetData(Type dataType, object repository, object dataContext)
            {
                // Not used, just for compatibility
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