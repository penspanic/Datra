using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Services;
using Datra.Unity.Editor.Views;
using Datra.Editor.Interfaces;

namespace Datra.Unity.Editor.Panels
{
    public class LocalizationInspectorPanel : BaseInspectorPanel
    {
        private LocalizationContext localizationContext;
        private IEditableLocalizationDataSource localizationDataSource;
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

        public void SetLocalizationContext(
            LocalizationContext context,
            Datra.Interfaces.IDataRepository repository,
            Datra.Interfaces.IDataContext dataContext,
            IEditableLocalizationDataSource dataSource)
        {
            // If already set to the same context and view exists, do nothing
            // This prevents recreating the view and losing change tracking
            if (localizationContext == context && localizationView != null && context != null)
            {
                return;
            }

            localizationContext = context;
            localizationDataSource = dataSource;

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

            // Create localization-specific view
            contentContainer.Clear();
            localizationView = new DatraLocalizationView();

            // Set data using unified pattern: dataSource is the localization data source
            localizationView.SetData(
                typeof(LocalizationContext),
                repository,
                dataContext,
                dataSource,   // source (IEditableDataSource) - will be cast to IEditableLocalizationDataSource
                context);     // localizationCtx

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

        /// <summary>
        /// Notify view that save operation completed
        /// </summary>
        public override void NotifySaveCompleted(bool success)
        {
            localizationView?.OnSaveCompleted(success);
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