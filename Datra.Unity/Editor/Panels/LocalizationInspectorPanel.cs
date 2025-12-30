using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Services;
using Datra.Unity.Editor.Views;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Panels
{
    public class LocalizationInspectorPanel : BaseInspectorPanel
    {
        private LocalizationContext localizationContext;
        private LocalizationChangeTracker changeTracker;
        private DatraLocalizationView localizationView;

        public bool HasUnsavedChanges => localizationView?.HasUnsavedChanges ?? false;

        public LocalizationInspectorPanel() : base()
        {
            AddToClassList("datra-localization-inspector-panel");
        }
        
        protected override VisualElement CreateHeaderActions()
        {
            // No header actions needed - DatraLocalizationView has its own toolbar
            return null;
        }
        
        protected override void InitializePanel()
        {
            // Localization panel doesn't need any special initialization
        }

        /// <summary>
        /// Set change tracker for localization (must be called before SetLocalizationContext)
        /// </summary>
        public void SetChangeTracker(LocalizationChangeTracker tracker)
        {
            changeTracker = tracker;
        }

        public void SetLocalizationContext(LocalizationContext context, Datra.Interfaces.IDataRepository repository = null, Datra.Interfaces.IDataContext dataContext = null)
        {
            // If already set to the same context and view exists, do nothing
            // This prevents recreating the view and losing change tracking
            if (localizationContext == context && localizationView != null && context != null)
            {
                return;
            }

            localizationContext = context;

            if (context == null)
            {
                ShowEmptyState();
                return;
            }

            // Update header
            UpdateHeader("Localization", "Manage localization keys and translations");

            // Clear breadcrumbs
            breadcrumbContainer.Clear();
            UpdateBreadcrumb();

            // Create localization-specific view (only if not already created)
            contentContainer.Clear();
            localizationView = new DatraLocalizationView();

            // Set change tracker before setting context
            if (changeTracker != null)
            {
                localizationView.SetChangeTracker(changeTracker);
            }

            // Set data (repository, dataContext, dataSource) - required for base DatraDataView functionality
            // Note: Localization doesn't use EditableDataSource yet, pass null
            if (repository != null && dataContext != null)
            {
                localizationView.SetData(typeof(LocalizationContext), repository, dataContext, null);
            }

            localizationView.SetLocalizationContext(context);

            // DatraLocalizationView now extends DatraDataView, so use its events
            localizationView.OnDataModified += (type, isModified) => InvokeDataModified(type, isModified);
            localizationView.OnSaveRequested += (type, repo) => InvokeSaveRequested(type, repo);

            contentContainer.Add(localizationView);
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
            
            var currentLabel = new Label("Localization");
            currentLabel.AddToClassList("breadcrumb-current");
            breadcrumbContainer.Add(currentLabel);
        }
        
        public void RefreshContent()
        {
            if (localizationContext == null)
            {
                ShowEmptyState();
                return;
            }

            // Refresh the localization view
            if (localizationView != null)
            {
                localizationView.RefreshContent();
                localizationView.UpdateModifiedState();
            }
        }

        /// <summary>
        /// Switch to a different language
        /// </summary>
        public async void SwitchLanguage(Datra.Localization.LanguageCode newLanguage)
        {
            if (localizationView != null)
            {
                await localizationView.SwitchLanguageAsync(newLanguage);
            }
        }

        public void SaveData()
        {
            localizationView?.SaveData();
        }
        
        protected override string GetEmptyStateMessage()
        {
            return "No localization context available";
        }
        
        protected override string GetEmptyStateTitle()
        {
            return "Localization";
        }
        
        public override void Cleanup()
        {
            // Cleanup if needed
            localizationView = null;
            localizationContext = null;
        }
    }
}