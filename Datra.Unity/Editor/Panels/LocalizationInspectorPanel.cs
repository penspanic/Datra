using Datra;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Localization;
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
        private VisualElement modifiedLanguagesContainer;

        public bool HasUnsavedChanges => localizationView?.HasUnsavedChanges ?? false;

        /// <summary>
        /// Fired when user requests to sync FixedLocale keys with data
        /// </summary>
        public event Action OnSyncFixedLocaleKeysRequested;

        /// <summary>
        /// Fired when language is changed via badge click (to sync toolbar dropdown)
        /// </summary>
        public event Action<LanguageCode> OnLanguageChanged;

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
            // Create modified languages container in header (added to title-section)
            modifiedLanguagesContainer = new VisualElement();
            modifiedLanguagesContainer.AddToClassList("modified-languages-container");
            modifiedLanguagesContainer.style.flexDirection = FlexDirection.Row;
            modifiedLanguagesContainer.style.flexWrap = Wrap.Wrap;
            modifiedLanguagesContainer.style.marginTop = 8;
        }

        public void SetLocalizationContext(
            LocalizationContext context,
            IEditableRepository repository,
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

            // Add modified languages container to title section
            var titleSection = headerContainer.Q(className: "title-section");
            if (titleSection != null && modifiedLanguagesContainer.parent != titleSection)
            {
                modifiedLanguagesContainer.RemoveFromHierarchy();
                titleSection.Add(modifiedLanguagesContainer);
            }

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
            localizationView.OnDataModified += (type, isModified) =>
            {
                InvokeDataModified(type, isModified);
                UpdateModifiedLanguageBadges();
            };
            localizationView.OnSaveRequested += (type, repo) => InvokeSaveRequested(type, repo);
            localizationView.OnSyncFixedLocaleKeysRequested += () => OnSyncFixedLocaleKeysRequested?.Invoke();

            // Subscribe to text changes to update badges
            if (dataSource != null)
            {
                dataSource.OnTextChanged += (key, lang) => UpdateModifiedLanguageBadges();
            }

            contentContainer.Add(localizationView);

            // Initial update of badges
            UpdateModifiedLanguageBadges();
        }

        private void UpdateModifiedLanguageBadges()
        {
            if (modifiedLanguagesContainer == null) return;

            modifiedLanguagesContainer.Clear();

            if (localizationDataSource == null) return;

            var modifiedLanguages = localizationDataSource.GetModifiedLanguages().ToList();
            if (modifiedLanguages.Count == 0) return;

            // Add label
            var label = new Label("Modified:");
            label.style.marginRight = 6;
            label.style.color = new Color(0.7f, 0.7f, 0.7f);
            label.style.fontSize = 11;
            modifiedLanguagesContainer.Add(label);

            // Add badges for each modified language
            foreach (var lang in modifiedLanguages.OrderBy(l => l.ToIsoCode()))
            {
                var badge = CreateLanguageBadge(lang);
                modifiedLanguagesContainer.Add(badge);
            }
        }

        private VisualElement CreateLanguageBadge(LanguageCode language)
        {
            var badge = new Button(() => SwitchLanguageFromBadge(language));
            badge.AddToClassList("modified-language-badge");

            // Orange dot
            var dot = new VisualElement();
            dot.AddToClassList("modified-dot");
            dot.style.width = 6;
            dot.style.height = 6;
            dot.style.borderTopLeftRadius = 3;
            dot.style.borderTopRightRadius = 3;
            dot.style.borderBottomLeftRadius = 3;
            dot.style.borderBottomRightRadius = 3;
            dot.style.backgroundColor = new Color(1f, 0.6f, 0.2f); // Orange
            dot.style.marginRight = 4;
            badge.Add(dot);

            // Language code
            var langLabel = new Label(language.ToIsoCode().ToUpper());
            langLabel.style.fontSize = 10;
            badge.Add(langLabel);

            // Badge styling
            badge.style.flexDirection = FlexDirection.Row;
            badge.style.alignItems = Align.Center;
            badge.style.paddingLeft = 6;
            badge.style.paddingRight = 8;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.marginRight = 4;
            badge.style.borderTopLeftRadius = 10;
            badge.style.borderTopRightRadius = 10;
            badge.style.borderBottomLeftRadius = 10;
            badge.style.borderBottomRightRadius = 10;
            badge.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            badge.style.borderTopWidth = 1;
            badge.style.borderBottomWidth = 1;
            badge.style.borderLeftWidth = 1;
            badge.style.borderRightWidth = 1;
            badge.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            badge.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
            badge.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
            badge.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);

            badge.tooltip = $"Click to switch to {language.GetDisplayName()}";

            return badge;
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

            // Update modified language badges
            UpdateModifiedLanguageBadges();
        }

        /// <summary>
        /// Switch to a different language
        /// </summary>
        public async void SwitchLanguage(LanguageCode newLanguage)
        {
            if (localizationView != null)
            {
                await localizationView.SwitchLanguageAsync(newLanguage);
                UpdateModifiedLanguageBadges();
            }
        }

        /// <summary>
        /// Switch to a different language and notify toolbar (called from badge click)
        /// </summary>
        private async void SwitchLanguageFromBadge(LanguageCode newLanguage)
        {
            if (localizationView != null)
            {
                await localizationView.SwitchLanguageAsync(newLanguage);
                UpdateModifiedLanguageBadges();

                // Notify toolbar to sync dropdown
                OnLanguageChanged?.Invoke(newLanguage);
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

            // Update badges after save
            if (success)
            {
                UpdateModifiedLanguageBadges();
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