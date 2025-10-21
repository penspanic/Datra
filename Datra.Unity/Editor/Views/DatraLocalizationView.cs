using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datra.Localization;
using Datra.Services;
using Datra.Unity.Editor.Components;
using Datra.Unity.Editor.Models;
using Datra.Unity.Editor.Windows;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Datra.Unity.Editor.Views
{
    /// <summary>
    /// View for editing localization data with full change tracking and revert functionality
    /// </summary>
    public class DatraLocalizationView : DatraDataView
    {
        private LocalizationContext localizationContext;
        private DropdownField languageDropdown;
        private VisualElement tableContainer;
        private VisualElement headerRow;
        private ScrollView bodyScrollView;
        private Dictionary<LocalizationKeyWrapper, Dictionary<string, VisualElement>> cellElements;
        private Dictionary<LocalizationKeyWrapper, VisualElement> rowElements;
        private VisualElement loadingOverlay;
        private bool isLoading = false;
        private LanguageCode currentLanguageCode;

        // Column widths
        private const float ActionsColumnWidth = 60f;
        private const float KeyColumnWidth = 300f;
        private const float RowHeight = 28f;

        public DatraLocalizationView() : base()
        {
            AddToClassList("datra-localization-view");
            cellElements = new Dictionary<LocalizationKeyWrapper, Dictionary<string, VisualElement>>();
            rowElements = new Dictionary<LocalizationKeyWrapper, VisualElement>();
        }

        protected override void InitializeView()
        {
            // Clear content container from base class
            contentContainer.Clear();

            // Add toolbar to header
            var toolbar = CreateToolbar();
            headerContainer.Add(toolbar);

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

        private void CreateLoadingOverlay()
        {
            loadingOverlay = new VisualElement();
            loadingOverlay.style.position = Position.Absolute;
            loadingOverlay.style.left = 0;
            loadingOverlay.style.top = 0;
            loadingOverlay.style.right = 0;
            loadingOverlay.style.bottom = 0;
            loadingOverlay.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            loadingOverlay.style.alignItems = Align.Center;
            loadingOverlay.style.justifyContent = Justify.Center;
            loadingOverlay.style.display = DisplayStyle.None;

            var loadingContainer = new VisualElement();
            loadingContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            loadingContainer.style.paddingLeft = 20;
            loadingContainer.style.paddingRight = 20;
            loadingContainer.style.paddingTop = 20;
            loadingContainer.style.paddingBottom = 20;

            var loadingLabel = new Label("Loading language data...");
            loadingLabel.style.fontSize = 14;
            loadingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
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

            // Store current modified state before clearing
            var previousModifiedCells = new HashSet<(object, string)>();
            foreach (var kvp in itemTrackers)
            {
                if (kvp.Value.HasAnyModifications())
                {
                    previousModifiedCells.Add((kvp.Key, "Text"));
                }
            }

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

                // Create tracker for this wrapper
                if (!itemTrackers.ContainsKey(wrapper))
                {
                    var tracker = new DatraPropertyTracker();
                    tracker.StartTracking(wrapper);
                    tracker.OnAnyPropertyModified += OnTrackerModified;
                    itemTrackers[wrapper] = tracker;
                }
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
            }

            bodyScrollView?.Add(bodyContainer);
        }

        private void CreateHeaderCells()
        {
            // Actions column header
            var actionsHeader = CreateHeaderCell("", ActionsColumnWidth);
            headerRow.Add(actionsHeader);

            // Key column header
            var keyHeader = CreateHeaderCell("Key", KeyColumnWidth);
            headerRow.Add(keyHeader);

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

            // Add missing locale indicator
            if (wrapper.IsMissing)
            {
                row.AddToClassList("missing-locale-row");
            }

            var cells = new Dictionary<string, VisualElement>();
            cellElements[wrapper] = cells;

            // 1. Actions column
            var actionsCell = CreateActionsCell(wrapper);
            row.Add(actionsCell);

            // 2. Key column (read-only)
            var keyCell = CreateKeyCell(wrapper);
            cells["Id"] = keyCell;
            row.Add(keyCell);

            // 3. Text column (editable with DatraPropertyField)
            var textProperty = typeof(LocalizationKeyWrapper).GetProperty("Text");
            if (textProperty != null && itemTrackers.ContainsKey(wrapper))
            {
                var textField = new DatraPropertyField(
                    wrapper,
                    textProperty,
                    itemTrackers[wrapper],
                    DatraFieldLayoutMode.Table
                );

                textField.OnValueChanged += (propName, newValue) => {
                    // Update LocalizationContext
                    localizationContext.SetText(wrapper.Id, newValue as string);
                    MarkAsModified();

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

                    // Get the tracker for this wrapper and track the change
                    if (itemTrackers.TryGetValue(wrapper, out var tracker))
                    {
                        // Force the tracker to recognize this as a modification
                        tracker.TrackChange(wrapper, "Text", translatedText);
                    }

                    // Mark as modified
                    MarkAsModified();

                    // Update the specific row visually
                    if (rowElements.TryGetValue(wrapper, out var row))
                    {
                        // Remove missing-locale-row class if it exists
                        row.RemoveFromClassList("missing-locale-row");

                        // Update the text field cell
                        if (cellElements.TryGetValue(wrapper, out var cells))
                        {
                            if (cells.TryGetValue("Text", out var textCell))
                            {
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

                    // Remove from tracking
                    if (itemTrackers.ContainsKey(wrapper))
                    {
                        itemTrackers[wrapper].OnAnyPropertyModified -= OnTrackerModified;
                        itemTrackers.Remove(wrapper);
                    }

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

                bool matches = string.IsNullOrEmpty(searchTerm);
                if (!matches)
                {
                    // Search in key, text, description, category
                    matches = wrapper.Id.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                              wrapper.Text.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                              (wrapper.Description?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                              (wrapper.Category?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
                }

                row.style.display = matches ? DisplayStyle.Flex : DisplayStyle.None;
            }
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
                // Save all modified text values to LocalizationContext
                foreach (var kvp in itemTrackers)
                {
                    var wrapper = kvp.Key as LocalizationKeyWrapper;
                    if (wrapper != null && kvp.Value.IsPropertyModified(wrapper, "Text"))
                    {
                        localizationContext.SetText(wrapper.Id, wrapper.Text);
                    }
                }

                // Save to file
                var saveTask = localizationContext.SaveCurrentLanguageAsync();
                saveTask.Wait();

                // Update baselines
                propertyTracker.UpdateBaseline();
                foreach (var tracker in itemTrackers.Values)
                {
                    tracker.UpdateBaseline();
                }

                hasUnsavedChanges = false;
                UpdateFooter();

                // Clear visual modifications from all cells
                foreach (var (wrapper, cells) in cellElements)
                {
                    foreach (var (property, cell) in cells)
                    {
                        cell.RemoveFromClassList("modified-cell");
                    }
                }

                // Clear row backgrounds
                foreach (var row in rowElements.Values)
                {
                    row.style.backgroundColor = Color.clear;
                }

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
            // Base handles tracker revert and UI refresh
            base.RevertChanges();

            // Additional: reload data from LocalizationContext
            foreach (var kvp in itemTrackers)
            {
                var wrapper = kvp.Key as LocalizationKeyWrapper;
                if (wrapper != null)
                {
                    wrapper.Text = localizationContext.GetText(wrapper.Id);
                }
            }

            // Clear visual modifications from all cells
            foreach (var (wrapper, cells) in cellElements)
            {
                foreach (var (property, cell) in cells)
                {
                    cell.RemoveFromClassList("modified-cell");
                }
            }

            // Clear row backgrounds
            foreach (var row in rowElements.Values)
            {
                row.style.backgroundColor = Color.clear;
            }

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
    }
}
