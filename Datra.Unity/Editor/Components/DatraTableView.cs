using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;

namespace Datra.Unity.Editor.Components
{
    public class DatraTableView : VisualElement
    {
        private ScrollView scrollView;
        private VisualElement headerRow;
        private VisualElement contentContainer;
        private List<PropertyInfo> columns;
        private Type dataType;
        private List<object> items;
        private DatraPropertyTracker propertyTracker;
        private Dictionary<object, Dictionary<string, VisualElement>> cellElements;
        
        // Events
        public event Action<object> OnItemSelected;
        public event Action<object, string, object> OnCellValueChanged;
        public event Action<object> OnItemDeleted;
        public event Action OnAddNewItem;
        
        // Properties
        public bool ShowIdColumn { get; set; } = true;
        public bool ShowActionsColumn { get; set; } = true;
        public bool IsEditable { get; set; } = true;
        public float RowHeight { get; set; } = 28f;
        
        public DatraTableView()
        {
            AddToClassList("datra-table-view");
            cellElements = new Dictionary<object, Dictionary<string, VisualElement>>();
            Initialize();
        }
        
        private void Initialize()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            
            // Create header container
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("table-header-container");
            headerContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            headerContainer.style.borderBottomWidth = 1;
            headerContainer.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);
            
            headerRow = new VisualElement();
            headerRow.AddToClassList("table-header-row");
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.height = RowHeight;
            headerContainer.Add(headerRow);
            
            Add(headerContainer);
            
            // Create scrollable content area
            scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList("table-scroll-view");
            scrollView.style.flexGrow = 1;
            
            contentContainer = new VisualElement();
            contentContainer.AddToClassList("table-content");
            contentContainer.style.flexDirection = FlexDirection.Column;
            scrollView.Add(contentContainer);
            
            Add(scrollView);
            
            // Add toolbar
            var toolbar = CreateToolbar();
            Insert(0, toolbar);
        }
        
        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("table-toolbar");
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 30;
            toolbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;
            
            // Add button
            var addButton = new Button(() => OnAddNewItem?.Invoke());
            addButton.text = "➕ Add Row";
            addButton.AddToClassList("table-add-button");
            addButton.style.marginRight = 8;
            toolbar.Add(addButton);
            
            // Search field
            var searchField = new ToolbarSearchField();
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
        
        public void SetData(Type type, IEnumerable<object> data, DatraPropertyTracker tracker = null)
        {
            dataType = type;
            items = data?.ToList() ?? new List<object>();
            propertyTracker = tracker ?? new DatraPropertyTracker();
            
            // Get columns (properties)
            columns = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && IsSupportedType(p.PropertyType))
                .ToList();
            
            RefreshTable();
        }
        
        private void RefreshTable()
        {
            headerRow.Clear();
            contentContainer.Clear();
            cellElements.Clear();
            
            if (columns == null || columns.Count == 0) return;
            
            // Create header cells
            CreateHeaderCells();
            
            // Create data rows
            foreach (var item in items)
            {
                CreateDataRow(item);
            }
        }
        
        private void CreateHeaderCells()
        {
            // Selection column
            var selectHeader = new VisualElement();
            selectHeader.AddToClassList("table-header-cell");
            selectHeader.style.width = 30;
            selectHeader.style.minWidth = 30;
            headerRow.Add(selectHeader);
            
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
        
        private void CreateDataRow(object item)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = RowHeight;
            row.style.alignItems = Align.Center;
            
            var cells = new Dictionary<string, VisualElement>();
            cellElements[item] = cells;
            
            // Selection checkbox
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
                
                var deleteButton = new Button(() => OnItemDeleted?.Invoke(item));
                deleteButton.text = "🗑";
                deleteButton.tooltip = "Delete Row";
                deleteButton.AddToClassList("table-delete-button");
                actionsCell.Add(deleteButton);
                
                row.Add(actionsCell);
            }
            
            // Row hover effect
            row.RegisterCallback<MouseEnterEvent>(evt => {
                if (!row.ClassListContains("selected"))
                    row.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.2f);
            });
            
            row.RegisterCallback<MouseLeaveEvent>(evt => {
                if (!row.ClassListContains("selected"))
                    row.style.backgroundColor = Color.clear;
            });
            
            contentContainer.Add(row);
        }
        
        private VisualElement CreateEditableCell(object item, PropertyInfo property, float width)
        {
            var cell = new VisualElement();
            cell.AddToClassList("table-cell");
            cell.style.width = width;
            cell.style.minWidth = width;
            cell.style.paddingLeft = 8;
            cell.style.paddingRight = 8;
            
            if (!IsEditable || !property.CanWrite)
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
                var field = CreateFieldForType(property.PropertyType, property.GetValue(item));
                if (field != null)
                {
                    field.style.flexGrow = 1;
                    field.RegisterCallback<ChangeEvent<object>>(evt => {
                        property.SetValue(item, evt.newValue);
                        OnCellValueChanged?.Invoke(item, property.Name, evt.newValue);
                        propertyTracker?.TrackChange(item, property.Name, evt.newValue);
                    });
                    cell.Add(field);
                }
            }
            
            return cell;
        }
        
        private VisualElement CreateFieldForType(Type type, object value)
        {
            if (type == typeof(string))
            {
                var field = new TextField();
                field.value = value as string ?? "";
                field.style.minHeight = 20;
                return field;
            }
            else if (type == typeof(int))
            {
                var field = new IntegerField();
                field.value = (int)(value ?? 0);
                field.style.minHeight = 20;
                return field;
            }
            else if (type == typeof(float))
            {
                var field = new FloatField();
                field.value = (float)(value ?? 0f);
                field.style.minHeight = 20;
                return field;
            }
            else if (type == typeof(bool))
            {
                var field = new Toggle();
                field.value = (bool)(value ?? false);
                return field;
            }
            else if (type.IsEnum)
            {
                var field = new EnumField((Enum)(value ?? Activator.CreateInstance(type)));
                field.style.minHeight = 20;
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
            var rows = contentContainer.Children().ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var item = items[i];
                
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
                RefreshTable();
            });
            menu.AddItem(new GUIContent("Show Actions"), ShowActionsColumn, () => {
                ShowActionsColumn = !ShowActionsColumn;
                RefreshTable();
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
                        var newField = CreateFieldForType(property.PropertyType, property.GetValue(item));
                        if (newField != null)
                        {
                            cell.Add(newField);
                        }
                    }
                }
            }
        }
    }
}