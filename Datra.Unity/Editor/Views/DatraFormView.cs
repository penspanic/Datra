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
        
        public DatraFormView() : base()
        {
            AddToClassList("datra-form-view");
        }
        
        protected override void InitializeView()
        {
            // Header already created by base class
            // Add any form-specific header elements if needed
        }
        
        public override void RefreshContent()
        {
            // Store current state before clearing
            var previousNewItems = new HashSet<object>(this.newItems);
            var previousTrackers = new Dictionary<object, DatraPropertyTracker>(itemTrackers);
            
            contentContainer.Clear();
            
            if (repository == null || dataType == null) return;
            
            if (IsTableData(dataType))
            {
                DisplayTableDataAsForm();
                
                // Restore state for table data items
                RestoreTableDataState(previousNewItems, previousTrackers);
            }
            else
            {
                DisplaySingleDataForm();
            }
        }
        
        private void DisplaySingleDataForm()
        {
            var getMethod = repository.GetType().GetMethod("Get");
            var data = getMethod?.Invoke(repository, null);
            
            if (data != null)
            {
                formContainer = new VisualElement();
                formContainer.AddToClassList("single-data-form");
                
                // Start tracking the data
                propertyTracker.StartTracking(data, false);
                
                // Create fields using the field factory
                var fields = DatraFieldFactory.CreateFieldsForObject(data, propertyTracker, false);
                foreach (var field in fields)
                {
                    field.OnValueChanged += (propName, value) => MarkAsModified();
                    formContainer.Add(field);
                    activeFields.Add(field);
                }
                
                contentContainer.Add(formContainer);
            }
        }
        
        private void DisplayTableDataAsForm()
        {
            // Add toolbar for table data
            var toolbar = CreateTableToolbar();
            contentContainer.Add(toolbar);
            
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
                    
                    // Mark as new if it was just added
                    if (this.newItems.Contains(item))
                    {
                        itemElement.AddToClassList("new-item");
                    }
                    
                    itemsContainer.Add(itemElement);
                }
                
                contentContainer.Add(itemsContainer);
            }
        }
        
        private VisualElement CreateTableToolbar()
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
            
            var addButton = new Button(() => base.AddNewItem());
            addButton.text = "➕ Add New Item";
            addButton.AddToClassList("add-button");
            addButton.style.marginRight = 16;
            toolbar.Add(addButton);
            
            this.searchField = new TextField();
            //searchField.placeholder = "Search items...";
            this.searchField.AddToClassList("table-search");
            this.searchField.style.flexGrow = 1;
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
            headerControls.style.flexDirection = FlexDirection.Row;
            headerControls.style.alignItems = Align.Center;
            headerControls.style.position = Position.Absolute;
            headerControls.style.right = 8;
            headerControls.style.top = 4;
            
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
                deleteButton.style.marginLeft = 4;
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
                // Create a tracker for this item
                var itemTracker = new DatraPropertyTracker();
                itemTracker.StartTracking(actualData, false);
                itemTracker.OnAnyPropertyModified += OnTrackerModified;
                itemTrackers[item] = itemTracker;
                
                // Create fields (skip ID since we handle it in header)
                var fields = DatraFieldFactory.CreateFieldsForObject(actualData, itemTracker, true);
                foreach (var field in fields)
                {
                    field.OnValueChanged += (propName, value) => MarkAsModified();
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
            idFieldContainer.style.flexDirection = FlexDirection.Row;
            idFieldContainer.style.alignItems = Align.Center;
            idFieldContainer.style.marginRight = 8;
            
            var idLabel = new Label("ID:");
            idLabel.AddToClassList("id-field-label");
            idLabel.style.marginRight = 4;
            idFieldContainer.Add(idLabel);
            
            if (idProperty.PropertyType == typeof(int))
            {
                var idField = new IntegerField();
                idField.value = (int)(currentId ?? 0);
                idField.AddToClassList("id-field");
                idField.style.width = 60;
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
                idField.style.width = 100;
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
        
        private void RestoreTableDataState(HashSet<object> previousNewItems, Dictionary<object, DatraPropertyTracker> previousTrackers)
        {
            if (itemsContainer == null) return;
            
            var items = itemsContainer.Query<VisualElement>(className: "table-item").ToList();
            foreach (var itemElement in items)
            {
                var item = itemElement.userData;
                if (item != null)
                {
                    // Restore tracker state
                    if (previousTrackers.ContainsKey(item))
                    {
                        itemTrackers[item] = previousTrackers[item];
                    }
                    
                    // Restore new item state
                    if (previousNewItems.Contains(item))
                    {
                        this.newItems.Add(item);
                        itemElement.AddToClassList("new-item");
                    }
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