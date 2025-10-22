using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.Unity.Editor.Components;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Views
{
    public class DatraFormView : DatraDataView
    {
        private VisualElement formContainer;
        private VisualElement itemsContainer;
        private ScrollView scrollView;
        private VisualElement scrollContent;

        public DatraFormView() : base()
        {
            AddToClassList("datra-form-view");
        }

        protected override void InitializeView()
        {
            // Header already created by base class
            // Add any form-specific header elements if needed

            // Create ScrollView for form content
            scrollView = new ScrollView();
            scrollView.AddToClassList("form-view-scroll");
            scrollView.AddToClassList("form-scroll-view");
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            // Create scroll content container
            scrollContent = new VisualElement();
            scrollContent.AddToClassList("form-view-scroll-content");
            scrollView.Add(scrollContent);

            // Add ScrollView to the main content container
            contentContainer.Add(scrollView);
        }
        
        public override void RefreshContent()
        {
            // Clear the scroll content, not the main container
            scrollContent.Clear();

            if (repository == null || dataType == null) return;

            if (IsTableData(dataType))
            {
                DisplayTableDataAsForm();
            }
            else
            {
                DisplaySingleDataForm();
            }

            // Update modification state after refresh (to show orange dot if there are modifications)
            UpdateModifiedState();
        }
        
        private void DisplaySingleDataForm()
        {
            var getMethod = repository.GetType().GetMethod("Get");
            var data = getMethod?.Invoke(repository, null);
            
            if (data != null)
            {
                formContainer = new VisualElement();
                formContainer.AddToClassList("single-data-form");

                // Create fields using the field factory
                var fields = DatraFieldFactory.CreateFieldsForObject(data, DatraFieldLayoutMode.Form, false, this);
                foreach (var field in fields)
                {
                    field.OnValueChanged += (propName, value) => {
                        // Track in external change tracker
                        var dataKey = GetKeyFromItem(data);
                        if (dataKey != null)
                        {
                            changeTracker.TrackChange(dataKey, data);
                        }

                        UpdateModifiedState();
                    };
                    formContainer.Add(field);
                    activeFields.Add(field);
                }

                scrollContent.Add(formContainer);
            }
        }
        
        private void DisplayTableDataAsForm()
        {
            // Add toolbar for table data
            var toolbar = CreateTableToolbar();
            scrollContent.Add(toolbar);
            
            // Get all items from repository
            var getAllMethod = repository.GetType().GetMethod("GetAll");
            var items = getAllMethod?.Invoke(repository, null) as System.Collections.IEnumerable;
            
            if (items != null)
            {
                itemsContainer = new VisualElement();
                itemsContainer.AddToClassList("table-items-container");
                
                int index = 0;
                foreach (var item in items)
                {
                    var itemElement = CreateTableItemElement(item, index++);
                    itemsContainer.Add(itemElement);
                }
                
                scrollContent.Add(itemsContainer);
            }
        }
        
        private VisualElement CreateTableToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("table-toolbar");
            toolbar.AddToClassList("form-toolbar");

            var addButton = new Button(() => base.AddNewItem());
            addButton.text = "➕ Add New Item";
            addButton.AddToClassList("add-button");
            addButton.AddToClassList("form-add-button");
            toolbar.Add(addButton);

            this.searchField = new TextField();
            //searchField.placeholder = "Search items...";
            this.searchField.AddToClassList("table-search");
            this.searchField.AddToClassList("form-search");
            (searchField as TextField).RegisterValueChangedCallback(evt => FilterItems(evt.newValue));
            toolbar.Add(this.searchField);

            return toolbar;
        }
        
        private VisualElement CreateTableItemElement(object item, int index)
        {
            var itemContainer = new VisualElement();
            itemContainer.AddToClassList("table-item");
            itemContainer.userData = item;
            
            // Extract actual data from KeyValuePair
            object actualData = ExtractActualData(item);
            
            // Item header
            var itemHeader = new VisualElement();
            itemHeader.AddToClassList("table-item-header");
            
            // Expandable toggle
            var foldout = new Foldout();
            foldout.AddToClassList("table-item-foldout");
            
            // Get ID and name properties
            var idProperty = actualData?.GetType().GetProperty("Id");
            var currentId = idProperty?.GetValue(actualData);
            var nameProperty = actualData?.GetType().GetProperty("Name");
            var nameValue = nameProperty?.GetValue(actualData)?.ToString() ?? "";
            
            // Create header controls container
            var headerControls = new VisualElement();
            headerControls.AddToClassList("table-item-controls");
            headerControls.AddToClassList("form-header-controls");

            // ID field
            if (idProperty != null && idProperty.CanWrite && !isReadOnly)
            {
                var idFieldContainer = CreateIdField(actualData, item, idProperty, currentId);
                headerControls.Add(idFieldContainer);
            }

            // Delete button
            if (!isReadOnly)
            {
                var deleteButton = new Button(() => {
                    base.DeleteItem(item);
                });
                deleteButton.text = "🗑";
                deleteButton.tooltip = "Delete Item";
                deleteButton.AddToClassList("delete-button");
                deleteButton.AddToClassList("form-delete-button");
                headerControls.Add(deleteButton);
            }
            
            foldout.Q<Toggle>().Add(headerControls);
            
            // Update foldout text
            void UpdateFoldoutText()
            {
                var id = idProperty?.GetValue(actualData)?.ToString() ?? "Unknown";
                var name = nameProperty?.GetValue(actualData)?.ToString() ?? "";
                foldout.text = string.IsNullOrEmpty(name) 
                    ? $"Item {index + 1}"
                    : name;
            }
            
            UpdateFoldoutText();
            foldout.value = false; // Collapsed by default
            
            // Fields container
            var fieldsContainer = new VisualElement();
            fieldsContainer.AddToClassList("table-item-fields");
            
            if (actualData != null)
            {
                // Create fields (skip ID since we handle it in header)
                var fields = DatraFieldFactory.CreateFieldsForObject(actualData, DatraFieldLayoutMode.Form, true, this);
                foreach (var field in fields)
                {
                    field.OnValueChanged += (propName, value) => {
                        // Track in external change tracker
                        var itemKey = GetKeyFromItem(actualData);
                        if (itemKey != null)
                        {
                            changeTracker.TrackChange(itemKey, actualData);
                        }

                        UpdateModifiedState();
                    };
                    field.SetEnabled(!isReadOnly);
                    fieldsContainer.Add(field);
                    activeFields.Add(field);
                }
            }
            
            foldout.Add(fieldsContainer);
            itemContainer.Add(foldout);
            
            return itemContainer;
        }
        
        private VisualElement CreateIdField(object actualData, object item, PropertyInfo idProperty, object currentId)
        {
            var idFieldContainer = new VisualElement();
            idFieldContainer.AddToClassList("id-field-container");
            idFieldContainer.AddToClassList("form-id-field-container");

            var idLabel = new Label("ID:");
            idLabel.AddToClassList("id-field-label");
            idLabel.AddToClassList("form-id-label");
            idFieldContainer.Add(idLabel);

            if (idProperty.PropertyType == typeof(int))
            {
                var idField = new IntegerField();
                idField.value = (int)(currentId ?? 0);
                idField.AddToClassList("id-field");
                idField.AddToClassList("form-id-field-int");
                idField.RegisterValueChangedCallback(evt =>
                {
                    var oldKey = GetKeyFromItem(item);
                    if (base.ValidateId(evt.newValue, oldKey, idField))
                    {
                        idProperty.SetValue(actualData, evt.newValue);
                        base.OnIdChanged(item, oldKey, evt.newValue);
                    }
                });
                idFieldContainer.Add(idField);
            }
            else if (idProperty.PropertyType == typeof(string))
            {
                var idField = new TextField();
                idField.value = currentId as string ?? "";
                idField.AddToClassList("id-field");
                idField.AddToClassList("form-id-field-string");
                idField.RegisterValueChangedCallback(evt =>
                {
                    var oldKey = GetKeyFromItem(item);
                    if (base.ValidateId(evt.newValue, oldKey, idField))
                    {
                        idProperty.SetValue(actualData, evt.newValue);
                        base.OnIdChanged(item, oldKey, evt.newValue);
                    }
                });
                idFieldContainer.Add(idField);
            }
            
            return idFieldContainer;
        }
        
        
        
        
        
        
        
        protected override void FilterItems(string searchTerm)
        {
            if (itemsContainer == null) return;
            
            var items = itemsContainer.Query<VisualElement>(className: "table-item").ToList();
            
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
        
        protected override void OnItemMarkedForDeletion(object item)
        {
            var itemElements = itemsContainer?.Query<VisualElement>(className: "table-item").ToList();
            if (itemElements != null)
            {
                foreach (var element in itemElements)
                {
                    if (element.userData == item)
                    {
                        element.AddToClassList("deleted-item");
                        break;
                    }
                }
            }
        }
        
        protected override void SaveChanges()
        {
            base.SaveChanges();

            // Clear visual indicators
            if (itemsContainer != null)
            {
                var items = itemsContainer.Query<VisualElement>(className: "table-item").ToList();
                foreach (var itemElement in items)
                {
                    itemElement.RemoveFromClassList("new-item");
                    itemElement.RemoveFromClassList("deleted-item");
                }
            }
        }
        
        protected override void RevertChanges()
        {
            base.RevertChanges();

            // Clear visual indicators
            if (itemsContainer != null)
            {
                var items = itemsContainer.Query<VisualElement>(className: "table-item").ToList();
                foreach (var itemElement in items)
                {
                    itemElement.RemoveFromClassList("new-item");
                    itemElement.RemoveFromClassList("deleted-item");
                }
            }
        }
    }
}