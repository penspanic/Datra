using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.Interfaces;

namespace Datra.Unity.Editor.Panels
{
    public class DatraInspectorPanel : VisualElement
    {
        private VisualElement headerContainer;
        private VisualElement breadcrumbContainer;
        private VisualElement contentContainer;
        private ScrollView contentScrollView;
        private VisualElement footerContainer;
        
        private Label titleLabel;
        private Label subtitleLabel;
        private Label statusLabel;
        private Button saveButton;
        private Button revertButton;
        
        private Type currentType;
        private object currentRepository;
        private object currentDataContext;
        private bool hasUnsavedChanges = false;
        
        // Properties
        public Type CurrentType => currentType;
        public object CurrentRepository => currentRepository;
        
        // Events
        public event Action<Type> OnDataModified;
        public event Action OnSaveRequested;
        
        public DatraInspectorPanel()
        {
            AddToClassList("datra-inspector-panel");
            Initialize();
        }
        
        private void Initialize()
        {
            // Header Section
            headerContainer = new VisualElement();
            headerContainer.AddToClassList("inspector-header");
            
            // Breadcrumb navigation
            breadcrumbContainer = new VisualElement();
            breadcrumbContainer.AddToClassList("breadcrumb-container");
            headerContainer.Add(breadcrumbContainer);
            
            // Title section
            var titleSection = new VisualElement();
            titleSection.AddToClassList("title-section");
            
            titleLabel = new Label();
            titleLabel.AddToClassList("inspector-title");
            titleSection.Add(titleLabel);
            
            subtitleLabel = new Label();
            subtitleLabel.AddToClassList("inspector-subtitle");
            titleSection.Add(subtitleLabel);
            
            headerContainer.Add(titleSection);
            
            // Action buttons in header
            var headerActions = new VisualElement();
            headerActions.AddToClassList("header-actions");
            
            var refreshButton = new Button(() => RefreshContent());
            refreshButton.text = "↻";
            refreshButton.tooltip = "Refresh";
            refreshButton.AddToClassList("icon-button");
            headerActions.Add(refreshButton);
            
            headerContainer.Add(headerActions);
            
            Add(headerContainer);
            
            // Content Section
            contentScrollView = new ScrollView();
            contentScrollView.AddToClassList("inspector-content-scroll");
            
            contentContainer = new VisualElement();
            contentContainer.AddToClassList("inspector-content");
            contentScrollView.Add(contentContainer);
            
            Add(contentScrollView);
            
            // Footer Section
            footerContainer = new VisualElement();
            footerContainer.AddToClassList("inspector-footer");
            
            // Status area
            var statusArea = new VisualElement();
            statusArea.AddToClassList("status-area");
            
            statusLabel = new Label();
            statusLabel.AddToClassList("status-label");
            statusArea.Add(statusLabel);
            
            footerContainer.Add(statusArea);
            
            // Action buttons
            var actionArea = new VisualElement();
            actionArea.AddToClassList("action-area");
            
            revertButton = new Button(() => RevertChanges());
            revertButton.text = "Revert";
            revertButton.AddToClassList("secondary-button");
            revertButton.SetEnabled(false);
            actionArea.Add(revertButton);
            
            saveButton = new Button(() => SaveChanges());
            saveButton.text = "Save";
            saveButton.AddToClassList("primary-button");
            saveButton.SetEnabled(false);
            actionArea.Add(saveButton);
            
            footerContainer.Add(actionArea);
            
            Add(footerContainer);
            
            // Show empty state initially
            ShowEmptyState();
        }
        
        public void SetDataContext(object dataContext, object repository, Type dataType)
        {
            currentDataContext = dataContext;
            currentRepository = repository;
            currentType = dataType;
            hasUnsavedChanges = false;
            
            UpdateHeader();
            RefreshContent();
            UpdateFooter();
        }
        
        private void UpdateHeader()
        {
            if (currentType == null)
            {
                titleLabel.text = "No Selection";
                subtitleLabel.text = "";
                breadcrumbContainer.Clear();
                return;
            }
            
            // Update title
            titleLabel.text = currentType.Name;
            
            // Update subtitle with additional info
            var isTableData = IsTableData(currentType);
            subtitleLabel.text = isTableData ? "Table Data" : "Single Data";
            
            // Update breadcrumb
            UpdateBreadcrumb();
        }
        
        private void UpdateBreadcrumb()
        {
            breadcrumbContainer.Clear();
            
            var homeButton = new Button(() => ShowEmptyState());
            homeButton.text = "Data Types";
            homeButton.AddToClassList("breadcrumb-item");
            breadcrumbContainer.Add(homeButton);
            
            var separator = new Label("›");
            separator.AddToClassList("breadcrumb-separator");
            breadcrumbContainer.Add(separator);
            
            var currentLabel = new Label(currentType.Name);
            currentLabel.AddToClassList("breadcrumb-current");
            breadcrumbContainer.Add(currentLabel);
        }
        
        public void RefreshContent()
        {
            contentContainer.Clear();
            
            if (currentRepository == null || currentType == null)
            {
                ShowEmptyState();
                return;
            }
            
            if (IsTableData(currentType))
            {
                DisplayTableData();
            }
            else
            {
                DisplaySingleData();
            }
        }
        
        private void ShowEmptyState()
        {
            contentContainer.Clear();
            
            var emptyState = new VisualElement();
            emptyState.AddToClassList("empty-state");
            
            var icon = new VisualElement();
            icon.AddToClassList("empty-state-icon");
            emptyState.Add(icon);
            
            var message = new Label("Select a data type to view and edit");
            message.AddToClassList("empty-state-message");
            emptyState.Add(message);
            
            contentContainer.Add(emptyState);
            
            titleLabel.text = "Inspector";
            subtitleLabel.text = "";
            breadcrumbContainer.Clear();
        }
        
        private bool IsTableData(Type type)
        {
            var interfaces = type.GetInterfaces();
            return interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITableData<>));
        }
        
        private void DisplayTableData()
        {
            // Add toolbar for table data
            var toolbar = new VisualElement();
            toolbar.AddToClassList("table-toolbar");
            
            var addButton = new Button(() => AddNewItem());
            addButton.text = "➕ Add New Item";
            addButton.AddToClassList("add-button");
            toolbar.Add(addButton);
            
            var searchField = new TextField();
            searchField.label = "Search items...";
            searchField.AddToClassList("table-search");
            searchField.RegisterValueChangedCallback(evt => FilterTableItems(evt.newValue));
            toolbar.Add(searchField);
            
            contentContainer.Add(toolbar);
            
            // Get all items from repository
            var getAllMethod = currentRepository.GetType().GetMethod("GetAll");
            var items = getAllMethod.Invoke(currentRepository, null) as System.Collections.IEnumerable;
            
            if (items != null)
            {
                var itemsContainer = new VisualElement();
                itemsContainer.AddToClassList("table-items-container");
                
                int index = 0;
                foreach (var item in items)
                {
                    var itemElement = CreateTableItemElement(item, index++);
                    itemsContainer.Add(itemElement);
                }
                
                contentContainer.Add(itemsContainer);
            }
        }
        
        private void DisplaySingleData()
        {
            var getMethod = currentRepository.GetType().GetMethod("Get");
            var data = getMethod.Invoke(currentRepository, null);
            
            if (data != null)
            {
                var formContainer = new VisualElement();
                formContainer.AddToClassList("single-data-form");
                
                AddPropertiesFields(data, formContainer, false);
                contentContainer.Add(formContainer);
            }
        }
        
        private VisualElement CreateTableItemElement(object item, int index)
        {
            var itemContainer = new VisualElement();
            itemContainer.AddToClassList("table-item");
            
            // Extract actual data from KeyValuePair
            object actualData = item;
            if (item != null)
            {
                var itemType = item.GetType();
                if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    var valueProperty = itemType.GetProperty("Value");
                    actualData = valueProperty?.GetValue(item);
                }
            }
            
            // Item header
            var itemHeader = new VisualElement();
            itemHeader.AddToClassList("table-item-header");
            
            // Expandable toggle
            var foldout = new Foldout();
            foldout.AddToClassList("table-item-foldout");
            
            // Get ID for display
            var idProperty = actualData?.GetType().GetProperty("Id");
            var idValue = idProperty?.GetValue(actualData)?.ToString() ?? "Unknown";
            
            // Get name if available
            var nameProperty = actualData?.GetType().GetProperty("Name");
            var nameValue = nameProperty?.GetValue(actualData)?.ToString() ?? "";
            
            var displayText = string.IsNullOrEmpty(nameValue) 
                ? $"Item {index + 1} (ID: {idValue})"
                : $"{nameValue} (ID: {idValue})";
                
            foldout.text = displayText;
            foldout.value = false; // Collapsed by default
            
            // Delete button
            var deleteButton = new Button(() => DeleteItem(item));
            deleteButton.text = "🗑";
            deleteButton.tooltip = "Delete Item";
            deleteButton.AddToClassList("delete-button");
            foldout.Q<Toggle>().Add(deleteButton);
            
            // Fields container
            var fieldsContainer = new VisualElement();
            fieldsContainer.AddToClassList("table-item-fields");
            
            if (actualData != null)
            {
                AddPropertiesFields(actualData, fieldsContainer, true);
            }
            
            foldout.Add(fieldsContainer);
            itemContainer.Add(foldout);
            
            return itemContainer;
        }
        
        private void AddPropertiesFields(object data, VisualElement container, bool skipId = true)
        {
            var properties = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                if (property.CanWrite && (!skipId || property.Name != "Id"))
                {
                    var fieldContainer = CreatePropertyField(data, property);
                    container.Add(fieldContainer);
                }
            }
        }
        
        private VisualElement CreatePropertyField(object target, PropertyInfo property)
        {
            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("property-field");
            
            var label = new Label(ObjectNames.NicifyVariableName(property.Name));
            label.AddToClassList("property-label");
            fieldContainer.Add(label);
            
            var value = property.GetValue(target);
            var propertyType = property.PropertyType;
            
            VisualElement field = null;
            
            // Handle different property types
            if (propertyType == typeof(string))
            {
                var textField = new TextField();
                textField.value = value as string ?? "";
                textField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    MarkAsModified();
                });
                field = textField;
            }
            else if (propertyType == typeof(int))
            {
                var intField = new IntegerField();
                intField.value = (int)(value ?? 0);
                intField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    MarkAsModified();
                });
                field = intField;
            }
            else if (propertyType == typeof(float))
            {
                var floatField = new FloatField();
                floatField.value = (float)(value ?? 0f);
                floatField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    MarkAsModified();
                });
                field = floatField;
            }
            else if (propertyType == typeof(bool))
            {
                var toggle = new Toggle();
                toggle.value = (bool)(value ?? false);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    MarkAsModified();
                });
                field = toggle;
            }
            else if (propertyType.IsEnum)
            {
                var enumField = new EnumField((Enum)value);
                enumField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    MarkAsModified();
                });
                field = enumField;
            }
            else
            {
                // For unsupported types, show read-only field
                var readOnlyField = new TextField();
                readOnlyField.value = value?.ToString() ?? "null";
                readOnlyField.isReadOnly = true;
                readOnlyField.AddToClassList("unsupported-field");
                field = readOnlyField;
                
                // Add type info
                var typeInfo = new Label($"({propertyType.Name})");
                typeInfo.AddToClassList("type-info");
                fieldContainer.Add(typeInfo);
            }
            
            if (field != null)
            {
                field.AddToClassList("property-input");
                fieldContainer.Add(field);
            }
            
            return fieldContainer;
        }
        
        private void MarkAsModified()
        {
            if (!hasUnsavedChanges)
            {
                hasUnsavedChanges = true;
                UpdateFooter();
                OnDataModified?.Invoke(currentType);
            }
        }
        
        private void UpdateFooter()
        {
            saveButton.SetEnabled(hasUnsavedChanges);
            revertButton.SetEnabled(hasUnsavedChanges);
            
            if (hasUnsavedChanges)
            {
                statusLabel.text = "Modified - unsaved changes";
                statusLabel.AddToClassList("status-warning");
            }
            else
            {
                statusLabel.text = "Up to date";
                statusLabel.RemoveFromClassList("status-warning");
            }
        }
        
        private void AddNewItem()
        {
            UpdateStatus("Add new item functionality not yet implemented");
        }
        
        private void DeleteItem(object item)
        {
            if (EditorUtility.DisplayDialog("Delete Item", 
                "Are you sure you want to delete this item?", 
                "Delete", "Cancel"))
            {
                UpdateStatus("Delete item functionality not yet implemented");
                MarkAsModified();
            }
        }
        
        private void FilterTableItems(string searchTerm)
        {
            // Implementation for filtering table items
            var items = contentContainer.Query<VisualElement>(className: "table-item").ToList();
            
            foreach (var item in items)
            {
                var foldout = item.Q<Foldout>();
                if (foldout != null)
                {
                    var text = foldout.text.ToLower();
                    item.style.display = text.Contains(searchTerm.ToLower()) 
                        ? DisplayStyle.Flex 
                        : DisplayStyle.None;
                }
            }
        }
        
        private void SaveChanges()
        {
            OnSaveRequested?.Invoke();
            hasUnsavedChanges = false;
            UpdateFooter();
            UpdateStatus("Changes saved successfully");
        }
        
        private void RevertChanges()
        {
            if (EditorUtility.DisplayDialog("Revert Changes", 
                "Are you sure you want to revert all changes?", 
                "Revert", "Cancel"))
            {
                RefreshContent();
                hasUnsavedChanges = false;
                UpdateFooter();
                UpdateStatus("Changes reverted");
            }
        }
        
        private void UpdateStatus(string message)
        {
            statusLabel.text = message;
        }
    }
}