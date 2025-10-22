using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datra.Localization;
using Datra.Services;
using Datra.Unity.Editor.Components;
using Datra.Unity.Editor.Models;
using Datra.Unity.Editor.Utilities;
using Datra.Unity.Editor.Windows;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Datra.Unity.Editor.Views
{
    /// <summary>
    /// Translation status filter options
    /// </summary>
    public enum TranslationStatus
    {
        All = 0,
        MissingOnly = 1,
        CompleteOnly = 2
    }

    /// <summary>
    /// View for editing localization data with full change tracking and revert functionality
    /// </summary>
    public class DatraLocalizationView : DatraDataView
    {
        private LocalizationContext localizationContext;
        private new LocalizationChangeTracker changeTracker;  // External change tracker (hides base class field)
        private DropdownField languageDropdown;
        private VisualElement tableContainer;
        private VisualElement headerRow;
        private ScrollView bodyScrollView;
        private Dictionary<LocalizationKeyWrapper, Dictionary<string, VisualElement>> cellElements;
        private Dictionary<LocalizationKeyWrapper, VisualElement> rowElements;
        private VisualElement loadingOverlay;
        private bool isLoading = false;
        private LanguageCode currentLanguageCode;

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
        private const float RowHeight = 28f;

        public DatraLocalizationView() : base()
        {
            AddToClassList("datra-localization-view");
            cellElements = new Dictionary<LocalizationKeyWrapper, Dictionary<string, VisualElement>>();
            rowElements = new Dictionary<LocalizationKeyWrapper, VisualElement>();
        }

        /// <summary>
        /// Override to check modifications from external LocalizationChangeTracker
        /// </summary>
        protected override bool HasActualModifications()
        {
            return changeTracker?.HasModifications() ?? false;
        }

        protected override void InitializeView()
        {
            // Clear content container from base class
            contentContainer.Clear();

            // Add toolbar to header
            var toolbar = CreateToolbar();
            headerContainer.Add(toolbar);

            // Create category filter bar
            categoryFilterBar = CreateCategoryFilterBar();
            headerContainer.Add(categoryFilterBar);

            // Create status filter bar
            statusFilterBar = CreateStatusFilterBar();
            headerContainer.Add(statusFilterBar);

            // Create statistics bar
            var statisticsBar = CreateStatisticsBar();
            headerContainer.Add(statisticsBar);

            // Create main table container
            tableContainer = new VisualElement();
            tableContainer.AddToClassList("table-container");
            tableContainer.style.flexGrow = 1;
            tableContainer.style.flexDirection = FlexDirection.Column;

            // Create header container
            var tableHeaderContainer = new VisualElement();
            tableHeaderContainer.style.height = RowHeight;
            tableHeaderContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            tableHeaderContainer.style.borderBottomWidth = 1;
            tableHeaderContainer.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            tableHeaderContainer.style.overflow = Overflow.Hidden;

            headerRow = new VisualElement();
            headerRow.AddToClassList("table-header-row");
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.height = RowHeight;
            tableHeaderContainer.Add(headerRow);

            tableContainer.Add(tableHeaderContainer);

            // Create scroll view for body
            bodyScrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            bodyScrollView.name = "table-body";
            bodyScrollView.AddToClassList("table-body-scroll");
            bodyScrollView.style.flexGrow = 1;

            // Sync horizontal scroll with header
            bodyScrollView.horizontalScroller.valueChanged += (value) => {
                headerRow.style.left = -value;
            };

            tableContainer.Add(bodyScrollView);
            contentContainer.Add(tableContainer);

            // Create loading overlay
            CreateLoadingOverlay();
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("localization-toolbar");
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 36;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);

            // Language selector
            var languageLabel = new Label("Language:");
            languageLabel.style.marginRight = 8;
            toolbar.Add(languageLabel);

            languageDropdown = new DropdownField();
            languageDropdown.style.width = 150;
            languageDropdown.style.marginRight = 20;
            languageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
            toolbar.Add(languageDropdown);

            // Add key button
            var addButton = new Button(() => {
                if (!isReadOnly)
                    AddNewLocalizationKey();
            });
            addButton.text = "➕ Add Key";
            addButton.AddToClassList("table-add-button");
            addButton.style.marginRight = 8;
            toolbar.Add(addButton);

            // Search field
            searchField = new ToolbarSearchField();
            searchField.AddToClassList("table-search");
            searchField.style.flexGrow = 1;
            (searchField as ToolbarSearchField).RegisterValueChangedCallback(evt => FilterItems(evt.newValue));
            toolbar.Add(searchField);

            return toolbar;
        }

        private VisualElement CreateCategoryFilterBar()
        {
            var filterBar = new VisualElement();
            filterBar.AddToClassList("category-filter-bar");

            var categoryLabel = new Label("Categories:");
            filterBar.Add(categoryLabel);

            // Category buttons container (will be populated when data is loaded)
            var categoryButtonsContainer = new VisualElement();
            categoryButtonsContainer.name = "category-buttons-container";
            filterBar.Add(categoryButtonsContainer);

            // Select All / Clear All buttons
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

            // All button
            var allButton = new Button(() => SetStatusFilter(TranslationStatus.All));
            allButton.text = "All";
            allButton.name = "status-all-button";
            allButton.AddToClassList("status-filter-button");
            allButton.AddToClassList("active"); // Default active
            filterBar.Add(allButton);

            // Missing Only button
            var missingButton = new Button(() => SetStatusFilter(TranslationStatus.MissingOnly));
            missingButton.text = "Missing Only";
            missingButton.name = "status-missing-button";
            missingButton.AddToClassList("status-filter-button");
            filterBar.Add(missingButton);

            // Complete button
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

        private void CreateLoadingOverlay()
        {
            loadingOverlay = new VisualElement();
            loadingOverlay.AddToClassList("loading-overlay");
            loadingOverlay.style.display = DisplayStyle.None;

            var loadingContainer = new VisualElement();
            loadingContainer.AddToClassList("loading-container");

            var loadingLabel = new Label("Loading language data...");
            loadingContainer.Add(loadingLabel);

            loadingOverlay.Add(loadingContainer);
            Add(loadingOverlay);
        }

        private void ShowLoading(bool show)
        {
            isLoading = show;
            if (loadingOverlay != null)
            {
                loadingOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Disable/enable interactive elements
            if (languageDropdown != null)
                languageDropdown.SetEnabled(!show);
            if (searchField != null)
                searchField.SetEnabled(!show);
        }

        /// <summary>
        /// Set change tracker (must be called before or with SetLocalizationContext)
        /// </summary>
        public void SetChangeTracker(LocalizationChangeTracker tracker)
        {
            changeTracker = tracker;
        }

        public async void SetLocalizationContext(LocalizationContext context)
        {
            localizationContext = context;

            if (context == null)
            {
                headerContainer.SetEnabled(false);
                contentContainer.SetEnabled(false);
                return;
            }

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

        private async void OnLanguageChanged(ChangeEvent<string> evt)
        {
            if (isLoading) return;

            var languageCode = LanguageCodeExtensions.TryParse(evt.newValue);
            if (languageCode.HasValue)
            {
                // Check for unsaved changes
                if (hasUnsavedChanges)
                {
                    if (!EditorUtility.DisplayDialog("Unsaved Changes",
                        "You have unsaved changes. Do you want to discard them and switch language?",
                        "Discard", "Cancel"))
                    {
                        languageDropdown.SetValueWithoutNotify(evt.previousValue);
                        return;
                    }
                }

                currentLanguageCode = languageCode.Value;
                await LoadLanguageDataAsync(languageCode.Value);
            }
            else
            {
                Debug.LogError($"Failed to parse language code: {evt.newValue}");
            }
        }

        private async Task LoadLanguageDataAsync(LanguageCode languageCode)
        {
            ShowLoading(true);

            try
            {
                await localizationContext.LoadLanguageAsync(languageCode);

                // Initialize change tracker for this language (only if not already initialized)
                // This prevents clearing existing change tracking when switching tabs
                if (changeTracker != null && !changeTracker.IsLanguageInitialized(languageCode))
                {
                    changeTracker.InitializeLanguage(languageCode);
                }

                RefreshContent();
                UpdateStatus($"Loaded language: {languageCode.GetDisplayName()}");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to load language data: {e.Message}", "OK");
                Debug.LogError($"Failed to load language: {e}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        public override void RefreshContent()
        {
            if (tableContainer == null || localizationContext == null) return;

            // Clear body content (keep header intact)
            bodyScrollView?.Clear();
            headerRow?.Clear();
            cellElements.Clear();
            rowElements.Clear();

            // Get all keys and wrap them
            var keys = localizationContext.GetAllKeys().ToList();
            var wrappers = new List<LocalizationKeyWrapper>();

            foreach (var key in keys)
            {
                var text = localizationContext.GetText(key);

                // Remove [Missing: prefix for display (we'll show it visually instead)
                if (!string.IsNullOrEmpty(text) && text.StartsWith("[Missing:"))
                {
                    text = ""; // Show as empty instead of [Missing: Key]
                }

                var keyData = localizationContext.GetKeyData(key);
                var wrapper = new LocalizationKeyWrapper(key, text, keyData);
                wrappers.Add(wrapper);
            }

            // Create header cells
            CreateHeaderCells();

            // Create body container
            var bodyContainer = new VisualElement();
            bodyContainer.AddToClassList("table-body-container");
            bodyContainer.style.flexDirection = FlexDirection.Column;

            // Create data rows
            foreach (var wrapper in wrappers)
            {
                CreateDataRow(wrapper, bodyContainer);

                // Restore modification indicators from ChangeTracker
                if (changeTracker != null && changeTracker.IsModified(wrapper.Id))
                {
                    if (cellElements.TryGetValue(wrapper, out var cells))
                    {
                        if (cells.TryGetValue("Text", out var textCell))
                        {
                            // Set modified state on the field
                            if (textCell is DatraPropertyField field)
                            {
                                field.SetModified(true);
                            }

                            // Add visual indicator
                            textCell.AddToClassList("modified-cell");
                        }
                    }
                }
            }

            bodyScrollView?.Add(bodyContainer);

            // Populate category filter buttons
            PopulateCategoryButtons();

            // Update statistics
            UpdateStatistics();

            // Update modification state after restoring (to show orange dot if there are modifications)
            UpdateModifiedState();
        }

        private void CreateHeaderCells()
        {
            // Actions column header
            var actionsHeader = CreateHeaderCell("", ActionsColumnWidth);
            headerRow.Add(actionsHeader);

            // Key column header
            var keyHeader = CreateHeaderCell("Key", KeyColumnWidth);
            headerRow.Add(keyHeader);

            // Category column header
            var categoryHeader = CreateHeaderCell("Category", CategoryColumnWidth);
            headerRow.Add(categoryHeader);

            // Text column header
            var textHeader = CreateHeaderCell("Text", 400f);
            textHeader.style.flexGrow = 1;
            headerRow.Add(textHeader);
        }

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
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 11;
            cell.Add(label);

            return cell;
        }

        private void CreateDataRow(LocalizationKeyWrapper wrapper, VisualElement container)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = RowHeight;
            row.style.alignItems = Align.Center;

            var cells = new Dictionary<string, VisualElement>();
            cellElements[wrapper] = cells;

            // 1. Actions column
            var actionsCell = CreateActionsCell(wrapper);

            // Add missing locale indicator to the first cell instead of the row
            if (wrapper.IsMissing)
            {
                row.AddToClassList("missing-locale-row");
                actionsCell.AddToClassList("missing-locale-cell");
            }

            row.Add(actionsCell);

            // 2. Key column (read-only)
            var keyCell = CreateKeyCell(wrapper);
            cells["Id"] = keyCell;
            row.Add(keyCell);

            // 3. Category column (read-only)
            var categoryCell = CreateCategoryCell(wrapper);
            cells["Category"] = categoryCell;
            row.Add(categoryCell);

            // 4. Text column (editable with DatraPropertyField)
            var textProperty = typeof(LocalizationKeyWrapper).GetProperty("Text");
            if (textProperty != null)
            {
                var textField = new DatraPropertyField(
                    wrapper,
                    textProperty,
                    DatraFieldLayoutMode.Table
                );

                textField.OnValueChanged += (propName, newValue) => {
                    // Update LocalizationContext
                    localizationContext.SetText(wrapper.Id, newValue as string);

                    // Track change in external tracker
                    changeTracker?.TrackTextChange(wrapper.Id, newValue as string);

                    MarkAsModified();

                    // Update field's modified state
                    textField.SetModified(changeTracker?.IsModified(wrapper.Id) ?? false);

                    // Add visual indicator to the cell
                    if (cells.TryGetValue("Text", out var textCell))
                    {
                        textCell.AddToClassList("modified-cell");

                        // Also update row background
                        if (rowElements.TryGetValue(wrapper, out var modifiedRow))
                        {
                            modifiedRow.style.backgroundColor = new Color(0.4f, 0.3f, 0.2f, 0.3f);
                        }
                    }
                };

                textField.OnRevertRequested += (propName) => {
                    // Revert using external tracker
                    if (changeTracker != null)
                    {
                        var baselineText = changeTracker.GetBaselineText(wrapper.Id);
                        if (baselineText != null)
                        {
                            // Update LocalizationContext
                            localizationContext.SetText(wrapper.Id, baselineText);

                            // Update wrapper
                            wrapper.Text = baselineText;

                            // Update the TextField value
                            var textFieldElement = textField.Q<TextField>();
                            if (textFieldElement != null)
                            {
                                textFieldElement.SetValueWithoutNotify(baselineText);
                            }

                            // Update field's modified state
                            textField.SetModified(false);

                            // Clear visual indicator
                            if (cells.TryGetValue("Text", out var textCell))
                            {
                                textCell.RemoveFromClassList("modified-cell");
                            }

                            // Update modification state (fires OnDataModified with correct state)
                            UpdateModifiedState();
                        }
                    }
                };

                textField.style.flexGrow = 1;
                cells["Text"] = textField;
                row.Add(textField);
            }

            // Row hover effect
            row.RegisterCallback<MouseEnterEvent>(evt => {
                if (!row.ClassListContains("selected") && !row.ClassListContains("missing-locale-row"))
                    row.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.2f);
            });

            row.RegisterCallback<MouseLeaveEvent>(evt => {
                if (!row.ClassListContains("selected") && !row.ClassListContains("missing-locale-row"))
                    row.style.backgroundColor = Color.clear;
            });

            // Right-click context menu
            row.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button == 1) // Right click
                {
                    evt.StopPropagation();
                    ShowRowContextMenu(wrapper);
                }
            });

            container.Add(row);
            rowElements[wrapper] = row;
        }

        private void ShowRowContextMenu(LocalizationKeyWrapper wrapper)
        {
            var menu = new GenericMenu();

            // Auto Translate option (only show for missing or empty translations)
            if (wrapper.IsMissing)
            {
                menu.AddItem(new GUIContent("Auto Translate from English"), false, () => {
                    AutoTranslateKey(wrapper, LanguageCode.En);
                });
                menu.AddSeparator("");
            }

            // Copy Key
            menu.AddItem(new GUIContent("Copy Key"), false, () => {
                EditorGUIUtility.systemCopyBuffer = wrapper.Id;
            });

            // Copy Text
            if (!string.IsNullOrEmpty(wrapper.Text))
            {
                menu.AddItem(new GUIContent("Copy Text"), false, () => {
                    EditorGUIUtility.systemCopyBuffer = wrapper.Text;
                });
            }

            menu.ShowAsContext();
        }

        private async void AutoTranslateKey(LocalizationKeyWrapper wrapper, LanguageCode sourceLanguage)
        {
            if (localizationContext == null || wrapper == null) return;

            try
            {
                UpdateStatus($"Translating '{wrapper.Id}'...");

                // Use the localization context's auto-translate method
                var success = await localizationContext.AutoTranslateKeyAsync(wrapper.Id, sourceLanguage);

                if (success)
                {
                    // Update the wrapper with the new translated text
                    var translatedText = localizationContext.GetText(wrapper.Id);
                    wrapper.Text = translatedText;

                    // Track change in external tracker
                    changeTracker?.TrackTextChange(wrapper.Id, translatedText);

                    // Mark as modified
                    MarkAsModified();

                    // Update the specific row visually
                    if (rowElements.TryGetValue(wrapper, out var row))
                    {
                        // Remove missing-locale-row class if it exists
                        row.RemoveFromClassList("missing-locale-row");

                        // Remove missing-locale-cell class from actions cell (first child)
                        if (row.childCount > 0)
                        {
                            row[0].RemoveFromClassList("missing-locale-cell");
                        }

                        // Update the text field cell
                        if (cellElements.TryGetValue(wrapper, out var cells))
                        {
                            if (cells.TryGetValue("Text", out var textCell))
                            {
                                // Update DatraPropertyField's modified state
                                if (textCell is DatraPropertyField field)
                                {
                                    field.SetModified(true);
                                }

                                // Find the TextField and update its value
                                var textField = textCell.Q<TextField>();
                                if (textField != null)
                                {
                                    textField.SetValueWithoutNotify(translatedText);
                                }
                            }
                        }
                    }

                    UpdateStatus($"'{wrapper.Id}' translated successfully");
                }
                else
                {
                    UpdateStatus($"Translation failed: Source text not found for '{wrapper.Id}'");
                    EditorUtility.DisplayDialog("Translation Failed",
                        $"Could not find source text in {sourceLanguage.GetDisplayName()} for key '{wrapper.Id}'.\n\n" +
                        "Make sure the key exists in the source language.",
                        "OK");
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Translation error: {e.Message}");
                EditorUtility.DisplayDialog("Translation Error",
                    $"Failed to translate '{wrapper.Id}':\n\n{e.Message}",
                    "OK");
                Debug.LogError($"Auto-translate failed: {e}");
            }
        }

        private VisualElement CreateActionsCell(LocalizationKeyWrapper wrapper)
        {
            var cell = new VisualElement();
            cell.AddToClassList("table-cell");
            cell.style.width = ActionsColumnWidth;
            cell.style.minWidth = ActionsColumnWidth;
            cell.style.justifyContent = Justify.Center;
            cell.style.alignItems = Align.Center;

            if (wrapper.IsFixedKey)
            {
                // Fixed key: show lock icon
                var lockIcon = new Label("🔒");
                lockIcon.tooltip = "Fixed key - cannot be deleted";
                lockIcon.style.fontSize = 14;
                lockIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
                cell.Add(lockIcon);
            }
            else
            {
                // Regular key: show delete button
                var deleteButton = new Button(() => {
                    if (!isReadOnly)
                        DeleteLocalizationKey(wrapper);
                });
                deleteButton.text = "🗑️";
                deleteButton.tooltip = "Delete this key";
                deleteButton.AddToClassList("table-delete-button");
                deleteButton.style.width = 30;
                deleteButton.style.height = 20;
                deleteButton.SetEnabled(!isReadOnly);
                cell.Add(deleteButton);
            }

            return cell;
        }

        private VisualElement CreateKeyCell(LocalizationKeyWrapper wrapper)
        {
            var cell = new VisualElement();
            cell.AddToClassList("table-cell");
            cell.style.width = KeyColumnWidth;
            cell.style.minWidth = KeyColumnWidth;
            cell.style.paddingLeft = 8;
            cell.style.paddingRight = 8;

            var label = new Label(wrapper.Id);
            label.style.fontSize = 11;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.tooltip = $"{wrapper.Id}\n{wrapper.Description}";

            // Style fixed keys differently
            if (wrapper.IsFixedKey)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Italic;
                label.style.color = new Color(0.7f, 0.8f, 0.9f);
            }

            cell.Add(label);
            return cell;
        }

        private VisualElement CreateCategoryCell(LocalizationKeyWrapper wrapper)
        {
            var cell = new VisualElement();
            cell.AddToClassList("table-cell");
            cell.style.width = CategoryColumnWidth;
            cell.style.minWidth = CategoryColumnWidth;
            cell.style.paddingLeft = 8;
            cell.style.paddingRight = 8;

            var categoryText = string.IsNullOrEmpty(wrapper.Category) ? "(Uncategorized)" : wrapper.Category;
            var label = new Label(categoryText);
            label.style.fontSize = 11;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.tooltip = categoryText;

            // Style uncategorized differently
            if (string.IsNullOrEmpty(wrapper.Category))
            {
                label.style.unityFontStyleAndWeight = FontStyle.Italic;
                label.style.color = new Color(0.6f, 0.6f, 0.6f);
            }

            cell.Add(label);
            return cell;
        }

        private async void DeleteLocalizationKey(LocalizationKeyWrapper wrapper)
        {
            if (EditorUtility.DisplayDialog(
                "Delete Localization Key",
                $"Are you sure you want to delete '{wrapper.Id}'?\n\n" +
                $"This will remove the key from all languages.",
                "Delete", "Cancel"))
            {
                try
                {
                    await localizationContext.DeleteKeyAsync(wrapper.Id);

                    // Track deletion in external tracker
                    changeTracker?.TrackKeyDelete(wrapper.Id);

                    RefreshContent();
                    UpdateStatus($"Key '{wrapper.Id}' deleted successfully");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Error",
                        $"Failed to delete key: {e.Message}", "OK");
                    Debug.LogError($"Failed to delete key: {e}");
                }
            }
        }

        private void AddNewLocalizationKey()
        {
            DatraInputDialog.Show("New Localization Key",
                "Enter the key name:",
                "New_Key",
                async (input) => {
                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        try
                        {
                            await localizationContext.AddKeyAsync(input);

                            // Track addition in external tracker
                            changeTracker?.TrackKeyAdd(input);

                            RefreshContent();
                            UpdateStatus($"Key '{input}' added successfully");
                            MarkAsModified();
                        }
                        catch (Exception e)
                        {
                            EditorUtility.DisplayDialog("Error",
                                $"Failed to add key: {e.Message}", "OK");
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Key", "Key cannot be empty", "OK");
                    }
                });
        }

        protected override void FilterItems(string searchTerm)
        {
            if (rowElements == null) return;

            foreach (var kvp in rowElements)
            {
                var wrapper = kvp.Key;
                var row = kvp.Value;

                bool matches = true;

                // 1. Search term filter
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    matches = wrapper.Id.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                              wrapper.Text.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                              (wrapper.Description?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                              (wrapper.Category?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
                }

                // 2. Category filter
                if (matches && selectedCategories != null && selectedCategories.Count > 0)
                {
                    var category = string.IsNullOrEmpty(wrapper.Category) ? "(Uncategorized)" : wrapper.Category;
                    matches = selectedCategories.Contains(category);
                }

                // 3. Status filter
                if (matches && currentStatusFilter != TranslationStatus.All)
                {
                    bool isMissing = wrapper.IsMissing;

                    matches = currentStatusFilter switch
                    {
                        TranslationStatus.MissingOnly => isMissing,
                        TranslationStatus.CompleteOnly => !isMissing,
                        _ => true
                    };
                }

                row.style.display = matches ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Update statistics after filtering
            UpdateStatistics();
        }

        protected override void SaveChanges()
        {
            if (localizationContext == null || isReadOnly) return;

            PerformSave();
        }

        private void PerformSave()
        {
            try
            {
                // All modified values are already in LocalizationContext
                // (tracked by changeTracker, updated in real-time)

                // Save to file
                var saveTask = localizationContext.SaveCurrentLanguageAsync();
                saveTask.Wait();

                // Update external change tracker baseline
                changeTracker?.UpdateBaseline();

                // Clear visual modifications from all cells
                foreach (var (wrapper, cells) in cellElements)
                {
                    foreach (var (property, cell) in cells)
                    {
                        // Clear modified state on fields
                        if (cell is DatraPropertyField field)
                        {
                            field.SetModified(false);
                        }

                        cell.RemoveFromClassList("modified-cell");
                    }
                }

                // Clear row backgrounds
                foreach (var row in rowElements.Values)
                {
                    row.style.backgroundColor = Color.clear;
                }

                // Update modification state (should be false after save)
                UpdateModifiedState();

                AssetDatabase.Refresh();
                UpdateStatus($"Localization saved for '{languageDropdown.value}'");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to save: {e.Message}", "OK");
                Debug.LogError($"Failed to save: {e}");
            }
        }

        protected override void RevertChanges()
        {
            if (localizationContext == null || changeTracker == null) return;

            // Revert using external change tracker (applies baseline to LocalizationContext)
            changeTracker.RevertAll();

            // Base handles tracker revert
            base.RevertChanges();

            // Refresh entire UI to reflect reverted values
            // This recreates all TextFields with the baseline values from LocalizationContext
            RefreshContent();

            UpdateStatus("Changes reverted");
        }

        protected override void UpdateEditability()
        {
            base.UpdateEditability();

            // Update add button
            var addButton = headerContainer.Q<Button>(className: "table-add-button");
            addButton?.SetEnabled(!isReadOnly);

            // Update delete buttons in rows
            var deleteButtons = bodyScrollView?.Query<Button>(className: "table-delete-button").ToList();
            if (deleteButtons != null)
            {
                foreach (var button in deleteButtons)
                {
                    button.SetEnabled(!isReadOnly);
                }
            }
        }

        #region Filter Methods

        private void PopulateCategoryButtons()
        {
            var container = categoryFilterBar?.Q("category-buttons-container");
            if (container == null) return;

            container.Clear();

            // Collect all unique categories from wrappers
            availableCategories = new HashSet<string>();
            if (rowElements != null)
            {
                foreach (var wrapper in rowElements.Keys)
                {
                    var category = string.IsNullOrEmpty(wrapper.Category) ? "(Uncategorized)" : wrapper.Category;
                    availableCategories.Add(category);
                }
            }

            // Load saved filter settings
            LoadFilterSettings();

            // Create toggle button for each category
            foreach (var category in availableCategories.OrderBy(c => c))
            {
                var isSelected = selectedCategories == null || selectedCategories.Contains(category);
                var button = CreateCategoryToggleButton(category, isSelected);
                container.Add(button);
            }
        }

        private Button CreateCategoryToggleButton(string category, bool isSelected)
        {
            var button = new Button(() => ToggleCategory(category));
            button.text = category;
            button.AddToClassList("category-toggle-button");

            if (isSelected)
            {
                button.AddToClassList("active");
            }

            return button;
        }

        private void ToggleCategory(string category)
        {
            if (selectedCategories == null)
            {
                // If null (all selected), initialize with all categories except the clicked one
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

            // If all categories are selected, set to null (means "all")
            if (selectedCategories.Count == availableCategories.Count)
            {
                selectedCategories = null;
            }

            // Update button visuals
            UpdateCategoryButtonVisuals();

            // Apply filter
            var currentSearchTerm = (searchField as ToolbarSearchField)?.value ?? "";
            FilterItems(currentSearchTerm);

            // Save settings
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
                {
                    button.AddToClassList("active");
                }
                else
                {
                    button.RemoveFromClassList("active");
                }
            }
        }

        private void SelectAllCategories()
        {
            selectedCategories = null; // null means all selected
            UpdateCategoryButtonVisuals();
            var currentSearchTerm = (searchField as ToolbarSearchField)?.value ?? "";
            FilterItems(currentSearchTerm);
            SaveFilterSettings();
        }

        private void ClearAllCategories()
        {
            selectedCategories = new HashSet<string>(); // Empty set means none selected
            UpdateCategoryButtonVisuals();
            var currentSearchTerm = (searchField as ToolbarSearchField)?.value ?? "";
            FilterItems(currentSearchTerm);
            SaveFilterSettings();
        }

        private void SetStatusFilter(TranslationStatus status)
        {
            currentStatusFilter = status;

            // Update button visuals
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

            // Apply filter
            var currentSearchTerm = (searchField as ToolbarSearchField)?.value ?? "";
            FilterItems(currentSearchTerm);

            // Save settings
            SaveFilterSettings();
        }

        private void UpdateStatistics()
        {
            if (rowElements == null || statisticsLabel == null) return;

            var totalCount = rowElements.Count;
            var shownCount = rowElements.Count(kvp => kvp.Value.style.display == DisplayStyle.Flex);
            var missingCount = rowElements.Count(kvp => kvp.Key.IsMissing);

            statisticsLabel.text = $"Total: {totalCount:N0} | Shown: {shownCount:N0} | Missing: {missingCount:N0}";

            // Highlight missing count if there are missing translations
            if (missingCount > 0)
            {
                statisticsLabel.style.color = new Color(1f, 0.7f, 0.3f); // Orange
            }
            else
            {
                statisticsLabel.style.color = Color.white;
            }
        }

        private void LoadFilterSettings()
        {
            if (currentLanguageCode == default) return;

            // Load category filter
            var savedCategories = DatraUserPreferences.GetLocalizationCategoryFilters(currentLanguageCode.ToIsoCode());
            if (string.IsNullOrEmpty(savedCategories))
            {
                selectedCategories = null; // All selected
            }
            else
            {
                selectedCategories = new HashSet<string>(savedCategories.Split(','));
            }

            // Load status filter
            var savedStatus = DatraUserPreferences.GetLocalizationStatusFilter(currentLanguageCode.ToIsoCode());
            currentStatusFilter = (TranslationStatus)savedStatus;

            // Update status button visuals
            SetStatusFilter(currentStatusFilter);
        }

        private void SaveFilterSettings()
        {
            if (currentLanguageCode == default) return;

            // Save category filter
            string categoriesToSave = null;
            if (selectedCategories != null && selectedCategories.Count > 0 && selectedCategories.Count < availableCategories?.Count)
            {
                categoriesToSave = string.Join(",", selectedCategories);
            }
            DatraUserPreferences.SetLocalizationCategoryFilters(currentLanguageCode.ToIsoCode(), categoriesToSave);

            // Save status filter
            DatraUserPreferences.SetLocalizationStatusFilter(currentLanguageCode.ToIsoCode(), (int)currentStatusFilter);
        }

        #endregion
    }
}
