using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.Unity.Editor.Components;
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
        public event Action<object> OnItemSelected;
        public event Action<object, string, object> OnCellValueChanged;
        
        // Properties
        public bool ShowSelectionColumn { get; set; } = true;
        public bool ShowIdColumn { get; set; } = true;
        public bool ShowActionsColumn { get; set; } = true;
        public float RowHeight { get; set; } = 28f;
        
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
            // Selection column
            if (ShowSelectionColumn)
            {
                var selectHeader = new VisualElement();
                selectHeader.AddToClassList("table-header-cell");
                selectHeader.style.width = 30;
                selectHeader.style.minWidth = 30;
                headerRow.Add(selectHeader);
            }
            
            // ID column
            if (ShowIdColumn)
            {
                var idHeader = CreateHeaderCell("ID", 80);
                headerRow.Add(idHeader);
            }
            
            // Data columns
            foreach (var column in columns)
            {
                if (column.Name == "Id" && ShowIdColumn) continue; // Skip ID if already shown
                
                var headerCell = CreateHeaderCell(ObjectNames.NicifyVariableName(column.Name), 150);
                headerRow.Add(headerCell);
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
            
            // Add resize handle
            var resizeHandle = new VisualElement();
            resizeHandle.AddToClassList("table-resize-handle");
            resizeHandle.style.position = Position.Absolute;
            resizeHandle.style.right = 0;
            resizeHandle.style.top = 0;
            resizeHandle.style.bottom = 0;
            resizeHandle.style.width = 4;
            resizeHandle.style.cursor = new Cursor();
            resizeHandle.RegisterCallback<MouseDownEvent>(evt => StartResize(evt, cell));
            cell.Add(resizeHandle);
            
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
            
            
            // Selection checkbox
            if (ShowSelectionColumn)
            {
                var selectCell = new VisualElement();
                selectCell.AddToClassList("table-cell");
                selectCell.style.width = 30;
                selectCell.style.minWidth = 30;
                selectCell.style.justifyContent = Justify.Center;
                
                var checkbox = new Toggle();
                checkbox.style.marginLeft = 6;
                checkbox.RegisterValueChangedCallback(evt => {
                    if (evt.newValue)
                        OnItemSelected?.Invoke(item);
                });
                selectCell.Add(checkbox);
                row.Add(selectCell);
            }
            
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
                var label = new Label(value?.ToString() ?? "");
                label.style.fontSize = 11;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                cell.Add(label);
            }
            else
            {
                // Editable field
                var field = CreateFieldForType(property.PropertyType, property.GetValue(item), (newValue) => {
                    property.SetValue(item, newValue);
                    OnCellValueChanged?.Invoke(item, property.Name, newValue);
                    
                    // Track changes
                    if (!itemTrackers.ContainsKey(item))
                    {
                        var tracker = new DatraPropertyTracker();
                        tracker.StartTracking(item, false);
                        tracker.OnAnyPropertyModified += OnTrackerModified;
                        itemTrackers[item] = tracker;
                    }
                    itemTrackers[item].TrackChange(item, property.Name, newValue);
                    MarkAsModified();
                    
                    // Track modified cell
                    modifiedCells.Add((item, property.Name));
                    
                    // Update cell visual state
                    if (cellElements.TryGetValue(item, out var cells) && cells.TryGetValue(property.Name, out var cell))
                    {
                        cell.AddToClassList("modified-cell");
                    }
                });
                if (field != null)
                {
                    field.style.flexGrow = 1;
                    cell.Add(field);
                }
            }
            
            return cell;
        }
        
        private VisualElement CreateFieldForType(Type type, object value, Action<object> onValueChanged)
        {
            if (type == typeof(string))
            {
                var field = new TextField();
                field.value = value as string ?? "";
                field.style.minHeight = 20;
                field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
                return field;
            }
            else if (type == typeof(int))
            {
                var field = new IntegerField();
                field.value = (int)(value ?? 0);
                field.style.minHeight = 20;
                field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
                return field;
            }
            else if (type == typeof(float))
            {
                var field = new FloatField();
                field.value = (float)(value ?? 0f);
                field.style.minHeight = 20;
                field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
                return field;
            }
            else if (type == typeof(bool))
            {
                var field = new Toggle();
                field.value = (bool)(value ?? false);
                field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
                return field;
            }
            else if (type.IsEnum)
            {
                var field = new EnumField((Enum)(value ?? Activator.CreateInstance(type)));
                field.style.minHeight = 20;
                field.RegisterValueChangedCallback(evt => onValueChanged(evt.newValue));
                return field;
            }
            
            return null;
        }
        
        private bool IsSupportedType(Type type)
        {
            return type == typeof(string) || 
                   type == typeof(int) || 
                   type == typeof(float) || 
                   type == typeof(bool) || 
                   type.IsEnum;
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
            // Column resize implementation
            evt.StopPropagation();
        }
        
        public void RefreshCell(object item, string propertyName)
        {
            if (cellElements.TryGetValue(item, out var cells))
            {
                if (cells.TryGetValue(propertyName, out var cell))
                {
                    // Refresh the specific cell
                    var property = columns.FirstOrDefault(c => c.Name == propertyName);
                    if (property != null)
                    {
                        cell.Clear();
                        var newField = CreateFieldForType(property.PropertyType, property.GetValue(item), (newValue) => {
                            property.SetValue(item, newValue);
                            OnCellValueChanged?.Invoke(item, property.Name, newValue);
                            
                            // Track changes
                            if (!itemTrackers.ContainsKey(item))
                            {
                                var tracker = new DatraPropertyTracker();
                                tracker.StartTracking(item, false);
                                tracker.OnAnyPropertyModified += OnTrackerModified;
                                itemTrackers[item] = tracker;
                            }
                            itemTrackers[item].TrackChange(item, property.Name, newValue);
                            MarkAsModified();
                            
                            // Track modified cell
                            modifiedCells.Add((item, property.Name));
                            
                            // Update cell visual state
                            if (cellElements.TryGetValue(item, out var cells) && cells.TryGetValue(property.Name, out var cell))
                            {
                                cell.AddToClassList("modified-cell");
                            }
                        });
                        if (newField != null)
                        {
                            newField.style.flexGrow = 1;
                            cell.Add(newField);
                        }
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