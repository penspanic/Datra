using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.Interfaces;
using Datra.Unity.Editor.Components;

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
        
        // Change tracking
        private DatraPropertyTracker propertyTracker;
        private Dictionary<object, DatraPropertyTracker> itemTrackers = new Dictionary<object, DatraPropertyTracker>();
        private List<DatraPropertyField> activeFields = new List<DatraPropertyField>();
        
        // Properties
        public Type CurrentType => currentType;
        public object CurrentRepository => currentRepository;
        
        // Events
        public event Action<Type> OnDataModified;
        public event Action<Type, object> OnSaveRequested; // Type and Repository
        
        public DatraInspectorPanel()
        {
            AddToClassList("datra-inspector-panel");
            propertyTracker = new DatraPropertyTracker();
            propertyTracker.OnAnyPropertyModified += OnTrackerModified;
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
            CleanupFields();
            
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
                
                // Start tracking the data
                propertyTracker.StartTracking(data, false);
                
                // Create fields using the new system
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
            
            // Get ID property
            var idProperty = actualData?.GetType().GetProperty("Id");
            var currentId = idProperty?.GetValue(actualData);
            
            // Get name if available
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
            if (idProperty != null && idProperty.CanWrite)
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
                        if (ValidateId(evt.newValue, oldKey, idField))
                        {
                            idProperty.SetValue(actualData, evt.newValue);
                            OnIdChanged(item, oldKey, evt.newValue);
                            UpdateFoldoutText();
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
                        if (ValidateId(evt.newValue, oldKey, idField))
                        {
                            idProperty.SetValue(actualData, evt.newValue);
                            OnIdChanged(item, oldKey, evt.newValue);
                            UpdateFoldoutText();
                        }
                    });
                    idFieldContainer.Add(idField);
                }
                
                headerControls.Add(idFieldContainer);
            }
            
            // Delete button
            var deleteButton = new Button(() => DeleteItem(item));
            deleteButton.text = "🗑";
            deleteButton.tooltip = "Delete Item";
            deleteButton.AddToClassList("delete-button");
            deleteButton.style.marginLeft = 4;
            headerControls.Add(deleteButton);
            
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
                itemTracker.StartTracking(actualData, false); // Don't skip ID, we handle it separately
                itemTracker.OnAnyPropertyModified += OnTrackerModified;
                itemTrackers[item] = itemTracker;
                
                // Create fields using the new system (skip ID since we handle it in header)
                var fields = DatraFieldFactory.CreateFieldsForObject(actualData, itemTracker, true);
                foreach (var field in fields)
                {
                    field.OnValueChanged += (propName, value) => MarkAsModified();
                    fieldsContainer.Add(field);
                    activeFields.Add(field);
                }
            }
            
            foldout.Add(fieldsContainer);
            itemContainer.Add(foldout);
            
            return itemContainer;
        }
        
        private void OnTrackerModified()
        {
            var hasChanges = propertyTracker.HasAnyModifications();
            
            // Check item trackers too
            if (!hasChanges)
            {
                hasChanges = itemTrackers.Values.Any(tracker => tracker.HasAnyModifications());
            }
            
            if (hasChanges != hasUnsavedChanges)
            {
                hasUnsavedChanges = hasChanges;
                UpdateFooter();
                if (hasChanges)
                {
                    OnDataModified?.Invoke(currentType);
                }
            }
        }
        
        private void CleanupFields()
        {
            foreach (var field in activeFields)
            {
                field.Cleanup();
            }
            activeFields.Clear();
            
            foreach (var tracker in itemTrackers.Values)
            {
                tracker.OnAnyPropertyModified -= OnTrackerModified;
            }
            itemTrackers.Clear();
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
            if (currentRepository == null || currentType == null) return;
            
            try
            {
                // Get the ID property type
                var idProperty = currentType.GetProperty("Id");
                if (idProperty == null)
                {
                    EditorUtility.DisplayDialog("Error", "Cannot find ID property on type", "OK");
                    return;
                }
                
                // Prompt for ID based on type
                if (idProperty.PropertyType == typeof(int))
                {
                    Windows.DatraInputDialog.Show("New Item ID", 
                        "Enter ID for the new item (integer):", 
                        "1",
                        (input) => {
                            if (int.TryParse(input, out int intId))
                            {
                                ProcessNewItemWithId(intId);
                            }
                            else
                            {
                                EditorUtility.DisplayDialog("Invalid ID", "Please enter a valid integer ID", "OK");
                            }
                        });
                }
                else if (idProperty.PropertyType == typeof(string))
                {
                    Windows.DatraInputDialog.Show("New Item ID", 
                        "Enter ID for the new item:", 
                        "NewItem",
                        (input) => {
                            if (!string.IsNullOrWhiteSpace(input))
                            {
                                ProcessNewItemWithId(input);
                            }
                            else
                            {
                                EditorUtility.DisplayDialog("Invalid ID", "ID cannot be empty", "OK");
                            }
                        });
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", 
                        $"Unsupported ID type: {idProperty.PropertyType}", "OK");
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create new item: {e.Message}", "OK");
                Debug.LogError($"Failed to create new item: {e}");
            }
        }
        
        private void ProcessNewItemWithId(object newId)
        {
            try
            {
                // Check for duplicate ID
                if (IsIdDuplicate(newId))
                {
                    EditorUtility.DisplayDialog("Duplicate ID", 
                        $"An item with ID '{newId}' already exists", "OK");
                    return;
                }
                
                // Create a new instance and set the ID
                var newItem = Activator.CreateInstance(currentType);
                var idProperty = currentType.GetProperty("Id");
                
                if (idProperty != null && idProperty.CanWrite)
                {
                    idProperty.SetValue(newItem, newId);
                }
                
                // Find the Add method on the repository
                var addMethod = currentRepository.GetType().GetMethod("Add");
                if (addMethod != null)
                {
                    try
                    {
                        addMethod.Invoke(currentRepository, new[] { newItem });
                        
                        // Refresh the display
                        RefreshContent();
                        MarkAsModified();
                        UpdateStatus($"New {currentType.Name} added with ID: {newId}");
                    }
                    catch (TargetInvocationException tie)
                    {
                        // Unwrap the inner exception for clearer error messages
                        var innerEx = tie.InnerException ?? tie;
                        EditorUtility.DisplayDialog("Error", 
                            $"Failed to add new item: {innerEx.Message}", 
                            "OK");
                        Debug.LogError($"Failed to add new item: {innerEx}");
                    }
                }
                else
                {
                    UpdateStatus("Add method not found on repository");
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to process new item: {e.Message}", "OK");
                Debug.LogError($"Failed to process new item: {e}");
            }
        }
        
        private object GetKeyFromItem(object item)
        {
            if (item == null) return null;
            
            var itemType = item.GetType();
            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var keyProperty = itemType.GetProperty("Key");
                return keyProperty?.GetValue(item);
            }
            
            return item;
        }
        
        private void OnIdChanged(object item, object oldKey, object newKey)
        {
            try
            {
                // If the repository supports key updates, handle it
                var updateKeyMethod = currentRepository.GetType().GetMethod("UpdateKey");
                if (updateKeyMethod != null)
                {
                    try
                    {
                        var result = updateKeyMethod.Invoke(currentRepository, new[] { oldKey, newKey });
                        
                        if (result is bool success && success)
                        {
                            MarkAsModified();
                            UpdateStatus($"ID changed from {oldKey} to {newKey}");
                        }
                        else
                        {
                            // Revert the ID change in the UI if update failed
                            RevertIdChange(item, oldKey);
                            UpdateStatus("Failed to update ID - ID might be read-only");
                        }
                    }
                    catch (TargetInvocationException tie)
                    {
                        // Unwrap the inner exception for clearer error messages
                        var innerEx = tie.InnerException ?? tie;
                        
                        // Revert the ID change in the UI
                        RevertIdChange(item, oldKey);
                        
                        if (innerEx.Message.Contains("ID cannot be empty"))
                        {
                            EditorUtility.DisplayDialog("Invalid ID", 
                                "ID cannot be empty. Please enter a valid ID.", 
                                "OK");
                        }
                        else if (innerEx.Message.Contains("already exists"))
                        {
                            EditorUtility.DisplayDialog("Duplicate ID", 
                                innerEx.Message, 
                                "OK");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Error", 
                                $"Failed to update ID: {innerEx.Message}", 
                                "OK");
                        }
                        
                        Debug.LogError($"Failed to update ID: {innerEx}");
                    }
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to update ID: {e.Message}", "OK");
                Debug.LogError($"Failed to update ID: {e}");
            }
        }
        
        private void RevertIdChange(object item, object oldKey)
        {
            // Extract actual data from KeyValuePair if needed
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
            
            // Revert the ID property
            var idProperty = actualData?.GetType().GetProperty("Id");
            if (idProperty != null && idProperty.CanWrite)
            {
                idProperty.SetValue(actualData, oldKey);
            }
            
            // Refresh the content to update the UI
            RefreshContent();
        }
        
        private void DeleteItem(object item)
        {
            if (EditorUtility.DisplayDialog("Delete Item", 
                "Are you sure you want to delete this item?", 
                "Delete", "Cancel"))
            {
                try
                {
                    // Extract actual data from KeyValuePair if needed
                    object keyToRemove = item;
                    var itemType = item.GetType();
                    if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                    {
                        var keyProperty = itemType.GetProperty("Key");
                        keyToRemove = keyProperty?.GetValue(item);
                    }
                    
                    // Find the Remove method on the repository
                    var removeMethod = currentRepository.GetType().GetMethod("Remove");
                    if (removeMethod != null && keyToRemove != null)
                    {
                        var result = removeMethod.Invoke(currentRepository, new[] { keyToRemove });
                        
                        // Refresh the display
                        RefreshContent();
                        MarkAsModified();
                        UpdateStatus($"Item deleted");
                    }
                    else
                    {
                        UpdateStatus("Remove method not found on repository");
                    }
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to delete item: {e.Message}", "OK");
                    Debug.LogError($"Failed to delete item: {e}");
                }
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
            OnSaveRequested?.Invoke(currentType, currentRepository);
            
            // Update baselines after save
            propertyTracker.UpdateBaseline();
            foreach (var tracker in itemTrackers.Values)
            {
                tracker.UpdateBaseline();
            }
            
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
                // Revert all trackers
                propertyTracker.RevertAll();
                foreach (var tracker in itemTrackers.Values)
                {
                    tracker.RevertAll();
                }
                
                // Refresh all fields
                foreach (var field in activeFields)
                {
                    field.RefreshField();
                }
                
                hasUnsavedChanges = false;
                UpdateFooter();
                UpdateStatus("Changes reverted");
            }
        }
        
        private void UpdateStatus(string message)
        {
            statusLabel.text = message;
        }
        
        private bool ValidateId(object newId, object oldId, VisualElement idField)
        {
            // Clear previous error state
            idField.RemoveFromClassList("id-field-error");
            
            // Check if ID is empty
            if (newId == null || (newId is string strId && string.IsNullOrWhiteSpace(strId)))
            {
                idField.AddToClassList("id-field-error");
                return false;
            }
            
            // Check if ID already exists (but allow keeping the same ID)
            if (!newId.Equals(oldId) && IsIdDuplicate(newId))
            {
                idField.AddToClassList("id-field-error");
                return false;
            }
            
            return true;
        }
        
        private bool IsIdDuplicate(object id)
        {
            if (currentRepository == null || id == null) return false;
            
            var getAllMethod = currentRepository.GetType().GetMethod("GetAll");
            if (getAllMethod != null)
            {
                var items = getAllMethod.Invoke(currentRepository, null) as System.Collections.IEnumerable;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        var key = GetKeyFromItem(item);
                        if (key != null && key.Equals(id))
                        {
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
    }
}