using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.Unity.Editor.Components;
using Datra.DataTypes;
using Datra.Interfaces;
using Datra.Unity.Editor.Utilities;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;

namespace Datra.Unity.Editor.Views
{
    /// <summary>
    /// Virtualized table view for generic data types
    /// </summary>
    public class DatraTableView : VirtualizedTableView
    {
        // Properties
        private List<PropertyInfo> columns;
        public bool ShowIdColumn { get; set; } = true;
        public bool ShowActionsColumn { get; set; } = true;

        // Column widths
        private Dictionary<string, float> columnWidths;
        private const float ActionsColumnWidth = 60f;
        private const float IdColumnWidth = 80f;
        private const float DefaultColumnWidth = 150f;

        // Column resize tracking
        private bool isResizing = false;
        private VisualElement resizingColumn;
        private float resizeStartX;
        private float resizeStartWidth;

        public DatraTableView() : base()
        {
            AddToClassList("datra-table-view");
            columnWidths = new Dictionary<string, float>();
        }

        public override void SetData(Type type, IDataRepository repo, IDataContext context, IRepositoryChangeTracker tracker)
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
            columns = GetFilteredProperties(type);

            // Now cleanup and refresh
            CleanupFields();
            RefreshContent();
            UpdateFooter();
        }

        protected override void CreateHeaderCells()
        {
            int columnIndex = 0;

            // Actions column header
            if (ShowActionsColumn)
            {
                var actionsHeader = CreateHeaderCell("Actions", ActionsColumnWidth);
                var leftHandle = actionsHeader.Q<VisualElement>(className: "resize-handle-left");
                if (leftHandle != null) leftHandle.style.display = DisplayStyle.None;
                headerRow.Add(actionsHeader);
                columnIndex++;
            }

            // ID column (only for table data)
            if (ShowIdColumn && IsTableData(dataType))
            {
                var idHeader = CreateHeaderCell("ID", IdColumnWidth);
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
                if (column.Name == "Id" && ShowIdColumn) continue;

                var headerCell = CreateHeaderCell(ObjectNames.NicifyVariableName(column.Name), DefaultColumnWidth);
                if (columnIndex == 0)
                {
                    var leftHandle = headerCell.Q<VisualElement>(className: "resize-handle-left");
                    if (leftHandle != null) leftHandle.style.display = DisplayStyle.None;
                }
                headerRow.Add(headerCell);
                columnIndex++;
            }
        }

        protected override void CreateRowCells(VisualElement row)
        {
            // Actions cell
            if (ShowActionsColumn)
            {
                var actionsCell = new VisualElement();
                actionsCell.AddToClassList("table-cell");
                actionsCell.AddToClassList("delete-cell");
                actionsCell.style.width = ActionsColumnWidth;
                actionsCell.style.minWidth = ActionsColumnWidth;

                var deleteButton = new Button();
                deleteButton.text = "🗑";
                deleteButton.tooltip = "Delete Row";
                deleteButton.AddToClassList("table-delete-button");
                deleteButton.SetEnabled(!isReadOnly);
                actionsCell.Add(deleteButton);

                row.Add(actionsCell);
            }

            // ID cell (only for table data)
            if (ShowIdColumn && IsTableData(dataType))
            {
                var idCell = new VisualElement();
                idCell.AddToClassList("table-cell");
                idCell.AddToClassList("editable-cell");
                idCell.style.width = IdColumnWidth;
                idCell.style.minWidth = IdColumnWidth;
                idCell.name = "id-cell";
                row.Add(idCell);
            }

            // Data cells
            foreach (var column in columns)
            {
                if (column.Name == "Id" && ShowIdColumn) continue;

                var cell = new VisualElement();
                cell.AddToClassList("table-cell");
                cell.AddToClassList("editable-cell");
                cell.style.width = DefaultColumnWidth;
                cell.style.minWidth = DefaultColumnWidth;
                cell.name = $"cell-{column.Name}";
                row.Add(cell);
            }
        }

        protected override void BindRowData(VisualElement row, object item, int index)
        {
            int cellIndex = 0;

            // Bind actions cell (delete button)
            if (ShowActionsColumn)
            {
                var actionsCell = row[cellIndex];
                var deleteButton = actionsCell.Q<Button>();
                if (deleteButton != null)
                {
                    // Clear previous callbacks
                    deleteButton.clicked += () => {
                        if (!isReadOnly)
                        {
                            DeleteItem(item);
                        }
                    };
                }
                cellIndex++;
            }

            // Bind ID cell
            if (ShowIdColumn && IsTableData(dataType))
            {
                var idProperty = dataType.GetProperty("Id");
                if (idProperty != null)
                {
                    var idCell = row[cellIndex];
                    BindEditableCell(idCell, item, idProperty);
                }
                cellIndex++;
            }

            // Bind data cells
            foreach (var column in columns)
            {
                if (column.Name == "Id" && ShowIdColumn) continue;

                var cell = row[cellIndex];
                BindEditableCell(cell, item, column);
                cellIndex++;
            }

            // Apply modified state
            var itemKey = GetKeyFromItem(item);
            if (itemKey != null && changeTracker != null)
            {
                var modifiedProps = changeTracker.GetModifiedProperties(itemKey);
                foreach (var propName in modifiedProps)
                {
                    // Find cell for this property and mark as modified
                    var cellForProp = row.Q($"cell-{propName}");
                    if (cellForProp != null)
                    {
                        var field = cellForProp.Q<DatraPropertyField>();
                        field?.SetModified(true);
                    }
                }
            }
        }

        private void BindEditableCell(VisualElement cell, object item, PropertyInfo property)
        {
            cell.Clear();

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
                // Create editable field
                var field = new DatraPropertyField(item, property, DatraFieldLayoutMode.Table);

                field.OnValueChanged += (propName, newValue) => {
                    var itemKey = GetKeyFromItem(item);
                    changeTracker.TrackPropertyChange(itemKey, propName, newValue, out bool isModified);
                    field.SetModified(isModified);
                    UpdateModifiedState();

                    // Update row state without rebuilding (to avoid interrupting typing)
                    UpdateRowStateVisuals(item);
                };

                field.OnRevertRequested += (propName) => {
                    var itemKey = GetKeyFromItem(item);
                    if (itemKey == null) return;

                    var baselineValue = changeTracker.GetPropertyBaselineValue(itemKey, propName);
                    property.SetValue(item, baselineValue);
                    UpdateFieldValue(field, property.PropertyType, baselineValue);
                    changeTracker.TrackPropertyChange(itemKey, propName, baselineValue, out bool isModified);
                    field.SetModified(isModified);
                    UpdateModifiedState();
                };

                cell.Add(field);
            }
        }

        protected override bool MatchesFilter(object item, string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm)) return true;

            foreach (var column in columns)
            {
                var value = column.GetValue(item)?.ToString() ?? "";
                if (value.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        protected override void OnAddButtonClicked()
        {
            AddNewItem();
        }

        protected override void ShowViewOptions()
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
            Debug.Log("Export to CSV - Not implemented yet");
        }

        private string GetDisplayValue(Type type, object value)
        {
            if (value == null) return "";

            if (type.IsArray)
            {
                var array = value as Array;
                return $"[{array?.Length ?? 0} items]";
            }

            if (type.IsGenericType &&
                (type.GetGenericTypeDefinition() == typeof(StringDataRef<>) ||
                 type.GetGenericTypeDefinition() == typeof(IntDataRef<>)))
            {
                var keyValue = type.GetProperty("Value")?.GetValue(value);
                return keyValue != null ? $"→ {keyValue}" : "(None)";
            }

            return value.ToString();
        }

        // Header cell creation with resize handles
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

            // Add resize handles
            var resizeHandleRight = CreateResizeHandle("resize-handle-right");
            var resizeHandleLeft = CreateResizeHandle("resize-handle-left");

            resizeHandleRight.RegisterCallback<MouseDownEvent>(evt => StartResize(evt, cell));
            resizeHandleLeft.RegisterCallback<MouseDownEvent>(evt => {
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

        private VisualElement CreateResizeHandle(string className)
        {
            var handle = new VisualElement();
            handle.AddToClassList("table-resize-handle");
            handle.AddToClassList("resize-handle");
            handle.AddToClassList(className);
            handle.pickingMode = PickingMode.Position;

            handle.RegisterCallback<MouseEnterEvent>(evt => {
                handle.style.cursor = new Cursor() { hotspot = Vector2.zero };
                handle.style.backgroundColor = new Color(0.5f, 0.7f, 1f, 0.3f);
            });

            handle.RegisterCallback<MouseLeaveEvent>(evt => {
                handle.style.cursor = StyleKeyword.Null;
                handle.style.backgroundColor = Color.clear;
            });

            return handle;
        }

        private void StartResize(MouseDownEvent evt, VisualElement cell)
        {
            evt.StopPropagation();

            isResizing = true;
            resizingColumn = cell;
            resizeStartX = evt.mousePosition.x;
            resizeStartWidth = cell.style.width.value.value;

            cell.CaptureMouse();
            cell.RegisterCallback<MouseMoveEvent>(OnResizeMove);
            cell.RegisterCallback<MouseUpEvent>(OnResizeEnd);

            var root = GetRootVisualElement();
            if (root != null)
                root.style.cursor = new Cursor() { texture = null };
        }

        private void OnResizeMove(MouseMoveEvent evt)
        {
            if (!isResizing || resizingColumn == null) return;

            evt.StopPropagation();

            float deltaX = evt.mousePosition.x - resizeStartX;
            float newWidth = Mathf.Max(50, resizeStartWidth + deltaX);

            resizingColumn.style.width = newWidth;
            resizingColumn.style.minWidth = newWidth;

            // Update corresponding cells in ListView
            int columnIndex = headerRow.IndexOf(resizingColumn);
            if (columnIndex >= 0)
            {
                // Update all visible rows in the ListView
                var visibleRows = listView.Query<VisualElement>(className: "table-row").ToList();
                foreach (var row in visibleRows)
                {
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

            if (resizingColumn != null)
            {
                resizingColumn.ReleaseMouse();
                resizingColumn.UnregisterCallback<MouseMoveEvent>(OnResizeMove);
                resizingColumn.UnregisterCallback<MouseUpEvent>(OnResizeEnd);
            }

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

            // Update add button
            var addButton = headerContainer.Q<Button>(className: "table-add-button");
            addButton?.SetEnabled(!isReadOnly);

            // Update delete buttons in visible rows
            var deleteButtons = listView?.Query<Button>(className: "table-delete-button").ToList();
            if (deleteButtons != null)
            {
                foreach (var button in deleteButtons)
                {
                    button.SetEnabled(!isReadOnly);
                }
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

            // Rebuild ListView to reflect reverted changes
            listView.Rebuild();
        }
    }
}
