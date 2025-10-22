using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Localization;
using Datra.Models;
using Datra.Services;
using Datra.Unity.Editor.Components;
using Datra.Unity.Editor.Models;
using Datra.Unity.Editor.Utilities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Datra.Unity.Editor.Views
{
    /// <summary>
    /// Virtualized localization view with category and status filtering
    /// </summary>
    public class DatraLocalizationView : VirtualizedTableView
    {
        // Localization-specific
        private LocalizationContext localizationContext;
        private new LocalizationChangeTracker changeTracker;
        private LanguageCode currentLanguageCode;
        private DropdownField languageDropdown;
        private bool isLoading = false;
        private VisualElement loadingOverlay;

        // Filtering
        private HashSet<string> availableCategories;
        private HashSet<string> selectedCategories;
        private TranslationStatus currentStatusFilter = TranslationStatus.All;
        private VisualElement categoryFilterBar;
        private VisualElement statusFilterBar;
        private Label statisticsLabel;

        // Column widths
        private const float ActionsColumnWidth = 60f;
        private const float KeyColumnWidth = 300f;
        private const float CategoryColumnWidth = 150f;

        public DatraLocalizationView() : base()
        {
            AddToClassList("datra-localization-view");
            availableCategories = new HashSet<string>();
        }

        /// <summary>
        /// Override to check modifications from external LocalizationChangeTracker
        /// </summary>
        protected override bool HasActualModifications()
        {
            return changeTracker?.HasModifications() ?? false;
        }

        protected override void CreateAdditionalHeaderUI()
        {
            // Category filter bar
            categoryFilterBar = CreateCategoryFilterBar();
            headerContainer.Add(categoryFilterBar);

            // Status filter bar
            statusFilterBar = CreateStatusFilterBar();
            headerContainer.Add(statusFilterBar);

            // Statistics bar
            var statisticsBar = CreateStatisticsBar();
            headerContainer.Add(statisticsBar);
        }

        protected override VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("localization-toolbar");
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 36;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;

            // Language dropdown
            languageDropdown = new DropdownField("Language:");
            languageDropdown.style.minWidth = 200;
            languageDropdown.RegisterValueChangedCallback(async evt => await OnLanguageChanged(evt.newValue));
            toolbar.Add(languageDropdown);

            // Add button
            var addButton = new Button(() => {
                if (!isReadOnly)
                    OnAddButtonClicked();
            });
            addButton.text = "➕ Add Key";
            addButton.AddToClassList("table-add-button");
            addButton.SetEnabled(!isReadOnly);
            toolbar.Add(addButton);

            // Auto translate button
            var translateButton = new Button(AutoTranslateAll);
            translateButton.text = "🌐 Auto Translate";
            translateButton.tooltip = "Auto-translate all missing keys";
            translateButton.SetEnabled(!isReadOnly);
            toolbar.Add(translateButton);

            // Search field
            toolbarSearchField = new ToolbarSearchField();
            toolbarSearchField.AddToClassList("table-search");
            toolbarSearchField.style.flexGrow = 1;
            toolbarSearchField.RegisterValueChangedCallback(evt => {
                currentSearchTerm = evt.newValue;
                searchDebouncer.Trigger(evt.newValue);
            });
            toolbar.Add(toolbarSearchField);

            searchField = toolbarSearchField;

            return toolbar;
        }

        public override void SetData(Type type, object repo, object context, IRepositoryChangeTracker tracker)
        {
            dataType = type;
            repository = repo;
            dataContext = context;
            changeTracker = tracker as LocalizationChangeTracker;

            if (context is LocalizationContext locContext)
            {
                localizationContext = locContext;
                PopulateLanguageDropdown();
            }

            CleanupFields();
        }

        /// <summary>
        /// Set change tracker (must be called before SetLocalizationContext)
        /// </summary>
        public void SetChangeTracker(LocalizationChangeTracker tracker)
        {
            changeTracker = tracker;
        }

        /// <summary>
        /// Set localization context and initialize the view
        /// </summary>
        public async void SetLocalizationContext(LocalizationContext context)
        {
            localizationContext = context;

            if (context == null)
            {
                headerContainer.SetEnabled(false);
                contentContainer.SetEnabled(false);
                return;
            }

            // Set required fields for VirtualizedTableView.RefreshContent
            dataType = typeof(LocalizationKeyWrapper);

            try
            {
                repository = context.KeyRepository;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to get KeyRepository: {e.Message}");
                UpdateStatus("Error: KeyRepository not initialized");
                return;
            }

            dataContext = context;

            // Initialize language dropdown
            var languages = context.GetAvailableLanguageIsoCodes().ToList();
            languageDropdown.choices = languages;

            if (languages.Count > 0)
            {
                currentLanguageCode = context.CurrentLanguageCode;
                languageDropdown.value = context.CurrentLanguage;
                await LoadLanguageDataAsync(currentLanguageCode);
            }
        }

        private async Task LoadLanguageDataAsync(LanguageCode languageCode)
        {
            ShowLoading(true);

            try
            {
                Debug.Log($"[DatraLocalizationView] Loading language: {languageCode}");
                await localizationContext.LoadLanguageAsync(languageCode);
                Debug.Log($"[DatraLocalizationView] Language loaded successfully");

                // Initialize change tracker for this language (only if not already initialized)
                if (changeTracker != null && !changeTracker.IsLanguageInitialized(languageCode))
                {
                    Debug.Log($"[DatraLocalizationView] Initializing change tracker for {languageCode}");
                    changeTracker.InitializeLanguage(languageCode);
                }

                Debug.Log($"[DatraLocalizationView] Refreshing content...");
                RefreshContent();
                UpdateStatus($"Loaded language: {languageCode.GetDisplayName()}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load language: {e.Message}\nStack trace: {e.StackTrace}");
                UpdateStatus($"Error: {e.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        protected override List<object> LoadItemsFromRepository()
        {
            if (localizationContext == null)
                return new List<object>();

            var keys = localizationContext.GetAllKeys();
            var wrappers = new List<object>();

            foreach (var keyId in keys)
            {
                var text = localizationContext.GetText(keyId);

                // Remove [Missing: prefix for display (we'll show it visually instead)
                if (!string.IsNullOrEmpty(text) && text.StartsWith("[Missing:"))
                {
                    text = ""; // Show as empty instead of [Missing: Key]
                }

                var keyData = localizationContext.GetKeyData(keyId);
                var wrapper = new LocalizationKeyWrapper(keyId, text, keyData);
                wrappers.Add(wrapper);
            }

            return wrappers;
        }

        protected override void CreateHeaderCells()
        {
            // Actions column
            var actionsHeader = CreateHeaderCell("", ActionsColumnWidth);
            headerRow.Add(actionsHeader);

            // Key column
            var keyHeader = CreateHeaderCell("Key", KeyColumnWidth);
            headerRow.Add(keyHeader);

            // Category column
            var categoryHeader = CreateHeaderCell("Category", CategoryColumnWidth);
            headerRow.Add(categoryHeader);

            // Text column
            var textHeader = CreateHeaderCell("Text", 400f);
            textHeader.style.flexGrow = 1;
            headerRow.Add(textHeader);
        }

        protected override void CreateRowCells(VisualElement row)
        {
            // Actions cell
            var actionsCell = new VisualElement();
            actionsCell.AddToClassList("table-cell");
            actionsCell.style.width = ActionsColumnWidth;
            actionsCell.style.minWidth = ActionsColumnWidth;
            actionsCell.style.justifyContent = Justify.Center;
            actionsCell.style.alignItems = Align.Center;

            var deleteButton = new Button();
            deleteButton.text = "🗑";
            deleteButton.tooltip = "Delete Key";
            deleteButton.AddToClassList("table-delete-button");
            actionsCell.Add(deleteButton);

            row.Add(actionsCell);

            // Key cell
            var keyCell = new VisualElement();
            keyCell.AddToClassList("table-cell");
            keyCell.style.width = KeyColumnWidth;
            keyCell.style.minWidth = KeyColumnWidth;
            keyCell.style.paddingLeft = 8;
            keyCell.style.paddingRight = 8;
            row.Add(keyCell);

            // Category cell
            var categoryCell = new VisualElement();
            categoryCell.AddToClassList("table-cell");
            categoryCell.style.width = CategoryColumnWidth;
            categoryCell.style.minWidth = CategoryColumnWidth;
            categoryCell.style.paddingLeft = 8;
            categoryCell.style.paddingRight = 8;
            row.Add(categoryCell);

            // Text cell
            var textCell = new VisualElement();
            textCell.AddToClassList("table-cell");
            textCell.style.flexGrow = 1;
            textCell.style.paddingLeft = 8;
            textCell.style.paddingRight = 8;
            row.Add(textCell);
        }

        protected override void BindRowData(VisualElement row, object item, int index)
        {
            if (!(item is LocalizationKeyWrapper wrapper)) return;

            // Remove old missing indicator
            row.RemoveFromClassList("missing-locale-row");

            // Bind actions cell
            var actionsCell = row[0];
            actionsCell.RemoveFromClassList("missing-locale-cell");

            var deleteButton = actionsCell.Q<Button>();
            if (deleteButton != null)
            {
                deleteButton.SetEnabled(!isReadOnly && !wrapper.IsFixedKey);
                deleteButton.clicked += () => DeleteLocalizationKey(wrapper);
            }

            // Add missing indicator if needed
            if (wrapper.IsMissing)
            {
                row.AddToClassList("missing-locale-row");
                actionsCell.AddToClassList("missing-locale-cell");
            }

            // Bind key cell
            var keyCell = row[1];
            keyCell.Clear();

            var keyLabel = new Label(wrapper.Id);
            keyLabel.style.fontSize = 11;
            keyLabel.style.overflow = Overflow.Hidden;
            keyLabel.style.textOverflow = TextOverflow.Ellipsis;
            keyLabel.tooltip = $"{wrapper.Id}\n{wrapper.Description}";

            if (wrapper.IsFixedKey)
            {
                keyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                keyLabel.style.color = new Color(0.7f, 0.8f, 0.9f);
            }

            keyCell.Add(keyLabel);

            // Bind category cell
            var categoryCell = row[2];
            categoryCell.Clear();

            var categoryText = string.IsNullOrEmpty(wrapper.Category) ? "(Uncategorized)" : wrapper.Category;
            var categoryLabel = new Label(categoryText);
            categoryLabel.style.fontSize = 11;
            categoryLabel.style.overflow = Overflow.Hidden;
            categoryLabel.style.textOverflow = TextOverflow.Ellipsis;
            categoryLabel.tooltip = categoryText;

            if (string.IsNullOrEmpty(wrapper.Category))
            {
                categoryLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                categoryLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            }

            categoryCell.Add(categoryLabel);

            // Bind text cell
            var textCell = row[3];
            textCell.Clear();

            if (isReadOnly)
            {
                var textLabel = new Label(wrapper.Text);
                textLabel.style.fontSize = 11;
                textCell.Add(textLabel);
            }
            else
            {
                var textProperty = typeof(LocalizationKeyWrapper).GetProperty("Text");
                var field = new DatraPropertyField(wrapper, textProperty, DatraFieldLayoutMode.Table);

                field.OnValueChanged += (propName, newValue) => {
                    OnTextChanged(wrapper, newValue as string);
                };

                textCell.Add(field);

                // Mark as modified if in change tracker
                if (changeTracker != null && changeTracker.IsModified(wrapper.Id))
                {
                    field.SetModified(true);
                    textCell.AddToClassList("modified-cell");
                }
            }
        }

        protected override bool MatchesFilter(object item, string searchTerm)
        {
            if (!(item is LocalizationKeyWrapper wrapper)) return false;

            // Search term filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                bool matchesSearch = wrapper.Id.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   wrapper.Text.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   (wrapper.Description?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                                   (wrapper.Category?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;

                if (!matchesSearch) return false;
            }

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

                if (currentStatusFilter == TranslationStatus.MissingOnly && !isMissing)
                    return false;
                if (currentStatusFilter == TranslationStatus.CompleteOnly && isMissing)
                    return false;
            }

            return true;
        }

        protected override void OnFilterApplied(int filteredCount, int totalCount)
        {
            UpdateStatistics();
        }

        // Category filter UI
        private VisualElement CreateCategoryFilterBar()
        {
            var filterBar = new VisualElement();
            filterBar.AddToClassList("category-filter-bar");

            var categoryLabel = new Label("Categories:");
            filterBar.Add(categoryLabel);

            var categoryButtonsContainer = new VisualElement();
            categoryButtonsContainer.name = "category-buttons-container";
            filterBar.Add(categoryButtonsContainer);

            var selectAllButton = new Button(() => SelectAllCategories());
            selectAllButton.text = "Select All";
            selectAllButton.AddToClassList("filter-action-button");
            filterBar.Add(selectAllButton);

            var clearAllButton = new Button(() => ClearAllCategories());
            clearAllButton.text = "Clear All";
            clearAllButton.AddToClassList("filter-action-button");
            filterBar.Add(clearAllButton);

            return filterBar;
        }

        private VisualElement CreateStatusFilterBar()
        {
            var filterBar = new VisualElement();
            filterBar.AddToClassList("status-filter-bar");

            var statusLabel = new Label("Status:");
            filterBar.Add(statusLabel);

            var allButton = new Button(() => SetStatusFilter(TranslationStatus.All));
            allButton.text = "All";
            allButton.name = "status-all-button";
            allButton.AddToClassList("status-filter-button");
            allButton.AddToClassList("active");
            filterBar.Add(allButton);

            var missingButton = new Button(() => SetStatusFilter(TranslationStatus.MissingOnly));
            missingButton.text = "Missing Only";
            missingButton.name = "status-missing-button";
            missingButton.AddToClassList("status-filter-button");
            filterBar.Add(missingButton);

            var completeButton = new Button(() => SetStatusFilter(TranslationStatus.CompleteOnly));
            completeButton.text = "Complete";
            completeButton.name = "status-complete-button";
            completeButton.AddToClassList("status-filter-button");
            filterBar.Add(completeButton);

            return filterBar;
        }

        private VisualElement CreateStatisticsBar()
        {
            var statsBar = new VisualElement();
            statsBar.AddToClassList("statistics-bar");

            statisticsLabel = new Label("Total: 0 | Shown: 0 | Missing: 0");
            statsBar.Add(statisticsLabel);

            return statsBar;
        }

        // Filter management
        private void PopulateCategoryButtons()
        {
            var container = categoryFilterBar?.Q("category-buttons-container");
            if (container == null) return;

            container.Clear();

            // Collect categories
            availableCategories.Clear();
            foreach (var item in allItems)
            {
                if (item is LocalizationKeyWrapper wrapper)
                {
                    var category = string.IsNullOrEmpty(wrapper.Category) ? "(Uncategorized)" : wrapper.Category;
                    availableCategories.Add(category);
                }
            }

            // Load saved filter settings
            LoadFilterSettings();

            // Create buttons
            foreach (var category in availableCategories.OrderBy(c => c))
            {
                var isSelected = selectedCategories == null || selectedCategories.Contains(category);
                var button = new Button(() => ToggleCategory(category));
                button.text = category;
                button.AddToClassList("category-toggle-button");

                if (isSelected)
                    button.AddToClassList("active");

                container.Add(button);
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

            if (selectedCategories.Count == availableCategories.Count)
                selectedCategories = null;

            UpdateCategoryButtonVisuals();
            ApplyFilter(currentSearchTerm);
            SaveFilterSettings();
        }

        private void UpdateCategoryButtonVisuals()
        {
            var container = categoryFilterBar?.Q("category-buttons-container");
            if (container == null) return;

            var buttons = container.Query<Button>(className: "category-toggle-button").ToList();
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
            ApplyFilter(currentSearchTerm);
            SaveFilterSettings();
        }

        private void ClearAllCategories()
        {
            selectedCategories = new HashSet<string>();
            UpdateCategoryButtonVisuals();
            ApplyFilter(currentSearchTerm);
            SaveFilterSettings();
        }

        private void SetStatusFilter(TranslationStatus status)
        {
            currentStatusFilter = status;

            var allButton = statusFilterBar?.Q<Button>("status-all-button");
            var missingButton = statusFilterBar?.Q<Button>("status-missing-button");
            var completeButton = statusFilterBar?.Q<Button>("status-complete-button");

            allButton?.RemoveFromClassList("active");
            missingButton?.RemoveFromClassList("active");
            completeButton?.RemoveFromClassList("active");

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
            }

            ApplyFilter(currentSearchTerm);
            SaveFilterSettings();
        }

        private void UpdateStatistics()
        {
            if (statisticsLabel == null) return;

            var totalCount = allItems.Count;
            var shownCount = filteredItems.Count;
            var missingCount = allItems.Count(item => item is LocalizationKeyWrapper w && w.IsMissing);

            statisticsLabel.text = $"Total: {totalCount:N0} | Shown: {shownCount:N0} | Missing: {missingCount:N0}";

            if (missingCount > 0)
                statisticsLabel.style.color = new Color(1f, 0.7f, 0.3f);
            else
                statisticsLabel.style.color = Color.white;
        }

        private void LoadFilterSettings()
        {
            var savedCategories = DatraUserPreferences.GetLocalizationCategoryFilters(currentLanguageCode.ToIsoCode());
            selectedCategories = string.IsNullOrEmpty(savedCategories)
                ? null
                : new HashSet<string>(savedCategories.Split(','));

            var savedStatus = DatraUserPreferences.GetLocalizationStatusFilter(currentLanguageCode.ToIsoCode());
            currentStatusFilter = (TranslationStatus)savedStatus;

            SetStatusFilter(currentStatusFilter);
        }

        private void SaveFilterSettings()
        {
            string categoriesToSave = null;
            if (selectedCategories != null && selectedCategories.Count > 0 && selectedCategories.Count < availableCategories?.Count)
            {
                categoriesToSave = string.Join(",", selectedCategories);
            }

            DatraUserPreferences.SetLocalizationCategoryFilters(currentLanguageCode.ToIsoCode(), categoriesToSave);
            DatraUserPreferences.SetLocalizationStatusFilter(currentLanguageCode.ToIsoCode(), (int)currentStatusFilter);
        }

        // Language management
        private void PopulateLanguageDropdown()
        {
            if (localizationContext == null || languageDropdown == null) return;

            var languages = localizationContext.GetAvailableLanguages().ToList();
            var languageNames = languages.Select(lang => lang.ToIsoCode()).ToList();

            languageDropdown.choices = languageNames;

            if (languageNames.Count > 0)
            {
                languageDropdown.value = languageNames[0];
                currentLanguageCode = languages[0];
            }
        }

        private async Task OnLanguageChanged(string newLanguage)
        {
            if (isLoading) return;

            var languages = localizationContext.GetAvailableLanguages();
            var newLangCode = languages.FirstOrDefault(lang => lang.ToIsoCode() == newLanguage);

            if (newLangCode != default)
            {
                await SwitchLanguage(newLangCode);
            }
        }

        private async Task SwitchLanguage(LanguageCode newLanguage)
        {
            ShowLoading(true);

            try
            {
                await Task.Delay(100); // Simulate async load

                currentLanguageCode = newLanguage;
                RefreshContent();
                PopulateCategoryButtons();
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void ShowLoading(bool show)
        {
            isLoading = show;

            if (loadingOverlay == null)
            {
                loadingOverlay = new VisualElement();
                loadingOverlay.AddToClassList("loading-overlay");

                var loadingContainer = new VisualElement();
                loadingContainer.AddToClassList("loading-container");

                var loadingLabel = new Label("Loading language data...");
                loadingContainer.Add(loadingLabel);

                loadingOverlay.Add(loadingContainer);
                Add(loadingOverlay);
            }

            loadingOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Actions
        protected override void OnAddButtonClicked()
        {
            var keyId = $"NewKey_{DateTime.Now.Ticks}";

            _ = AddKeyAsync(keyId);
        }

        private async Task AddKeyAsync(string keyId)
        {
            try
            {
                await localizationContext.AddKeyAsync(keyId, "New key", "");
                changeTracker?.TrackKeyAdd(keyId);
                RefreshContent();
                MarkAsModified();
                UpdateStatus($"Key '{keyId}' added");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to add key: {e.Message}");
                UpdateStatus($"Error: {e.Message}");
            }
        }

        private async void DeleteLocalizationKey(LocalizationKeyWrapper wrapper)
        {
            if (wrapper.IsFixedKey)
            {
                EditorUtility.DisplayDialog("Cannot Delete", "This is a fixed key and cannot be deleted.", "OK");
                return;
            }

            if (EditorUtility.DisplayDialog("Delete Localization Key",
                $"Are you sure you want to delete '{wrapper.Id}'?\n\nThis will remove the key from all languages.",
                "Delete", "Cancel"))
            {
                try
                {
                    await localizationContext.DeleteKeyAsync(wrapper.Id);
                    changeTracker?.TrackKeyDelete(wrapper.Id);
                    RefreshContent();
                    MarkAsModified();
                    UpdateStatus($"Key '{wrapper.Id}' deleted");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to delete key: {e.Message}");
                    UpdateStatus($"Error: {e.Message}");
                }
            }
        }

        private void OnTextChanged(LocalizationKeyWrapper wrapper, string newValue)
        {
            wrapper.Text = newValue;
            localizationContext.SetText(wrapper.Id, newValue);
            changeTracker?.TrackTextChange(wrapper.Id, newValue);

            // Remove missing indicator if translation is added
            if (!wrapper.IsMissing)
            {
                // Rebuild to update visuals
                listView.Rebuild();
            }

            MarkAsModified();
            UpdateStatistics();
        }

        private async void AutoTranslateAll()
        {
            if (isReadOnly) return;

            var missingKeys = allItems
                .OfType<LocalizationKeyWrapper>()
                .Where(w => w.IsMissing)
                .ToList();

            if (missingKeys.Count == 0)
            {
                EditorUtility.DisplayDialog("Auto Translate", "No missing translations found.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Auto Translate",
                $"Translate {missingKeys.Count} missing keys?", "Translate", "Cancel"))
                return;

            ShowLoading(true);

            try
            {
                foreach (var wrapper in missingKeys)
                {
                    var success = await localizationContext.AutoTranslateKeyAsync(wrapper.Id, LanguageCode.En);
                    if (success)
                    {
                        var translatedText = localizationContext.GetText(wrapper.Id);
                        wrapper.Text = translatedText;
                        changeTracker?.TrackTextChange(wrapper.Id, translatedText);
                    }
                }

                RefreshContent();
                MarkAsModified();
            }
            finally
            {
                ShowLoading(false);
            }
        }

        // Helper to create header cells
        private VisualElement CreateHeaderCell(string text, float width)
        {
            var cell = new VisualElement();
            cell.AddToClassList("table-header-cell");
            cell.style.width = width;
            cell.style.minWidth = width;
            cell.style.paddingLeft = 8;
            cell.style.paddingRight = 8;
            cell.style.justifyContent = Justify.Center;

            var label = new Label(text);
            label.AddToClassList("header-cell-label");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 11;
            cell.Add(label);

            return cell;
        }

        protected override void SaveChanges()
        {
            if (localizationContext == null || isReadOnly) return;

            _ = SaveChangesAsync();
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                await localizationContext.SaveCurrentLanguageAsync();
                changeTracker?.UpdateBaseline();
                hasUnsavedChanges = false;
                UpdateModifiedState();
                UpdateStatus("Changes saved");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save: {e.Message}");
                UpdateStatus($"Save error: {e.Message}");
            }
        }

        protected override void RevertChanges()
        {
            if (localizationContext == null) return;

            changeTracker?.RevertAll();
            hasUnsavedChanges = false;
            RefreshContent();
            UpdateModifiedState();

            UpdateStatus("Changes reverted");
        }

        public override void Cleanup()
        {
            searchDebouncer?.Dispose();
            base.Cleanup();
        }
    }

    /// <summary>
    /// Translation status filter options
    /// </summary>
    public enum TranslationStatus
    {
        All = 0,
        MissingOnly = 1,
        CompleteOnly = 2
    }
}
