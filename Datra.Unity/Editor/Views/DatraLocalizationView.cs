using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Models;
using Datra.Services;
using Datra.Unity.Editor.Components;
using Datra.Unity.Editor.Models;
using Datra.Unity.Editor.Utilities;
using Datra.Editor.Interfaces;
using Datra.Editor.Models;
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
        private LanguageCode currentLanguageCode;
        private bool isLoading = false;
        private VisualElement loadingOverlay;

        // Components
        private LocalizationFilterPanel filterPanel;
        private DataStatisticsBar statisticsBar;

        // Column widths
        private const float ActionsColumnWidth = 60f;
        private const float KeyColumnWidth = 300f;
        private const float CategoryColumnWidth = 150f;

        public DatraLocalizationView() : base()
        {
            AddToClassList("datra-localization-view");
        }

        /// <summary>
        /// Override to provide localization-specific row state (modified + missing)
        /// </summary>
        protected override (bool isModified, bool isSpecial) GetRowState(object item)
        {
            if (!(item is LocalizationKeyWrapper wrapper))
                return (false, false);

            // Check if modified via change tracker
            bool isModified = false;
            if (changeTracker != null)
            {
                isModified = changeTracker.IsModified(wrapper.Id);
            }
            else
            {
                Debug.LogWarning("[GetRowState] changeTracker is null!");
            }

            // Check if missing translation
            bool isMissing = wrapper.IsMissing;

            return (isModified, isMissing);
        }

        protected override void CreateAdditionalHeaderUI()
        {
            // Filter panel
            filterPanel = new LocalizationFilterPanel();
            filterPanel.OnFilterChanged += () => {
                ApplyFilter(currentSearchTerm);
                UpdateStatistics();
            };
            headerContainer.Add(filterPanel);

            // Statistics bar
            statisticsBar = new DataStatisticsBar();
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

        public override void SetData(
            Type type,
            IDataRepository repo,
            IDataContext context,
            IRepositoryChangeTracker tracker,
            Datra.Services.LocalizationContext localizationCtx = null,
            Utilities.LocalizationChangeTracker localizationTracker = null)
        {
            dataType = type;
            repository = repo;
            dataContext = context;
            changeTracker = tracker;
            localizationContext = localizationCtx;
            localizationChangeTracker = localizationTracker;

            if (context is LocalizationContext locContext)
            {
                localizationContext = locContext;
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
            // Only set dataType if it hasn't been set via SetData() already
            // If SetData() was called with typeof(LocalizationContext), preserve that
            if (dataType == null)
            {
                dataType = typeof(LocalizationKeyWrapper);
            }

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

            // Initialize current language
            currentLanguageCode = context.CurrentLanguageCode;
            await LoadLanguageDataAsync(currentLanguageCode);
        }

        private async Task LoadLanguageDataAsync(LanguageCode languageCode)
        {
            ShowLoading(true);

            try
            {
                await localizationContext.LoadLanguageAsync(languageCode);

                // Initialize change tracker for this language (only if not already initialized)
                var localizationChangeTracker = changeTracker as LocalizationChangeTracker;
                if (!localizationChangeTracker!.IsLanguageInitialized(languageCode))
                {
                    localizationChangeTracker.InitializeLanguage(languageCode);
                }

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
                var text = localizationContext.GetText(keyId, currentLanguageCode);

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
            textCell.style.width = 600f;
            textCell.style.minWidth = 400f;
            textCell.style.paddingLeft = 8;
            textCell.style.paddingRight = 8;
            row.Add(textCell);
        }

        protected override void BindRowData(VisualElement row, object item, int index)
        {
            if (!(item is LocalizationKeyWrapper wrapper)) return;

            // Bind actions cell (delete button)
            var actionsCell = row[0];
            var deleteButton = actionsCell.Q<Button>();
            if (deleteButton != null)
            {
                deleteButton.SetEnabled(!isReadOnly && !wrapper.IsFixedKey);
                deleteButton.clicked += () => DeleteLocalizationKey(wrapper);
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
                var field = new DatraPropertyField(
                    wrapper,
                    textProperty,
                    FieldLayoutMode.Table,
                    this);

                // OnValueChanged: Handle property changes with proper tracking
                field.OnValueChanged += (propName, newValue) => {
                    // Update wrapper and localization context for current language
                    wrapper.Text = newValue as string;
                    localizationContext.SetText(wrapper.Id, newValue as string, currentLanguageCode);

                    // Track property change (not just key-level change)
                    changeTracker.TrackPropertyChange(wrapper.Id, propName, newValue, out bool isModified);
                    field.SetModified(isModified);

                    // Update UI states
                    UpdateModifiedState();
                    UpdateRowStateVisuals(wrapper);
                    MarkAsModified();
                    UpdateStatistics();
                };

                // OnRevertRequested: Revert to baseline value
                field.OnRevertRequested += (propName) => {
                    // Get baseline value from change tracker
                    var baselineValue = changeTracker.GetPropertyBaselineValue(wrapper.Id, propName);

                    // Restore to baseline for current language
                    wrapper.Text = baselineValue as string;
                    localizationContext.SetText(wrapper.Id, baselineValue as string, currentLanguageCode);

                    // Re-track with baseline value (should mark as not modified)
                    changeTracker.TrackPropertyChange(wrapper.Id, propName, baselineValue, out bool isModified);
                    field.SetModified(isModified);

                    // Update UI states
                    UpdateModifiedState();
                    UpdateRowStateVisuals(wrapper);
                };

                textCell.Add(field);

                // Initialize modified state based on property-level tracking
                if (changeTracker != null && changeTracker.IsPropertyModified(wrapper.Id, "Text"))
                {
                    field.SetModified(true);
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

            // Delegate category and status filtering to filter panel
            return filterPanel?.MatchesFilter(wrapper) ?? true;
        }

        protected override void OnFilterApplied(int filteredCount, int totalCount)
        {
            UpdateStatistics();
        }

        private void UpdateStatistics()
        {
            if (statisticsBar == null) return;

            var totalCount = allItems.Count;
            var shownCount = filteredItems.Count;
            var missingCount = allItems.Count(item => item is LocalizationKeyWrapper w && w.IsMissing);

            statisticsBar.SetStatistics(
                ("Total", totalCount, null),
                ("Shown", shownCount, null),
                ("Missing", missingCount, missingCount > 0 ? new Color(1f, 0.7f, 0.3f) : (Color?)null)
            );
        }

        // Language management - now handled by toolbar

        /// <summary>
        /// Switch to a different language (called from toolbar)
        /// </summary>
        public async Task SwitchLanguageAsync(LanguageCode newLanguage)
        {
            if (localizationContext == null) return;

            currentLanguageCode = newLanguage;

            // Load language data (includes LoadLanguageAsync, tracker init, and RefreshContent)
            await LoadLanguageDataAsync(newLanguage);

            // Update filter panel with categories from loaded data
            var categories = allItems
                .OfType<LocalizationKeyWrapper>()
                .Select(w => string.IsNullOrEmpty(w.Category) ? "(Uncategorized)" : w.Category)
                .Distinct();
            filterPanel?.SetCategories(categories);
            filterPanel?.LoadSettings(currentLanguageCode);
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
                (changeTracker as LocalizationChangeTracker)!.TrackKeyAdd(keyId);
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
                    (changeTracker as LocalizationChangeTracker)!.TrackKeyDelete(wrapper.Id);
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
                        var translatedText = localizationContext.GetText(wrapper.Id, currentLanguageCode);
                        wrapper.Text = translatedText;
                        (changeTracker as LocalizationChangeTracker)!.TrackTextChange(wrapper.Id, translatedText);
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

            // Call base.SaveChanges() to trigger OnSaveRequested event
            // This ensures localization uses the same save infrastructure as other data types
            base.SaveChanges();
        }

        protected override void OnModificationsCleared()
        {
            base.OnModificationsCleared();

            // Rebuild ListView to clear visual modifications
            listView?.Rebuild();
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
}
