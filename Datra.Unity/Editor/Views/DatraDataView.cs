using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
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
        protected DatraPropertyTracker propertyTracker;
        protected Dictionary<object, DatraPropertyTracker> itemTrackers = new Dictionary<object, DatraPropertyTracker>();
        protected List<DatraPropertyField> activeFields = new List<DatraPropertyField>();
        
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
            propertyTracker = new DatraPropertyTracker();
            propertyTracker.OnAnyPropertyModified += OnTrackerModified;
            
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
            
            // Content
            var contentScroll = new ScrollView();
            contentScroll.AddToClassList("data-view-content-scroll");
            contentScroll.style.flexGrow = 1;
            
            contentContainer = new VisualElement();
            contentContainer.AddToClassList("data-view-content");
            contentScroll.Add(contentContainer);
            Add(contentScroll);
            
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
        
        protected void OnTrackerModified()
        {
            var hasChanges = propertyTracker.HasAnyModifications();
            
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
                    OnDataModified?.Invoke(dataType);
                }
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
        
        protected virtual void RevertChanges()
        {
            if (isReadOnly) return;
            
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
        
        protected void UpdateStatus(string message)
        {
            statusLabel.text = message;
        }
        
        protected void MarkAsModified()
        {
            if (!hasUnsavedChanges && !isReadOnly)
            {
                hasUnsavedChanges = true;
                UpdateFooter();
                OnDataModified?.Invoke(dataType);
            }
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
                    // Extract actual data from KeyValuePair if needed
                    object keyToRemove = GetKeyFromItem(item);
                    
                    // Find the Remove method on the repository
                    var removeMethod = repository.GetType().GetMethod("Remove");
                    if (removeMethod != null && keyToRemove != null)
                    {
                        var result = removeMethod.Invoke(repository, new[] { keyToRemove });
                        
                        // Remove from our tracking
                        if (itemTrackers.ContainsKey(item))
                        {
                            itemTrackers[item].OnAnyPropertyModified -= OnTrackerModified;
                            itemTrackers.Remove(item);
                        }
                        
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
        
        public virtual void Cleanup()
        {
            CleanupFields();
            propertyTracker.OnAnyPropertyModified -= OnTrackerModified;
        }
    }
}