using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.Unity.Editor.Components;
using Datra.DataTypes;
using Datra.Unity.Editor.Utilities;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;

namespace Datra.Unity.Editor.Views
{
    public class DatraTableView : DatraDataView
    {
        private VisualElement tableContainer;
        private VisualElement headerRow;
        private List<PropertyInfo> columns;
        private List<object> items;
        private Dictionary<object, Dictionary<string, VisualElement>> cellElements;
        private Dictionary<object, VisualElement> rowElements;
        private ScrollView bodyScrollView;

        // Properties
        public bool ShowIdColumn { get; set; } = true;
        public bool ShowActionsColumn { get; set; } = true;
        public float RowHeight { get; set; } = 28f;
        
        // Column resize tracking
        private bool isResizing = false;
        private VisualElement resizingColumn;
        private float resizeStartX;
        private float resizeStartWidth;
        
        public DatraTableView() : base()
        {
            AddToClassList("datra-table-view");
            cellElements = new Dictionary<object, Dictionary<string, VisualElement>>();
            rowElements = new Dictionary<object, VisualElement>();
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

            // Create header container
            var tableHeaderContainer = new VisualElement();
            tableHeaderContainer.AddToClassList("table-header-container");

            headerRow = new VisualElement();
            headerRow.AddToClassList("table-header-row");
            tableHeaderContainer.Add(headerRow);

            tableContainer.Add(tableHeaderContainer);

            // Create 2D scroll view for body
            bodyScrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            bodyScrollView.name = "table-body";
            bodyScrollView.AddToClassList("table-body-scroll");
            
            // Sync horizontal scroll with header
            bodyScrollView.horizontalScroller.valueChanged += (value) => {
                headerRow.style.left = -value;
            };
            
            tableContainer.Add(bodyScrollView);
            contentContainer.Add(tableContainer);
        }
        
        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("table-toolbar");

            // Add button
            var addButton = new Button(() => {
                if (!isReadOnly)
                    AddNewItem();
            });
            addButton.text = "➕ Add Row";
            addButton.AddToClassList("table-add-button");
            addButton.SetEnabled(!isReadOnly);
            toolbar.Add(addButton);

            // Search field
            searchField = new ToolbarSearchField();
            searchField.AddToClassList("table-search");
            (searchField as ToolbarSearchField).RegisterValueChangedCallback(evt => FilterRows(evt.newValue));
            toolbar.Add(searchField);

            // View options
            var optionsButton = new Button(() => ShowViewOptions());
            optionsButton.text = "⚙";
            optionsButton.tooltip = "View Options";
            optionsButton.AddToClassList("table-options-button");
            toolbar.Add(optionsButton);

            return toolbar;
        }
        
        public override void SetData(Type type, object repo, object context, IRepositoryChangeTracker tracker)
        {
            // Only reset modification state if switching to a different data type
            bool isDifferentType = dataType != type;

            // Don't call base.SetData - we need to initialize columns first
            dataType = type;
            repository = repo;
            dataContext = context;
            changeTracker = tracker;

            if (isDifferentType)
            {
                hasUnsavedChanges = false;
            }

            // Get columns (properties) BEFORE calling RefreshContent
            // Filter out properties with DatraIgnore attribute
            columns = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && !p.GetCustomAttributes(typeof(Datra.Attributes.DatraIgnoreAttribute), true).Any())
                .ToList();

            // Now cleanup and refresh
            CleanupFields();
            RefreshContent();
            UpdateFooter();
        }
        
        public override void RefreshContent()
        {
            if (tableContainer == null) return;

            // Clear body content (keep header intact)
            bodyScrollView?.Clear();
            headerRow?.Clear();
            cellElements.Clear();
            rowElements.Clear();

            if (dataType == null || repository == null) return;

            // Get items from repository
            if (IsTableData(dataType))
            {
                var getAllMethod = repository.GetType().GetMethod("GetAll");

                var data = getAllMethod?.Invoke(repository, null) as System.Collections.IEnumerable;

                if (data != null)
                {
                    items = new List<object>();
                    foreach (var item in data)
                    {
                        // Extract value from KeyValuePair if needed
                        var actualData = ExtractActualData(item);
                        items.Add(actualData);
                    }
                }
            }
            else
            {
                // Single data - show as single row
                var getMethod = repository.GetType().GetMethod("Get");
                var singleData = getMethod?.Invoke(repository, null);
                if (singleData != null)
                {
                    items = new List<object> { singleData };
                }
            }


            if (columns == null || columns.Count == 0 || items == null) return;

            // Create header cells
            CreateHeaderCells();

            var bodyContainer = new VisualElement();
            bodyContainer.AddToClassList("table-body-container");

            // Create data rows
            foreach (var item in items)
            {
                CreateDataRow(item, bodyContainer);

                // Restore modified cells from changeTracker
                if (changeTracker != null)
                {
                    var itemKey = GetKeyFromItem(item);
                    if (itemKey != null)
                    {
                        var modifiedProps = changeTracker.GetModifiedProperties(itemKey);
                        foreach (var propName in modifiedProps)
                        {
                            if (cellElements.TryGetValue(item, out var cells) && cells.TryGetValue(propName, out var cell))
                                cell.Q<DatraPropertyField>().SetModified(true);
                        }
                    }
                }
            }

            bodyScrollView?.Add(bodyContainer);

            // Update modification state after restoring (to show orange dot if there are modifications)
            UpdateModifiedState();
        }
        
        private void CreateHeaderCells()
        {
            int columnIndex = 0;
            
            // Actions column header (first column)
            if (ShowActionsColumn)
            {
                var actionsHeader = CreateHeaderCell("Actions", 60);
                // Hide left resize handle for first column
                var leftHandle = actionsHeader.Q<VisualElement>(className: "resize-handle-left");
                if (leftHandle != null) leftHandle.style.display = DisplayStyle.None;
                headerRow.Add(actionsHeader);
                columnIndex++;
            }
            
            // ID column (only for table data, not single data)
            if (ShowIdColumn && IsTableData(dataType))
            {
                var idHeader = CreateHeaderCell("ID", 80);
                // Hide left resize handle for first column if no actions column
                if (columnIndex == 0)
                {
                    var leftHandle = idHeader.Q<VisualElement>(className: "resize-handle-left");
                    if (leftHandle != null) leftHandle.style.display = DisplayStyle.None;
                }
                headerRow.Add(idHeader);
                columnIndex++;
            }
            
            // Data columns
            foreach (var column in columns)
            {
                if (column.Name == "Id" && ShowIdColumn) continue; // Skip ID if already shown
                
                var headerCell = CreateHeaderCell(ObjectNames.NicifyVariableName(column.Name), 150);
                // Hide left resize handle for first column if no actions or ID column
                if (columnIndex == 0)
                {
                    var leftHandle = headerCell.Q<VisualElement>(className: "resize-handle-left");
                    if (leftHandle != null) leftHandle.style.display = DisplayStyle.None;
                }
                headerRow.Add(headerCell);
                columnIndex++;
            }
        }
        
        private VisualElement CreateHeaderCell(string text, float width)
        {
            var cell = new VisualElement();
            cell.AddToClassList("table-header-cell");
            cell.AddToClassList("header-cell-content");
            cell.style.width = width;
            cell.style.minWidth = width;

            var label = new Label(text);
            label.AddToClassList("header-cell-label");
            cell.Add(label);

            // Add resize handle on the right
            var resizeHandleRight = new VisualElement();
            resizeHandleRight.AddToClassList("table-resize-handle");
            resizeHandleRight.AddToClassList("resize-handle");
            resizeHandleRight.AddToClassList("resize-handle-right");
            resizeHandleRight.pickingMode = PickingMode.Position;

            // Add resize handle on the left (except for first column)
            var resizeHandleLeft = new VisualElement();
            resizeHandleLeft.AddToClassList("table-resize-handle");
            resizeHandleLeft.AddToClassList("resize-handle");
            resizeHandleLeft.AddToClassList("resize-handle-left");
            resizeHandleLeft.pickingMode = PickingMode.Position;

            // Set cursor style for right handle
            resizeHandleRight.RegisterCallback<MouseEnterEvent>(evt => {
                resizeHandleRight.style.cursor = new Cursor() { hotspot = Vector2.zero };
                resizeHandleRight.style.backgroundColor = new Color(0.5f, 0.7f, 1f, 0.3f);
            });

            resizeHandleRight.RegisterCallback<MouseLeaveEvent>(evt => {
                resizeHandleRight.style.cursor = StyleKeyword.Null;
                resizeHandleRight.style.backgroundColor = Color.clear;
            });

            resizeHandleRight.RegisterCallback<MouseDownEvent>(evt => StartResize(evt, cell));

            // Set cursor style for left handle
            resizeHandleLeft.RegisterCallback<MouseEnterEvent>(evt => {
                resizeHandleLeft.style.cursor = new Cursor() { hotspot = Vector2.zero };
                resizeHandleLeft.style.backgroundColor = new Color(0.5f, 0.7f, 1f, 0.3f);
            });

            resizeHandleLeft.RegisterCallback<MouseLeaveEvent>(evt => {
                resizeHandleLeft.style.cursor = StyleKeyword.Null;
                resizeHandleLeft.style.backgroundColor = Color.clear;
            });

            resizeHandleLeft.RegisterCallback<MouseDownEvent>(evt => {
                // Find previous cell to resize
                var index = headerRow.IndexOf(cell);
                if (index > 0)
                {
                    var previousCell = headerRow[index - 1];
                    StartResize(evt, previousCell);
                }
            });

            cell.Add(resizeHandleRight);
            cell.Add(resizeHandleLeft);

            return cell;
        }
        
        private void CreateDataRow(object item, VisualElement container)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");

            var cells = new Dictionary<string, VisualElement>();
            cellElements[item] = cells;

            // Add delete button as the first cell in the row
            if (ShowActionsColumn)
            {
                var deleteCell = new VisualElement();
                deleteCell.AddToClassList("table-cell");
                deleteCell.AddToClassList("delete-cell");

                var deleteButton = new Button(() => {
                    if (!isReadOnly)
                    {
                        base.DeleteItem(item);
                    }
                });
                deleteButton.text = "🗑";
                deleteButton.tooltip = "Delete Row";
                deleteButton.AddToClassList("table-delete-button");
                deleteCell.Add(deleteButton);

                row.Add(deleteCell);
            }

            // ID field (only for table data, not single data)
            if (ShowIdColumn && IsTableData(dataType))
            {
                var idProperty = dataType.GetProperty("Id");
                if (idProperty != null)
                {
                    var idCell = CreateEditableCell(item, idProperty, 80);
                    cells["Id"] = idCell;
                    row.Add(idCell);
                }
            }

            // Data cells
            foreach (var column in columns)
            {
                if (column.Name == "Id" && ShowIdColumn) continue;

                var cell = CreateEditableCell(item, column, 150);
                cells[column.Name] = cell;
                row.Add(cell);
            }

            container.Add(row);
            rowElements[item] = row;
        }
        
        private VisualElement CreateEditableCell(object item, PropertyInfo property, float width)
        {
            var cell = new VisualElement();
            cell.AddToClassList("table-cell");
            cell.AddToClassList("editable-cell");
            cell.style.width = width;
            cell.style.minWidth = width;

            if (isReadOnly || !property.CanWrite)
            {
                // Read-only display
                var value = property.GetValue(item);
                var displayValue = GetDisplayValue(property.PropertyType, value);
                var label = new Label(displayValue);
                cell.Add(label);
            }
            else
            {
                // Create field using DatraPropertyField in table mode
                var field = new DatraPropertyField(item, property, DatraFieldLayoutMode.Table);
                field.OnValueChanged += (propName, newValue) => {
                    var itemKey = GetKeyFromItem(item);
                    changeTracker.TrackPropertyChange(itemKey, propName, newValue, out bool isModified);

                    field.SetModified(isModified);

                    // Update modification state (checks changeTracker)
                    UpdateModifiedState();
                };

                field.OnRevertRequested += (propName) => {
                    // Get baseline value from tracker at property level
                    var itemKey = GetKeyFromItem(item);
                    if (itemKey == null) return;

                    // Get baseline value for this specific property
                    var baselineValue = changeTracker.GetPropertyBaselineValue(itemKey, propName);
                    if (baselineValue == null && !property.PropertyType.IsValueType)
                    {
                        // Property might be null in baseline - that's valid
                    }

                    // Update property value in the item object
                    property.SetValue(item, baselineValue);

                    // Update UI element directly
                    UpdateFieldValue(field, property.PropertyType, baselineValue);

                    // Track the property change again to remove it from changes (back to baseline)
                    changeTracker.TrackPropertyChange(itemKey, propName, baselineValue, out bool isModified);
                    field.SetModified(isModified);

                    // Update modification state (fires OnDataModified with correct state)
                    UpdateModifiedState();

                    Debug.Log($"[OnRevertRequested] Reverted property: key={itemKey}, property={propName}");
                };

                cell.Add(field);
            }

            return cell;
        }
        
        private string GetDisplayValue(Type type, object value)
        {
            if (value == null) return "";
            
            // Handle arrays
            if (type.IsArray)
            {
                var array = value as Array;
                return $"[{array?.Length ?? 0} items]";
            }
            
            // Handle DataRef types
            if (type.IsGenericType && 
                (type.GetGenericTypeDefinition() == typeof(StringDataRef<>) ||
                 type.GetGenericTypeDefinition() == typeof(IntDataRef<>)))
            {
                var keyValue = type.GetProperty("Value")?.GetValue(value);
                return keyValue != null ? $"→ {keyValue}" : "(None)";
            }
            
            return value.ToString();
        }
        
        protected override void FilterItems(string searchTerm)
        {
            if (items == null) return;
            
            foreach (var kvp in rowElements)
            {
                var item = kvp.Key;
                var row = kvp.Value;
                
                bool matches = string.IsNullOrEmpty(searchTerm);
                if (!matches)
                {
                    foreach (var column in columns)
                    {
                        var value = column.GetValue(item)?.ToString() ?? "";
                        if (value.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matches = true;
                            break;
                        }
                    }
                }
                
                row.style.display = matches ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        private void FilterRows(string searchTerm)
        {
            FilterItems(searchTerm);
        }
        
        private void ShowViewOptions()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show ID Column"), ShowIdColumn, () => {
                ShowIdColumn = !ShowIdColumn;
                RefreshContent();
            });
            menu.AddItem(new GUIContent("Show Actions"), ShowActionsColumn, () => {
                ShowActionsColumn = !ShowActionsColumn;
                RefreshContent();
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Export to CSV"), false, ExportToCSV);
            menu.ShowAsContext();
        }
        
        private void ExportToCSV()
        {
            // Implementation for CSV export
            Debug.Log("Export to CSV - Not implemented yet");
        }
        
        private void StartResize(MouseDownEvent evt, VisualElement cell)
        {
            evt.StopPropagation();
            
            isResizing = true;
            resizingColumn = cell;
            resizeStartX = evt.mousePosition.x;
            resizeStartWidth = cell.style.width.value.value;
            
            // Capture mouse
            cell.CaptureMouse();
            
            // Register mouse move and up handlers
            cell.RegisterCallback<MouseMoveEvent>(OnResizeMove);
            cell.RegisterCallback<MouseUpEvent>(OnResizeEnd);
            
            // Change cursor on the root element
            var root = GetRootVisualElement();
            if (root != null)
                root.style.cursor = new Cursor() { texture = null };
        }
        
        private void OnResizeMove(MouseMoveEvent evt)
        {
            if (!isResizing || resizingColumn == null) return;
            
            evt.StopPropagation();
            
            float deltaX = evt.mousePosition.x - resizeStartX;
            float newWidth = Mathf.Max(50, resizeStartWidth + deltaX); // Min width of 50
            
            // Update header column width
            resizingColumn.style.width = newWidth;
            resizingColumn.style.minWidth = newWidth;
            
            // Find the column index
            int columnIndex = headerRow.IndexOf(resizingColumn);
            if (columnIndex >= 0)
            {
                // Update all cells in this column
                foreach (var rowKvp in rowElements)
                {
                    var row = rowKvp.Value;
                    if (row.childCount > columnIndex)
                    {
                        var cell = row[columnIndex];
                        cell.style.width = newWidth;
                        cell.style.minWidth = newWidth;
                    }
                }
            }
        }
        
        private void OnResizeEnd(MouseUpEvent evt)
        {
            if (!isResizing) return;
            
            evt.StopPropagation();
            
            isResizing = false;
            
            // Release mouse capture
            if (resizingColumn != null)
            {
                resizingColumn.ReleaseMouse();
                resizingColumn.UnregisterCallback<MouseMoveEvent>(OnResizeMove);
                resizingColumn.UnregisterCallback<MouseUpEvent>(OnResizeEnd);
            }
            
            // Reset cursor
            var root = GetRootVisualElement();
            if (root != null)
                root.style.cursor = StyleKeyword.Null;
            
            resizingColumn = null;
        }
        
        private VisualElement GetRootVisualElement()
        {
            var element = this as VisualElement;
            while (element.parent != null)
            {
                element = element.parent;
            }
            return element;
        }
        
        protected override void UpdateEditability()
        {
            base.UpdateEditability();
            
            // Update toolbar buttons
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
        
        protected override void OnItemMarkedForDeletion(object item)
        {
            if (rowElements.TryGetValue(item, out var row))
            {
                row.AddToClassList("deleted-row");
            }
        }
        
        protected override void OnModificationsCleared()
        {
            base.OnModificationsCleared();

            // Clear visual modifications from all cells
            foreach (var (item, cells) in cellElements)
            {
                foreach (var (property, cell) in cells)
                    cell.Q<DatraPropertyField>()?.SetModified(false);
            }

            // Clear new row indicators
            foreach (var row in rowElements.Values)
            {
                row.RemoveFromClassList("new-row");
                row.RemoveFromClassList("deleted-row");
            }
        }

        protected override void SaveChanges()
        {
            base.SaveChanges();
            UpdateModifiedState();
        }

        protected override void RevertChanges()
        {
            base.RevertChanges();
            UpdateModifiedState();
        }
    }
}