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
    public abstract class DatraDataView : VisualElement
    {
        // Core properties
        protected Type dataType;
        protected object repository;
        protected object dataContext;
        protected List<DatraPropertyField> activeFields = new List<DatraPropertyField>();

        // Tracking collections
        protected HashSet<object> newItems = new HashSet<object>();
        protected HashSet<object> deletedItems = new HashSet<object>();

        // Baseline storage for revert functionality
        protected Dictionary<object, Dictionary<string, object>> baselineValues = new Dictionary<object, Dictionary<string, object>>();
        
        // Common UI elements
        protected VisualElement searchField;  // Base type to support both TextField and ToolbarSearchField
        
        // UI Elements
        protected VisualElement headerContainer;
        protected new VisualElement contentContainer;
        protected VisualElement footerContainer;
        protected Button saveButton;
        protected Button revertButton;
        protected Label statusLabel;
        
        // State
        protected bool hasUnsavedChanges = false;
        protected bool isReadOnly = false;
        
        // Events
        public event Action<Type> OnDataModified;
        public event Action<Type, object> OnSaveRequested;
        public event Action<object> OnItemDeleted;
        public event Action OnAddNewItem;

        protected void InvokeOnItemDeleted(object item) => OnItemDeleted?.Invoke(item);
        protected void InvokeOnAddNewItem() => OnAddNewItem?.Invoke();
        protected void InvokeOnSaveRequested(Type type, object repo) => OnSaveRequested?.Invoke(type, repo);
        
        // Properties
        public Type DataType => dataType;
        public object Repository => repository;
        public bool HasUnsavedChanges => hasUnsavedChanges;
        public bool IsReadOnly 
        { 
            get => isReadOnly; 
            set 
            { 
                isReadOnly = value;
                UpdateEditability();
            }
        }
        
        protected DatraDataView()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            InitializeBase();
        }
        
        private void InitializeBase()
        {
            // Header
            headerContainer = new VisualElement();
            headerContainer.AddToClassList("data-view-header");
            Add(headerContainer);
            
            // Content container (no scroll - let subclasses handle scrolling)
            contentContainer = new VisualElement();
            contentContainer.AddToClassList("data-view-content");
            contentContainer.style.flexGrow = 1;
            Add(contentContainer);
            
            // Footer
            footerContainer = new VisualElement();
            footerContainer.AddToClassList("data-view-footer");
            InitializeFooter();
            Add(footerContainer);
            
            // Let derived classes initialize their specific UI
            InitializeView();
        }
        
        private void InitializeFooter()
        {
            footerContainer.style.flexDirection = FlexDirection.Row;
            footerContainer.style.justifyContent = Justify.SpaceBetween;
            footerContainer.style.paddingLeft = 8;
            footerContainer.style.paddingRight = 8;
            footerContainer.style.paddingTop = 4;
            footerContainer.style.paddingBottom = 4;
            footerContainer.style.borderTopWidth = 1;
            footerContainer.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);
            
            // Status area
            var statusArea = new VisualElement();
            statusArea.style.flexDirection = FlexDirection.Row;
            statusArea.style.alignItems = Align.Center;
            
            statusLabel = new Label("Ready");
            statusLabel.AddToClassList("status-label");
            statusArea.Add(statusLabel);
            
            footerContainer.Add(statusArea);
            
            // Action buttons
            var actionArea = new VisualElement();
            actionArea.style.flexDirection = FlexDirection.Row;
            actionArea.style.alignItems = Align.Center;
            
            revertButton = new Button(RevertChanges);
            revertButton.text = "Revert";
            revertButton.AddToClassList("secondary-button");
            revertButton.SetEnabled(false);
            revertButton.style.marginRight = 8;
            actionArea.Add(revertButton);
            
            saveButton = new Button(SaveChanges);
            saveButton.text = "Save";
            saveButton.AddToClassList("primary-button");
            saveButton.SetEnabled(false);
            actionArea.Add(saveButton);
            
            footerContainer.Add(actionArea);
        }
        
        public virtual void SetData(Type type, object repo, object context)
        {
            dataType = type;
            repository = repo;
            dataContext = context;
            hasUnsavedChanges = false;
            
            CleanupFields();
            RefreshContent();
            UpdateFooter();
        }
        
        protected abstract void InitializeView();
        public abstract void RefreshContent();
        
        protected virtual void UpdateEditability()
        {
            saveButton?.SetEnabled(!isReadOnly && hasUnsavedChanges);
            revertButton?.SetEnabled(!isReadOnly && hasUnsavedChanges);
            
            foreach (var field in activeFields)
            {
                field.SetEnabled(!isReadOnly);
            }
        }
        
        protected void CleanupFields()
        {
            activeFields.Clear();
        }

        protected void MarkAsModified()
        {
            if (!hasUnsavedChanges)
            {
                hasUnsavedChanges = true;
                UpdateFooter();
                OnDataModified?.Invoke(dataType);
            }
        }
        
        protected virtual void UpdateFooter()
        {
            if (isReadOnly)
            {
                saveButton.SetEnabled(false);
                revertButton.SetEnabled(false);
                statusLabel.text = "Read-only mode";
                statusLabel.RemoveFromClassList("status-warning");
            }
            else
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
        }
        
        protected virtual void SaveChanges()
        {
            if (isReadOnly) return;
            
            OnSaveRequested?.Invoke(dataType, repository);

            // Clear tracking collections
            newItems.Clear();
            deletedItems.Clear();

            hasUnsavedChanges = false;
            UpdateFooter();
            UpdateStatus("Changes saved successfully");
        }

        protected virtual void RevertChanges()
        {
            if (isReadOnly) return;

            if (EditorUtility.DisplayDialog("Revert Changes",
                "Are you sure you want to revert all changes?",
                "Revert", "Cancel"))
            {
                // Clear tracking collections
                newItems.Clear();
                deletedItems.Clear();

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

        protected void UpdateStatus(string message)
        {
            statusLabel.text = message;
        }

        /// <summary>
        /// Public method to trigger save from external code (e.g., toolbar buttons)
        /// </summary>
        public void SaveData()
        {
            SaveChanges();
        }

        /// <summary>
        /// Public method to trigger revert from external code
        /// </summary>
        public void RevertData()
        {
            RevertChanges();
        }
        
        protected bool IsTableData(Type type)
        {
            var interfaces = type.GetInterfaces();
            return interfaces.Any(i => i.IsGenericType && 
                i.GetGenericTypeDefinition() == typeof(Datra.Interfaces.ITableData<>));
        }
        
        protected object GetKeyFromItem(object item)
        {
            if (item == null) return null;
            
            var itemType = item.GetType();
            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var keyProperty = itemType.GetProperty("Key");
                return keyProperty?.GetValue(item);
            }
            
            // For non-KeyValuePair items, try to get the ID property
            var idProperty = item.GetType().GetProperty("Id");
            if (idProperty != null)
            {
                return idProperty.GetValue(item);
            }
            
            return item;
        }
        
        protected object ExtractActualData(object item)
        {
            if (item == null) return null;
            
            var itemType = item.GetType();
            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var valueProperty = itemType.GetProperty("Value");
                return valueProperty?.GetValue(item);
            }
            
            return item;
        }
        
        protected void AddNewItem()
        {
            if (repository == null || dataType == null || isReadOnly) return;
            
            InvokeOnAddNewItem();
            
            try
            {
                // Get the ID property type
                var idProperty = dataType.GetProperty("Id");
                if (idProperty == null)
                {
                    EditorUtility.DisplayDialog("Error", "Cannot find ID property on type", "OK");
                    return;
                }
                
                // Prompt for ID based on type
                if (idProperty.PropertyType == typeof(int))
                {
                    DatraInputDialog.Show("New Item ID", 
                        "Enter ID for the new item (integer):", 
                        "1",
                        (input) => {
                            if (int.TryParse(input, out int intId))
                            {
                                ProcessNewItemWithId(intId);
                            }
                            else
                            {
                                EditorUtility.DisplayDialog("Invalid ID", 
                                    "Please enter a valid integer ID", "OK");
                            }
                        });
                }
                else if (idProperty.PropertyType == typeof(string))
                {
                    DatraInputDialog.Show("New Item ID", 
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
        
        protected void ProcessNewItemWithId(object newId)
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
                var newItem = Activator.CreateInstance(dataType);
                var idProperty = dataType.GetProperty("Id");
                
                if (idProperty != null && idProperty.CanWrite)
                {
                    idProperty.SetValue(newItem, newId);
                }
                
                // Find the Add method on the repository
                var addMethod = repository.GetType().GetMethod("Add");
                if (addMethod != null)
                {
                    try
                    {
                        addMethod.Invoke(repository, new[] { newItem });
                        
                        // Mark item as new before refresh
                        OnNewItemAdded(newItem);
                        
                        // Refresh the display
                        RefreshContent();
                        MarkAsModified();
                        UpdateStatus($"New {dataType.Name} added with ID: {newId}");
                    }
                    catch (TargetInvocationException tie)
                    {
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
        
        protected bool IsIdDuplicate(object id)
        {
            if (repository == null || id == null) return false;
            
            var getAllMethod = repository.GetType().GetMethod("GetAll");
            if (getAllMethod != null)
            {
                var items = getAllMethod.Invoke(repository, null) as System.Collections.IEnumerable;
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
        
        protected void DeleteItem(object item)
        {
            if (isReadOnly) return;
            
            if (EditorUtility.DisplayDialog("Delete Item", 
                "Are you sure you want to delete this item?", 
                "Delete", "Cancel"))
            {
                try
                {
                    // Mark item as deleted before actually deleting
                    deletedItems.Add(item);
                    OnItemMarkedForDeletion(item);
                    
                    // Extract actual data from KeyValuePair if needed
                    object keyToRemove = GetKeyFromItem(item);
                    
                    // Find the Remove method on the repository
                    var removeMethod = repository.GetType().GetMethod("Remove");
                    if (removeMethod != null && keyToRemove != null)
                    {
                        var result = removeMethod.Invoke(repository, new[] { keyToRemove });

                        // Refresh the display
                        RefreshContent();
                        MarkAsModified();
                        UpdateStatus($"Item deleted");
                        
                        InvokeOnItemDeleted(item);
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
        
        protected void OnIdChanged(object item, object oldKey, object newKey)
        {
            try
            {
                // If the repository supports key updates, handle it
                var updateKeyMethod = repository.GetType().GetMethod("UpdateKey");
                if (updateKeyMethod != null)
                {
                    try
                    {
                        var result = updateKeyMethod.Invoke(repository, new[] { oldKey, newKey });
                        
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
        
        protected void RevertIdChange(object item, object oldKey)
        {
            // Extract actual data from KeyValuePair if needed
            object actualData = ExtractActualData(item);
            
            // Revert the ID property
            var idProperty = actualData?.GetType().GetProperty("Id");
            if (idProperty != null && idProperty.CanWrite)
            {
                idProperty.SetValue(actualData, oldKey);
            }
            
            // Refresh the content to update the UI
            RefreshContent();
        }
        
        protected bool ValidateId(object newId, object oldId, VisualElement idField)
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
        
        protected virtual void OnNewItemAdded(object newItem)
        {
            newItems.Add(newItem);
        }
        
        protected virtual void OnItemMarkedForDeletion(object item)
        {
            // Override in derived classes to add visual feedback
        }
        
        protected virtual void FilterItems(string searchTerm)
        {
            // Override in derived classes to implement filtering
        }
        
        public virtual void Cleanup()
        {
            CleanupFields();
            newItems.Clear();
            deletedItems.Clear();
            baselineValues.Clear();
        }

        #region Baseline Management for Revert Functionality

        /// <summary>
        /// Store baseline values for all items and their properties
        /// </summary>
        protected void StoreBaselineValues(IEnumerable<object> items, IEnumerable<PropertyInfo> properties)
        {
            baselineValues.Clear();

            if (items == null || properties == null) return;

            foreach (var item in items)
            {
                var itemBaseline = new Dictionary<string, object>();
                foreach (var property in properties)
                {
                    try
                    {
                        var value = property.GetValue(item);
                        itemBaseline[property.Name] = CloneValue(value);
                    }
                    catch
                    {
                        // Skip properties that can't be read
                    }
                }
                baselineValues[item] = itemBaseline;
            }
        }

        /// <summary>
        /// Get baseline value for a specific item and property
        /// </summary>
        protected object GetBaselineValue(object item, string propertyName)
        {
            if (baselineValues.TryGetValue(item, out var itemBaseline))
            {
                if (itemBaseline.TryGetValue(propertyName, out var value))
                {
                    return CloneValue(value);
                }
            }
            return null;
        }

        /// <summary>
        /// Simple clone for baseline values
        /// </summary>
        protected object CloneValue(object value)
        {
            if (value == null) return null;

            var type = value.GetType();

            // For value types and strings, direct assignment works
            if (type.IsValueType || type == typeof(string))
            {
                return value;
            }

            // For arrays, create a copy
            if (type.IsArray)
            {
                var array = value as Array;
                var clone = Array.CreateInstance(type.GetElementType(), array.Length);
                Array.Copy(array, clone, array.Length);
                return clone;
            }

            // For other reference types, return as-is (basic implementation)
            // In production, you might want deep cloning via serialization
            return value;
        }

        #endregion

        #region Field Value Update Helpers

        /// <summary>
        /// Update the UI field value directly (needed for revert to work properly)
        /// </summary>
        protected void UpdateFieldValue(DatraPropertyField field, Type propertyType, object value)
        {
            // Find the actual UI element inside DatraPropertyField and update it
            if (propertyType == typeof(string))
            {
                var textField = field.Q<TextField>();
                if (textField != null)
                {
                    textField.SetValueWithoutNotify(value as string ?? "");
                }
            }
            else if (propertyType == typeof(int))
            {
                var intField = field.Q<IntegerField>();
                if (intField != null)
                {
                    intField.SetValueWithoutNotify(value != null ? (int)value : 0);
                }
            }
            else if (propertyType == typeof(float))
            {
                var floatField = field.Q<FloatField>();
                if (floatField != null)
                {
                    floatField.SetValueWithoutNotify(value != null ? (float)value : 0f);
                }
            }
            else if (propertyType == typeof(double))
            {
                var doubleField = field.Q<DoubleField>();
                if (doubleField != null)
                {
                    doubleField.SetValueWithoutNotify(value != null ? (double)value : 0.0);
                }
            }
            else if (propertyType == typeof(bool))
            {
                var toggle = field.Q<Toggle>();
                if (toggle != null)
                {
                    toggle.SetValueWithoutNotify(value != null && (bool)value);
                }
            }
            else if (propertyType.IsEnum)
            {
                var enumField = field.Q<EnumField>();
                if (enumField != null && value != null)
                {
                    enumField.SetValueWithoutNotify((Enum)value);
                }
            }
            else
            {
                // For other types, try to refresh the field
                field.RefreshField();
            }
        }

        #endregion
    }
}