using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Services;
using Datra.Unity.Editor.Views;

namespace Datra.Unity.Editor.Panels
{
    public class LocalizationInspectorPanel : BaseInspectorPanel
    {
        private LocalizationContext localizationContext;
        private DatraLocalizationView localizationView;
        
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
        
        public void SetLocalizationContext(LocalizationContext context)
        {
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
            
            // Create localization-specific view
            contentContainer.Clear();
            localizationView = new DatraLocalizationView();
            localizationView.SetLocalizationContext(context);

            // DatraLocalizationView now extends DatraDataView, so use its events
            localizationView.OnDataModified += (type) => InvokeDataModified(type);
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
            }
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