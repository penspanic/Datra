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
        private ToolbarSearchField searchField;
        
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
            
            // Create table container
            tableContainer = new VisualElement();
            tableContainer.AddToClassList("table-container");
            tableContainer.style.flexGrow = 1;
            tableContainer.style.flexDirection = FlexDirection.Column;
            
            // Create header container
            var tableHeaderContainer = new VisualElement();
            tableHeaderContainer.AddToClassList("table-header-container");
            tableHeaderContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            tableHeaderContainer.style.borderBottomWidth = 1;
            tableHeaderContainer.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            
            headerRow = new VisualElement();
            headerRow.AddToClassList("table-header-row");
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.height = RowHeight;
            tableHeaderContainer.Add(headerRow);
            
            tableContainer.Add(tableHeaderContainer);
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
            searchField.RegisterValueChangedCallback(evt => FilterRows(evt.newValue));
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
            
            // Clear table body
            var tableBody = tableContainer.Q<ScrollView>("table-body");
            if (tableBody != null)
            {
                tableContainer.Remove(tableBody);
            }
            
            headerRow?.Clear();
            cellElements.Clear();
            rowElements.Clear();
            modifiedCells.Clear();
            
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
            
            // Create scrollable body
            var bodyScrollView = new ScrollView(ScrollViewMode.Vertical);
            bodyScrollView.name = "table-body";
            bodyScrollView.AddToClassList("table-body-scroll");
            bodyScrollView.style.flexGrow = 1;
            
            var bodyContainer = new VisualElement();
            bodyContainer.AddToClassList("table-body-container");
            bodyContainer.style.flexDirection = FlexDirection.Column;
            
            // Create data rows
            foreach (var item in items)
            {
                CreateDataRow(item, bodyContainer);
            }
            
            bodyScrollView.Add(bodyContainer);
            tableContainer.Add(bodyScrollView);
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
            
            // Actions column
            if (ShowActionsColumn)
            {
                var actionsHeader = CreateHeaderCell("Actions", 60);
                headerRow.Add(actionsHeader);
            }
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
        
        private void CreateDataRow(object item, VisualElement container)
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
            
            // Actions
            if (ShowActionsColumn)
            {
                var actionsCell = new VisualElement();
                actionsCell.AddToClassList("table-cell");
                actionsCell.style.width = 60;
                actionsCell.style.minWidth = 60;
                actionsCell.style.flexDirection = FlexDirection.Row;
                actionsCell.style.justifyContent = Justify.Center;
                
                var deleteButton = new Button(() => {
                    if (!isReadOnly)
                        base.DeleteItem(item);
                });
                deleteButton.text = "🗑";
                deleteButton.tooltip = "Delete Row";
                deleteButton.AddToClassList("table-delete-button");
                actionsCell.Add(deleteButton);
                
                row.Add(actionsCell);
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
        
        private void FilterRows(string searchTerm)
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
        
        
        protected override void UpdateEditability()
        {
            base.UpdateEditability();
            
            // Update toolbar buttons
            var addButton = headerContainer.Q<Button>(className: "table-add-button");
            addButton?.SetEnabled(!isReadOnly);
            
            // Update delete buttons
            foreach (var row in rowElements.Values)
            {
                var deleteButton = row.Q<Button>(className: "table-delete-button");
                deleteButton?.SetEnabled(!isReadOnly);
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
            modifiedCells.Clear();
        }
    }
}