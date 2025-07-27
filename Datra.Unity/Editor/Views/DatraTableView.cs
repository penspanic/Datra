using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.Unity.Editor.Components;
using Datra.DataTypes;
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
        private HashSet<(object item, string property)> modifiedCells;
        private ScrollView bodyScrollView;
        private VisualElement deleteColumnContainer;
        
        // Events
        public event Action<object, string, object> OnCellValueChanged;
        
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
            modifiedCells = new HashSet<(object, string)>();
        }
        
        protected override void InitializeView()
        {
            // Clear content container from base class
            contentContainer.Clear();
            
            // Add toolbar to header
            var toolbar = CreateToolbar();
            headerContainer.Add(toolbar);
            
            // Create main table container with horizontal layout
            tableContainer = new VisualElement();
            tableContainer.AddToClassList("table-container");
            tableContainer.style.flexGrow = 1;
            tableContainer.style.flexDirection = FlexDirection.Row;
            
            // Create scrollable container for both header and body
            bodyScrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            bodyScrollView.name = "table-body";
            bodyScrollView.AddToClassList("table-body-scroll");
            bodyScrollView.style.flexGrow = 1;
            
            var scrollableContent = new VisualElement();
            scrollableContent.style.flexDirection = FlexDirection.Column;
            
            // Create header container (inside scroll for horizontal sync)
            var tableHeaderContainer = new VisualElement();
            tableHeaderContainer.AddToClassList("table-header-container");
            tableHeaderContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            tableHeaderContainer.style.borderBottomWidth = 1;
            tableHeaderContainer.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            tableHeaderContainer.style.position = Position.Relative;
            
            headerRow = new VisualElement();
            headerRow.AddToClassList("table-header-row");
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.height = RowHeight;
            tableHeaderContainer.Add(headerRow);
            
            scrollableContent.Add(tableHeaderContainer);
            
            bodyScrollView.Add(scrollableContent);
            tableContainer.Add(bodyScrollView);
            
            // Create delete column container (fixed, outside scroll)
            deleteColumnContainer = new VisualElement();
            deleteColumnContainer.AddToClassList("delete-column-container");
            deleteColumnContainer.style.flexDirection = FlexDirection.Column;
            deleteColumnContainer.style.width = 60;
            deleteColumnContainer.style.minWidth = 60;
            deleteColumnContainer.style.borderLeftWidth = 1;
            deleteColumnContainer.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
            deleteColumnContainer.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
            
            // Add header for delete column
            var deleteHeader = new VisualElement();
            deleteHeader.AddToClassList("table-header-cell");
            deleteHeader.style.height = RowHeight;
            deleteHeader.style.justifyContent = Justify.Center;
            deleteHeader.style.alignItems = Align.Center;
            deleteHeader.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            deleteHeader.style.borderBottomWidth = 1;
            deleteHeader.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            
            var deleteHeaderLabel = new Label("Actions");
            deleteHeaderLabel.style.fontSize = 11;
            deleteHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            deleteHeader.Add(deleteHeaderLabel);
            deleteColumnContainer.Add(deleteHeader);
            
            // Container for delete buttons
            var deleteButtonsContainer = new VisualElement();
            deleteButtonsContainer.name = "delete-buttons-container";
            deleteButtonsContainer.AddToClassList("delete-buttons-container");
            deleteButtonsContainer.style.flexGrow = 1;
            deleteButtonsContainer.style.overflow = Overflow.Hidden;
            deleteColumnContainer.Add(deleteButtonsContainer);
            
            tableContainer.Add(deleteColumnContainer);
            
            contentContainer.Add(tableContainer);
        }
        
        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("table-toolbar");
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 36;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            
            // Add button
            var addButton = new Button(() => {
                if (!isReadOnly)
                    AddNewItem();
            });
            addButton.text = "➕ Add Row";
            addButton.AddToClassList("table-add-button");
            addButton.style.marginRight = 8;
            addButton.SetEnabled(!isReadOnly);
            toolbar.Add(addButton);
            
            // Search field
            searchField = new ToolbarSearchField();
            searchField.AddToClassList("table-search");
            searchField.style.flexGrow = 1;
            (searchField as ToolbarSearchField).RegisterValueChangedCallback(evt => FilterRows(evt.newValue));
            toolbar.Add(searchField);
            
            // View options
            var optionsButton = new Button(() => ShowViewOptions());
            optionsButton.text = "⚙";
            optionsButton.tooltip = "View Options";
            optionsButton.AddToClassList("table-options-button");
            optionsButton.style.marginLeft = 8;
            toolbar.Add(optionsButton);
            
            return toolbar;
        }
        
        public override void SetData(Type type, object repo, object context)
        {
            // Don't call base.SetData yet - we need to initialize columns first
            dataType = type;
            repository = repo;
            dataContext = context;
            hasUnsavedChanges = false;
            
            // Get columns (properties) BEFORE calling RefreshContent
            columns = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && IsSupportedType(p.PropertyType))
                .ToList();
                
            
            // Now cleanup and refresh
            CleanupFields();
            RefreshContent();
            UpdateFooter();
        }
        
        public void SetTableData(IEnumerable<object> data)
        {
            items = data?.ToList() ?? new List<object>();
            RefreshContent();
        }
        
        public override void RefreshContent()
        {
            if (tableContainer == null) return;
            
            // Store current modified state before clearing
            var previousModifiedCells = new HashSet<(object, string)>(modifiedCells);
            var previousNewItems = new HashSet<object>(newItems);
            var previousTrackers = new Dictionary<object, DatraPropertyTracker>(itemTrackers);
            
            // Clear table content
            var scrollContent = bodyScrollView?.Q<VisualElement>();
            if (scrollContent != null && scrollContent.childCount > 1)
            {
                // Keep header, remove body
                for (int i = scrollContent.childCount - 1; i > 0; i--)
                {
                    scrollContent.RemoveAt(i);
                }
            }
            
            var deleteButtons = deleteColumnContainer?.Q<VisualElement>("delete-buttons-container");
            deleteButtons?.Clear();
            
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
                else
                {
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
            bodyContainer.style.flexDirection = FlexDirection.Column;
            
            var deleteButtonsContainer = deleteColumnContainer?.Q<VisualElement>("delete-buttons-container");
            
            // Create data rows
            foreach (var item in items)
            {
                CreateDataRow(item, bodyContainer, deleteButtonsContainer);
                
                // Restore tracking state
                if (previousTrackers.ContainsKey(item))
                {
                    itemTrackers[item] = previousTrackers[item];
                }
                
                // Restore modified cells
                foreach (var (modItem, prop) in previousModifiedCells)
                {
                    if (modItem.Equals(item))
                    {
                        modifiedCells.Add((item, prop));
                        if (cellElements.TryGetValue(item, out var cells) && cells.TryGetValue(prop, out var cell))
                        {
                            cell.AddToClassList("modified-cell");
                        }
                    }
                }
                
                // Restore new items
                if (previousNewItems.Contains(item))
                {
                    newItems.Add(item);
                    if (rowElements.TryGetValue(item, out var row))
                    {
                        row.AddToClassList("new-row");
                    }
                }
            }
            
            scrollContent?.Add(bodyContainer);
            
            // Sync scroll position of delete buttons with main content
            SyncDeleteButtonsScroll();
        }
        
        private void CreateHeaderCells()
        {
            int columnIndex = 0;
            
            // ID column
            if (ShowIdColumn)
            {
                var idHeader = CreateHeaderCell("ID", 80);
                // Hide left resize handle for first column
                var leftHandle = idHeader.Q<VisualElement>(className: "resize-handle-left");
                if (leftHandle != null) leftHandle.style.display = DisplayStyle.None;
                headerRow.Add(idHeader);
                columnIndex++;
            }
            
            // Data columns
            foreach (var column in columns)
            {
                if (column.Name == "Id" && ShowIdColumn) continue; // Skip ID if already shown
                
                var headerCell = CreateHeaderCell(ObjectNames.NicifyVariableName(column.Name), 150);
                // Hide left resize handle for first column if no ID column
                if (columnIndex == 0)
                {
                    var leftHandle = headerCell.Q<VisualElement>(className: "resize-handle-left");
                    if (leftHandle != null) leftHandle.style.display = DisplayStyle.None;
                }
                headerRow.Add(headerCell);
                columnIndex++;
            }
            
            // Actions column header is now in the fixed delete column container
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
            
            // Add resize handle on the right
            var resizeHandleRight = new VisualElement();
            resizeHandleRight.AddToClassList("table-resize-handle");
            resizeHandleRight.AddToClassList("resize-handle-right");
            resizeHandleRight.style.position = Position.Absolute;
            resizeHandleRight.style.right = -3; // Extend 3px to the right
            resizeHandleRight.style.top = 0;
            resizeHandleRight.style.bottom = 0;
            resizeHandleRight.style.width = 6;
            resizeHandleRight.pickingMode = PickingMode.Position;
            
            // Add resize handle on the left (except for first column)
            var resizeHandleLeft = new VisualElement();
            resizeHandleLeft.AddToClassList("table-resize-handle");
            resizeHandleLeft.AddToClassList("resize-handle-left");
            resizeHandleLeft.style.position = Position.Absolute;
            resizeHandleLeft.style.left = -3; // Extend 3px to the left
            resizeHandleLeft.style.top = 0;
            resizeHandleLeft.style.bottom = 0;
            resizeHandleLeft.style.width = 6;
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
        
        private void CreateDataRow(object item, VisualElement container, VisualElement deleteContainer)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = RowHeight;
            row.style.alignItems = Align.Center;
            
            var cells = new Dictionary<string, VisualElement>();
            cellElements[item] = cells;
            
            
            
            // ID field
            if (ShowIdColumn)
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
            
            // Create delete button in the fixed column
            if (ShowActionsColumn && deleteContainer != null)
            {
                var deleteButtonContainer = new VisualElement();
                deleteButtonContainer.style.height = RowHeight;
                deleteButtonContainer.style.justifyContent = Justify.Center;
                deleteButtonContainer.style.alignItems = Align.Center;
                deleteButtonContainer.style.paddingLeft = 8;
                deleteButtonContainer.style.paddingRight = 8;
                
                var deleteButton = new Button(() => {
                    if (!isReadOnly)
                    {
                        base.DeleteItem(item);
                    }
                });
                deleteButton.text = "🗑";
                deleteButton.tooltip = "Delete Row";
                deleteButton.AddToClassList("table-delete-button");
                deleteButtonContainer.Add(deleteButton);
                
                deleteContainer.Add(deleteButtonContainer);
            }
            
            // Row hover effect
            row.RegisterCallback<MouseEnterEvent>(evt => {
                if (!row.ClassListContains("selected") && !row.ClassListContains("modified-row"))
                    row.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.2f);
            });
            
            row.RegisterCallback<MouseLeaveEvent>(evt => {
                if (!row.ClassListContains("selected") && !row.ClassListContains("modified-row"))
                    row.style.backgroundColor = Color.clear;
            });
            
            container.Add(row);
            rowElements[item] = row;
        }
        
        private VisualElement CreateEditableCell(object item, PropertyInfo property, float width)
        {
            var cell = new VisualElement();
            cell.AddToClassList("table-cell");
            cell.style.width = width;
            cell.style.minWidth = width;
            cell.style.paddingLeft = 8;
            cell.style.paddingRight = 8;
            
            if (isReadOnly || !property.CanWrite)
            {
                // Read-only display
                var value = property.GetValue(item);
                var displayValue = GetDisplayValue(property.PropertyType, value);
                var label = new Label(displayValue);
                label.style.fontSize = 11;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                cell.Add(label);
            }
            else
            {
                // Get or create tracker for this item
                if (!itemTrackers.ContainsKey(item))
                {
                    var tracker = new DatraPropertyTracker();
                    tracker.StartTracking(item, false);
                    tracker.OnAnyPropertyModified += OnTrackerModified;
                    itemTrackers[item] = tracker;
                }
                
                // Create field using DatraPropertyField in table mode
                var field = new DatraPropertyField(item, property, itemTrackers[item], DatraFieldLayoutMode.Table);
                field.OnValueChanged += (propName, newValue) => {
                    OnCellValueChanged?.Invoke(item, propName, newValue);
                    MarkAsModified();
                    
                    // Track modified cell
                    modifiedCells.Add((item, propName));
                    
                    // Update cell visual state
                    if (cellElements.TryGetValue(item, out var cells) && cells.TryGetValue(propName, out var modCell))
                    {
                        modCell.AddToClassList("modified-cell");
                    }
                };
                field.style.flexGrow = 1;
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
        
        private bool IsSupportedType(Type type)
        {
            // Basic types
            if (type == typeof(string) || 
                type == typeof(int) || 
                type == typeof(float) || 
                type == typeof(bool) || 
                type.IsEnum)
            {
                return true;
            }
            
            // Array types
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return elementType == typeof(int) || 
                       elementType == typeof(string) || 
                       elementType == typeof(float) ||
                       elementType.IsEnum;
            }
            
            // DataRef types
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                return genericDef == typeof(StringDataRef<>) || 
                       genericDef == typeof(IntDataRef<>);
            }
            
            return false;
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
            evt.PreventDefault();
            
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
            evt.PreventDefault();
            
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
            evt.PreventDefault();
            
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
        
        public void RefreshCell(object item, string propertyName)
        {
            if (cellElements.TryGetValue(item, out var cells))
            {
                if (cells.TryGetValue(propertyName, out var cell))
                {
                    // Refresh the specific cell
                    var property = columns.FirstOrDefault(c => c.Name == propertyName);
                    if (property != null && property.CanWrite && !isReadOnly)
                    {
                        cell.Clear();
                        
                        // Get or create tracker for this item
                        if (!itemTrackers.ContainsKey(item))
                        {
                            var tracker = new DatraPropertyTracker();
                            tracker.StartTracking(item, false);
                            tracker.OnAnyPropertyModified += OnTrackerModified;
                            itemTrackers[item] = tracker;
                        }
                        
                        // Create field using DatraPropertyField in table mode
                        var field = new DatraPropertyField(item, property, itemTrackers[item], DatraFieldLayoutMode.Table);
                        field.OnValueChanged += (propName, newValue) => {
                            OnCellValueChanged?.Invoke(item, propName, newValue);
                            MarkAsModified();
                            
                            // Track modified cell
                            modifiedCells.Add((item, propName));
                            
                            // Update cell visual state
                            if (cellElements.TryGetValue(item, out var cells) && cells.TryGetValue(propName, out var modCell))
                            {
                                modCell.AddToClassList("modified-cell");
                            }
                        };
                        field.style.flexGrow = 1;
                        cell.Add(field);
                    }
                }
            }
        }
        
        
        private void SyncDeleteButtonsScroll()
        {
            if (bodyScrollView == null || deleteColumnContainer == null) return;
            
            var deleteButtonsContainer = deleteColumnContainer.Q<VisualElement>("delete-buttons-container");
            if (deleteButtonsContainer == null) return;
            
            // Sync vertical scroll position
            bodyScrollView.verticalScroller.valueChanged += (value) => {
                deleteButtonsContainer.style.top = -value;
            };
        }
        
        protected override void UpdateEditability()
        {
            base.UpdateEditability();
            
            // Update toolbar buttons
            var addButton = headerContainer.Q<Button>(className: "table-add-button");
            addButton?.SetEnabled(!isReadOnly);
            
            // Update delete buttons in the fixed column
            var deleteButtons = deleteColumnContainer?.Q<VisualElement>("delete-buttons-container")?.Query<Button>(className: "table-delete-button").ToList();
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
        
        protected override void SaveChanges()
        {
            base.SaveChanges();
            
            // Clear visual modifications from all cells after save
            foreach (var (item, cells) in cellElements)
            {
                foreach (var (property, cell) in cells)
                {
                    cell.RemoveFromClassList("modified-cell");
                }
            }
            
            // Clear new row indicators
            foreach (var row in rowElements.Values)
            {
                row.RemoveFromClassList("new-row");
                row.RemoveFromClassList("deleted-row");
            }
            
            modifiedCells.Clear();
        }
        
        protected override void RevertChanges()
        {
            base.RevertChanges();
            
            // Clear visual modifications from all cells
            foreach (var (item, cells) in cellElements)
            {
                foreach (var (property, cell) in cells)
                {
                    cell.RemoveFromClassList("modified-cell");
                }
            }
            
            // Clear new row indicators
            foreach (var row in rowElements.Values)
            {
                row.RemoveFromClassList("new-row");
                row.RemoveFromClassList("deleted-row");
            }
            
            modifiedCells.Clear();
        }
    }
}