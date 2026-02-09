#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Datra.Localization;
using Datra.Unity.Editor.Models;
using Datra.Unity.Editor.Utilities;
using UnityEngine.UIElements;

namespace Datra.Unity.Editor.Components
{
    /// <summary>
    /// Filter panel specifically for LocalizationView with category and status filters.
    /// Handles UI creation, filter state management, and persistence.
    /// </summary>
    public class LocalizationFilterPanel : VisualElement
    {
        // State
        private HashSet<string> availableCategories;
        private HashSet<string> selectedCategories;
        private TranslationStatus currentStatusFilter;
        private LanguageCode currentLanguageCode;

        // UI Elements
        private VisualElement categoryFilterBar;
        private VisualElement statusFilterBar;
        private VisualElement categoryButtonsContainer;

        // Events
        public event Action OnFilterChanged;

        // Properties
        public HashSet<string> SelectedCategories => selectedCategories;
        public TranslationStatus CurrentStatusFilter => currentStatusFilter;

        public LocalizationFilterPanel()
        {
            AddToClassList("localization-filter-panel");

            availableCategories = new HashSet<string>();
            currentStatusFilter = TranslationStatus.All;

            InitializeUI();
        }

        private void InitializeUI()
        {
            // Category filter bar
            categoryFilterBar = CreateCategoryFilterBar();
            Add(categoryFilterBar);

            // Status filter bar
            statusFilterBar = CreateStatusFilterBar();
            Add(statusFilterBar);
        }

        /// <summary>
        /// Set available categories and refresh filter buttons
        /// </summary>
        public void SetCategories(IEnumerable<string> categories)
        {
            availableCategories.Clear();
            foreach (var category in categories)
                availableCategories.Add(category);

            PopulateCategoryButtons();
        }

        /// <summary>
        /// Load filter settings for specific language
        /// </summary>
        public void LoadSettings(LanguageCode languageCode)
        {
            currentLanguageCode = languageCode;

            var savedCategories = DatraUserPreferences.GetLocalizationCategoryFilters(languageCode.ToIsoCode());
            selectedCategories = string.IsNullOrEmpty(savedCategories)
                ? null
                : new HashSet<string>(savedCategories.Split(','));

            var savedStatus = DatraUserPreferences.GetLocalizationStatusFilter(languageCode.ToIsoCode());
            currentStatusFilter = (TranslationStatus)savedStatus;

            UpdateUI();
        }

        /// <summary>
        /// Check if item matches current filters (category and status)
        /// </summary>
        public bool MatchesFilter(LocalizationKeyWrapper wrapper)
        {
            // Category filter
            if (selectedCategories != null && selectedCategories.Count > 0)
            {
                var category = string.IsNullOrEmpty(wrapper.Category) ? "(Uncategorized)" : wrapper.Category;
                if (!selectedCategories.Contains(category))
                    return false;
            }

            // Status filter
            if (currentStatusFilter != TranslationStatus.All)
            {
                bool isMissing = wrapper.IsMissing;
                bool isModified = wrapper.IsModified;

                if (currentStatusFilter == TranslationStatus.MissingOnly && !isMissing)
                    return false;
                if (currentStatusFilter == TranslationStatus.CompleteOnly && isMissing)
                    return false;
                if (currentStatusFilter == TranslationStatus.ModifiedOnly && !isModified)
                    return false;
            }

            return true;
        }

        #region Category Filter UI

        private VisualElement CreateCategoryFilterBar()
        {
            var filterBar = new VisualElement();
            filterBar.AddToClassList("category-filter-bar");
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.alignItems = Align.Center;
            filterBar.style.paddingLeft = 8;
            filterBar.style.paddingRight = 8;
            filterBar.style.paddingTop = 4;
            filterBar.style.paddingBottom = 4;

            var categoryLabel = new Label("Categories:");
            categoryLabel.style.marginRight = 8;
            filterBar.Add(categoryLabel);

            categoryButtonsContainer = new VisualElement();
            categoryButtonsContainer.name = "category-buttons-container";
            categoryButtonsContainer.style.flexDirection = FlexDirection.Row;
            categoryButtonsContainer.style.flexWrap = Wrap.Wrap;
            categoryButtonsContainer.style.flexGrow = 1;
            filterBar.Add(categoryButtonsContainer);

            var selectAllButton = new Button(SelectAllCategories);
            selectAllButton.text = "Select All";
            selectAllButton.AddToClassList("filter-action-button");
            selectAllButton.style.marginLeft = 4;
            filterBar.Add(selectAllButton);

            var clearAllButton = new Button(ClearAllCategories);
            clearAllButton.text = "Clear All";
            clearAllButton.AddToClassList("filter-action-button");
            clearAllButton.style.marginLeft = 4;
            filterBar.Add(clearAllButton);

            return filterBar;
        }

        private void PopulateCategoryButtons()
        {
            if (categoryButtonsContainer == null) return;

            categoryButtonsContainer.Clear();

            // Create buttons for each category
            foreach (var category in availableCategories.OrderBy(c => c))
            {
                var isSelected = selectedCategories == null || selectedCategories.Contains(category);
                var button = new Button(() => ToggleCategory(category));
                button.text = category;
                button.AddToClassList("category-toggle-button");
                button.style.marginRight = 4;
                button.style.marginBottom = 2;

                if (isSelected)
                    button.AddToClassList("active");

                categoryButtonsContainer.Add(button);
            }
        }

        private void ToggleCategory(string category)
        {
            if (selectedCategories == null)
            {
                selectedCategories = new HashSet<string>(availableCategories);
                selectedCategories.Remove(category);
            }
            else if (selectedCategories.Contains(category))
            {
                selectedCategories.Remove(category);
            }
            else
            {
                selectedCategories.Add(category);
            }

            // If all categories selected, set to null (no filter)
            if (selectedCategories.Count == availableCategories.Count)
                selectedCategories = null;

            UpdateCategoryButtonVisuals();
            SaveSettings();
            OnFilterChanged?.Invoke();
        }

        private void UpdateCategoryButtonVisuals()
        {
            if (categoryButtonsContainer == null) return;

            var buttons = categoryButtonsContainer.Query<Button>(className: "category-toggle-button").ToList();
            foreach (var button in buttons)
            {
                var category = button.text;
                var isSelected = selectedCategories == null || selectedCategories.Contains(category);

                if (isSelected)
                    button.AddToClassList("active");
                else
                    button.RemoveFromClassList("active");
            }
        }

        private void SelectAllCategories()
        {
            selectedCategories = null;
            UpdateCategoryButtonVisuals();
            SaveSettings();
            OnFilterChanged?.Invoke();
        }

        private void ClearAllCategories()
        {
            selectedCategories = new HashSet<string>();
            UpdateCategoryButtonVisuals();
            SaveSettings();
            OnFilterChanged?.Invoke();
        }

        #endregion

        #region Status Filter UI

        private VisualElement CreateStatusFilterBar()
        {
            var filterBar = new VisualElement();
            filterBar.AddToClassList("status-filter-bar");
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.alignItems = Align.Center;
            filterBar.style.paddingLeft = 8;
            filterBar.style.paddingRight = 8;
            filterBar.style.paddingTop = 4;
            filterBar.style.paddingBottom = 4;

            var statusLabel = new Label("Status:");
            statusLabel.style.marginRight = 8;
            filterBar.Add(statusLabel);

            var allButton = new Button(() => SetStatusFilter(TranslationStatus.All));
            allButton.text = "All";
            allButton.name = "status-all-button";
            allButton.AddToClassList("status-filter-button");
            allButton.AddToClassList("active");
            allButton.style.marginRight = 4;
            filterBar.Add(allButton);

            var missingButton = new Button(() => SetStatusFilter(TranslationStatus.MissingOnly));
            missingButton.text = "Missing Only";
            missingButton.name = "status-missing-button";
            missingButton.AddToClassList("status-filter-button");
            missingButton.style.marginRight = 4;
            filterBar.Add(missingButton);

            var completeButton = new Button(() => SetStatusFilter(TranslationStatus.CompleteOnly));
            completeButton.text = "Complete";
            completeButton.name = "status-complete-button";
            completeButton.AddToClassList("status-filter-button");
            completeButton.style.marginRight = 4;
            filterBar.Add(completeButton);

            var modifiedButton = new Button(() => SetStatusFilter(TranslationStatus.ModifiedOnly));
            modifiedButton.text = "Modified";
            modifiedButton.name = "status-modified-button";
            modifiedButton.AddToClassList("status-filter-button");
            filterBar.Add(modifiedButton);

            return filterBar;
        }

        private void SetStatusFilter(TranslationStatus status)
        {
            currentStatusFilter = status;

            var allButton = statusFilterBar?.Q<Button>("status-all-button");
            var missingButton = statusFilterBar?.Q<Button>("status-missing-button");
            var completeButton = statusFilterBar?.Q<Button>("status-complete-button");
            var modifiedButton = statusFilterBar?.Q<Button>("status-modified-button");

            allButton?.RemoveFromClassList("active");
            missingButton?.RemoveFromClassList("active");
            completeButton?.RemoveFromClassList("active");
            modifiedButton?.RemoveFromClassList("active");

            switch (status)
            {
                case TranslationStatus.All:
                    allButton?.AddToClassList("active");
                    break;
                case TranslationStatus.MissingOnly:
                    missingButton?.AddToClassList("active");
                    break;
                case TranslationStatus.CompleteOnly:
                    completeButton?.AddToClassList("active");
                    break;
                case TranslationStatus.ModifiedOnly:
                    modifiedButton?.AddToClassList("active");
                    break;
            }

            SaveSettings();
            OnFilterChanged?.Invoke();
        }

        #endregion

        #region Settings Persistence

        private void SaveSettings()
        {
            string categoriesToSave = null;
            if (selectedCategories != null && selectedCategories.Count > 0 && selectedCategories.Count < availableCategories?.Count)
            {
                categoriesToSave = string.Join(",", selectedCategories);
            }

            DatraUserPreferences.SetLocalizationCategoryFilters(currentLanguageCode.ToIsoCode(), categoriesToSave);
            DatraUserPreferences.SetLocalizationStatusFilter(currentLanguageCode.ToIsoCode(), (int)currentStatusFilter);
        }

        private void UpdateUI()
        {
            UpdateCategoryButtonVisuals();
            SetStatusFilter(currentStatusFilter);
        }

        #endregion
    }
}
