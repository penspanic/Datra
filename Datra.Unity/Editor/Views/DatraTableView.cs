using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.Unity.Editor.Components;
using Datra.Unity.Editor.UI;
using DatraPropertyField = Datra.Unity.Editor.Components.DatraPropertyField;
using Datra.DataTypes;
using Datra.Interfaces;
using Datra.Unity.Editor.Utilities;
using Datra.Editor.Interfaces;
using Datra.Editor.Models;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;

namespace Datra.Unity.Editor.Views
{
    /// <summary>
    /// Represents a column in the table (either a direct property or a nested property field)
    /// </summary>
    internal class ColumnInfo
    {
        public PropertyInfo Property { get; set; }        // The parent property
        public MemberInfo NestedMember { get; set; }      // Nested field/property (null for direct properties)
        public string DisplayName { get; set; }           // Display name in header
        public string ColumnName { get; set; }            // Column identifier (e.g., "TestPooledPrefab.Path")

        public bool IsNestedColumn => NestedMember != null;

        public object GetValue(object item)
        {
            var parentValue = Property.GetValue(item);
            if (!IsNestedColumn || parentValue == null)
                return parentValue;

            return NestedMember switch
            {
                FieldInfo field => field.GetValue(parentValue),
                PropertyInfo prop => prop.GetValue(parentValue),
                _ => null
            };
        }

        public void SetValue(object item, object value)
        {
            if (!IsNestedColumn)
            {
                Property.SetValue(item, value);
                return;
            }

            var parentValue = Property.GetValue(item);
            if (parentValue == null)
            {
                // Create instance for value types (structs)
                if (Property.PropertyType.IsValueType)
                {
                    parentValue = Activator.CreateInstance(Property.PropertyType);
                }
                else
                {
                    return; // Cannot set nested value on null parent
                }
            }

            switch (NestedMember)
            {
                case FieldInfo field:
                    field.SetValue(parentValue, value);
                    break;
                case PropertyInfo prop:
                    prop.SetValue(parentValue, value);
                    break;
            }

            // For structs, we need to write back to the parent property
            if (Property.PropertyType.IsValueType)
            {
                Property.SetValue(item, parentValue);
            }
        }

        public Type GetValueType()
        {
            if (!IsNestedColumn)
                return Property.PropertyType;

            return NestedMember switch
            {
                FieldInfo field => field.FieldType,
                PropertyInfo prop => prop.PropertyType,
                _ => typeof(object)
            };
        }
    }

    /// <summary>
    /// Virtualized table view for generic data types
    /// </summary>
    public class DatraTableView : VirtualizedTableView
    {
        // Properties
        private List<PropertyInfo> columns;
        private List<ColumnInfo> expandedColumns;  // Columns with nested types expanded
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
            expandedColumns = new List<ColumnInfo>();
        }

        public override void SetData(
            Type type,
            IDataRepository repo,
            IDataContext context,
            IRepositoryChangeTracker tracker,
            Datra.Services.LocalizationContext localizationCtx = null,
            Utilities.LocalizationChangeTracker localizationTracker = null)
        {
            // Only reset modification state if switching to a different data type
            bool isDifferentType = dataType != type;

            // Don't call base.SetData - we need to initialize columns first
            dataType = type;
            repository = repo;
            dataContext = context;
            changeTracker = tracker;
            localizationContext = localizationCtx;
            localizationChangeTracker = localizationTracker;

            if (isDifferentType)
            {
                hasUnsavedChanges = false;
            }

            // Get columns (properties) BEFORE calling RefreshContent
            columns = GetFilteredProperties(type);

            // Build expanded columns list (with nested types expanded to individual columns)
            BuildExpandedColumns();

            // Now cleanup and refresh
            CleanupFields();
            RefreshContent();
            UpdateFooter();
        }

        private void BuildExpandedColumns()
        {
            expandedColumns.Clear();

            foreach (var property in columns)
            {
                if (IsNestedType(property.PropertyType))
                {
                    // Expand nested type to multiple columns
                    var nestedMembers = GetNestedTypeMembers(property.PropertyType);
                    foreach (var member in nestedMembers)
                    {
                        var memberName = member switch
                        {
                            FieldInfo f => f.Name,
                            PropertyInfo p => p.Name,
                            _ => "Unknown"
                        };

                        expandedColumns.Add(new ColumnInfo
                        {
                            Property = property,
                            NestedMember = member,
                            DisplayName = $"{ObjectNames.NicifyVariableName(property.Name)}.{ObjectNames.NicifyVariableName(memberName)}",
                            ColumnName = $"{property.Name}.{memberName}"
                        });
                    }
                }
                else
                {
                    // Regular column
                    expandedColumns.Add(new ColumnInfo
                    {
                        Property = property,
                        NestedMember = null,
                        DisplayName = ObjectNames.NicifyVariableName(property.Name),
                        ColumnName = property.Name
                    });
                }
            }
        }

        private bool IsNestedType(Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return false;
            if (type.IsArray || type.IsEnum)
                return false;
            if (type.Namespace != null && type.Namespace.StartsWith("System"))
                return false;
            if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine"))
                return false;
            // Check for DataRef types
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                if (genericDef.Name.Contains("DataRef"))
                    return false;
            }
            // Check for LocaleRef
            if (type == typeof(LocaleRef))
                return false;

            return type.IsValueType || type.IsClass;
        }

        private List<MemberInfo> GetNestedTypeMembers(Type type)
        {
            var members = new List<MemberInfo>();

            // Get public fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                // Skip backing fields
                if (field.Name.Contains("<") || field.Name.Contains(">"))
                    continue;
                members.Add(field);
            }

            // Get public properties with setter
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.CanWrite && prop.CanRead && prop.GetIndexParameters().Length == 0)
                {
                    // Skip if there's already a field with similar name
                    var fieldExists = fields.Any(f => f.Name.Equals(prop.Name, StringComparison.OrdinalIgnoreCase));
                    if (!fieldExists)
                    {
                        members.Add(prop);
                    }
                }
            }

            return members;
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

            // Data columns (using expanded columns for nested types)
            foreach (var colInfo in expandedColumns)
            {
                if (colInfo.ColumnName == "Id" && ShowIdColumn) continue;

                var headerCell = CreateHeaderCell(colInfo.DisplayName, DefaultColumnWidth);
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

            // Data cells (using expanded columns for nested types)
            foreach (var colInfo in expandedColumns)
            {
                if (colInfo.ColumnName == "Id" && ShowIdColumn) continue;

                var cell = new VisualElement();
                cell.AddToClassList("table-cell");
                cell.AddToClassList("editable-cell");
                cell.style.width = DefaultColumnWidth;
                cell.style.minWidth = DefaultColumnWidth;
                cell.name = $"cell-{colInfo.ColumnName}";
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

            // Bind data cells (using expanded columns for nested types)
            foreach (var colInfo in expandedColumns)
            {
                if (colInfo.ColumnName == "Id" && ShowIdColumn) continue;

                var cell = row[cellIndex];
                BindEditableCellForColumnInfo(cell, item, colInfo);
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

        private void BindEditableCellForColumnInfo(VisualElement cell, object item, ColumnInfo colInfo)
        {
            cell.Clear();

            if (colInfo.IsNestedColumn)
            {
                // Nested column - create direct field for the nested member
                BindNestedMemberCell(cell, item, colInfo);
            }
            else
            {
                // Regular column - use existing method
                BindEditableCell(cell, item, colInfo.Property);
            }
        }

        private void BindNestedMemberCell(VisualElement cell, object item, ColumnInfo colInfo)
        {
            var valueType = colInfo.GetValueType();
            var value = colInfo.GetValue(item);

            if (isReadOnly)
            {
                var displayValue = GetDisplayValue(valueType, value);
                var label = new Label(displayValue);
                cell.Add(label);
                return;
            }

            // Create appropriate field based on type
            VisualElement field = null;

            if (valueType == typeof(string))
            {
                // Check for asset attributes on the nested member
                if (colInfo.NestedMember is FieldInfo fieldInfo &&
                    Utilities.AttributeFieldHandler.HasAssetAttributes(fieldInfo))
                {
                    var assetType = Utilities.AttributeFieldHandler.GetAssetTypeAttribute(fieldInfo);
                    var folderPath = Utilities.AttributeFieldHandler.GetFolderPathAttribute(fieldInfo);

                    field = new AssetFieldElement(assetType, folderPath, value as string ?? "", (newValue) =>
                    {
                        colInfo.SetValue(item, newValue);
                        TrackNestedPropertyChange(item, colInfo, newValue);
                    }, true);
                }
                else
                {
                    var textField = new TextField();
                    textField.value = value as string ?? "";
                    textField.RegisterValueChangedCallback(evt =>
                    {
                        colInfo.SetValue(item, evt.newValue);
                        TrackNestedPropertyChange(item, colInfo, evt.newValue);
                    });
                    field = textField;
                }
            }
            else if (valueType == typeof(int))
            {
                var intField = new IntegerField();
                intField.value = value != null ? Convert.ToInt32(value) : 0;
                intField.RegisterValueChangedCallback(evt =>
                {
                    colInfo.SetValue(item, evt.newValue);
                    TrackNestedPropertyChange(item, colInfo, evt.newValue);
                });
                field = intField;
            }
            else if (valueType == typeof(float))
            {
                var floatField = new FloatField();
                floatField.value = value != null ? Convert.ToSingle(value) : 0f;
                floatField.RegisterValueChangedCallback(evt =>
                {
                    colInfo.SetValue(item, evt.newValue);
                    TrackNestedPropertyChange(item, colInfo, evt.newValue);
                });
                field = floatField;
            }
            else if (valueType == typeof(bool))
            {
                var toggle = new Toggle();
                toggle.value = value != null && Convert.ToBoolean(value);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    colInfo.SetValue(item, evt.newValue);
                    TrackNestedPropertyChange(item, colInfo, evt.newValue);
                });
                field = toggle;
            }
            else if (valueType.IsEnum)
            {
                var enumField = new EnumField((Enum)(value ?? Enum.GetValues(valueType).GetValue(0)));
                enumField.RegisterValueChangedCallback(evt =>
                {
                    colInfo.SetValue(item, evt.newValue);
                    TrackNestedPropertyChange(item, colInfo, evt.newValue);
                });
                field = enumField;
            }
            else
            {
                // Fallback to label
                var displayValue = GetDisplayValue(valueType, value);
                var label = new Label(displayValue);
                cell.Add(label);
                return;
            }

            if (field != null)
            {
                field.AddToClassList("table-field");
                cell.Add(field);
            }
        }

        private void TrackNestedPropertyChange(object item, ColumnInfo colInfo, object newValue)
        {
            var itemKey = GetKeyFromItem(item);
            // Track at the parent property level for change tracking
            var parentValue = colInfo.Property.GetValue(item);
            changeTracker.TrackPropertyChange(itemKey, colInfo.Property.Name, parentValue, out bool isModified);
            UpdateModifiedState();
            UpdateRowStateVisuals(item);
        }

        private void BindEditableCell(VisualElement cell, object item, PropertyInfo property)
        {
            cell.Clear();

            if (isReadOnly || !DatraPropertyField.CanHandle(property, this))
            {
                // Read-only display
                var value = property.GetValue(item);
                var displayValue = GetDisplayValue(property.PropertyType, value);
                var label = new Label(displayValue);
                cell.Add(label);
            }
            else
            {
                // Create editable field (pass this as ILocaleProvider for FixedLocale support)
                var field = new DatraPropertyField(item, property, FieldLayoutMode.Table, this);

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

            // Search through expanded columns (including nested type fields)
            foreach (var colInfo in expandedColumns)
            {
                var value = colInfo.GetValue(item)?.ToString() ?? "";
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

            // Handle generic collection types
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();

                // DataRef types
                if (genericDef == typeof(StringDataRef<>) || genericDef == typeof(IntDataRef<>))
                {
                    var keyValue = type.GetProperty("Value")?.GetValue(value);
                    return keyValue != null ? $"→ {keyValue}" : "(None)";
                }

                // List<T>
                if (genericDef == typeof(List<>))
                {
                    var countProp = type.GetProperty("Count");
                    var count = countProp?.GetValue(value) ?? 0;
                    return $"[{count} items]";
                }

                // Dictionary<K,V>
                if (genericDef == typeof(Dictionary<,>))
                {
                    var countProp = type.GetProperty("Count");
                    var count = countProp?.GetValue(value) ?? 0;
                    return $"{{{count} entries}}";
                }
            }

            // Handle ICollection interface (catches other collection types)
            if (value is System.Collections.ICollection collection)
            {
                return $"[{collection.Count} items]";
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
