using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.DataTypes;
using Datra.Interfaces;
using Datra.Localization;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.Unity.Editor.Components;
using Datra.Unity.Editor.Interfaces;
using Datra.Unity.Editor.Utilities;
using Datra.Editor.Interfaces;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Views
{
    public abstract class DatraDataView : VisualElement, ILocaleProvider
    {
        // Core properties
        protected Type dataType;
        protected IDataRepository repository;
        protected object dataContext;
        protected List<DatraPropertyField> activeFields = new List<DatraPropertyField>();

        // External change tracker (IRepositoryChangeTracker)
        protected IRepositoryChangeTracker changeTracker;

        // Localization support (for FixedLocale properties)
        protected Datra.Services.LocalizationContext localizationContext;
        protected Utilities.LocalizationChangeTracker localizationChangeTracker;

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
        public event Action<Type, bool> OnDataModified;  // Type, isModified
        public event Action<Type, IDataRepository> OnSaveRequested;
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
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            InitializeBase();
        }
        
        private void InitializeBase()
        {
            // Header (fixed size, doesn't grow)
            headerContainer = new VisualElement();
            headerContainer.AddToClassList("data-view-header");
            headerContainer.style.flexShrink = 0; // Don't shrink
            headerContainer.style.flexGrow = 0;   // Don't grow
            Add(headerContainer);

            // Content container (grows to fill remaining space)
            contentContainer = new VisualElement();
            contentContainer.AddToClassList("data-view-content");
            contentContainer.style.flexGrow = 1;  // Take remaining space
            contentContainer.style.flexShrink = 1; // Allow shrinking
            contentContainer.style.overflow = Overflow.Hidden; // Prevent overflow
            Add(contentContainer);

            // Footer (fixed size)
            footerContainer = new VisualElement();
            footerContainer.AddToClassList("data-view-footer");
            footerContainer.style.flexShrink = 0; // Don't shrink
            footerContainer.style.flexGrow = 0;   // Don't grow
            InitializeFooter();
            Add(footerContainer);

            // Let derived classes initialize their specific UI
            InitializeView();
        }
        
        private void InitializeFooter()
        {
            // Status area
            var statusArea = new VisualElement();
            statusArea.AddToClassList("footer-status-area");

            statusLabel = new Label("Ready");
            statusLabel.AddToClassList("status-label");
            statusArea.Add(statusLabel);

            footerContainer.Add(statusArea);

            // Action buttons
            var actionArea = new VisualElement();
            actionArea.AddToClassList("footer-action-area");

            revertButton = new Button(RevertChanges);
            revertButton.text = "Revert";
            revertButton.AddToClassList("secondary-button");
            revertButton.SetEnabled(false);
            actionArea.Add(revertButton);

            saveButton = new Button(SaveChanges);
            saveButton.text = "Save";
            saveButton.AddToClassList("primary-button");
            saveButton.SetEnabled(false);
            actionArea.Add(saveButton);

            footerContainer.Add(actionArea);
        }
        
        public virtual void SetData(
            Type type,
            IDataRepository repo,
            IDataContext context,
            IRepositoryChangeTracker tracker,
            Datra.Services.LocalizationContext localizationCtx = null,
            Utilities.LocalizationChangeTracker localizationTracker = null)
        {
            // Only reset modification state if switching to a different data type
            bool isDifferentType = dataType != type;

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

        /// <summary>
        /// Virtual method to check if there are actual modifications in derived classes
        /// </summary>
        protected virtual bool HasActualModifications() => changeTracker?.HasModifications ?? false;

        /// <summary>
        /// Updates the modified state based on actual modification check
        /// </summary>
        public void UpdateModifiedState()
        {
            bool actuallyModified = HasActualModifications();

            bool wasModified = hasUnsavedChanges;
            if (hasUnsavedChanges != actuallyModified)
            {
                hasUnsavedChanges = actuallyModified;
                UpdateFooter();
                OnDataModified?.Invoke(dataType, actuallyModified);

                // If changed from modified to not modified, clear UI modifications
                if (wasModified && !actuallyModified)
                {
                    OnModificationsCleared();
                }
            }
        }

        /// <summary>
        /// Called when modifications are cleared (e.g., after save). Override to clear UI indicators.
        /// </summary>
        protected virtual void OnModificationsCleared()
        {
            // Override in derived classes to clear visual modification indicators
        }

        /// <summary>
        /// Legacy method for backward compatibility - now calls UpdateModifiedState
        /// </summary>
        protected void MarkAsModified()
        {
            UpdateModifiedState();
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

            // Update state based on actual modifications
            UpdateModifiedState();
            UpdateStatus("Changes saved successfully");
        }

        protected virtual void RevertChanges()
        {
            if (isReadOnly) return;

            if (EditorUtility.DisplayDialog("Revert Changes",
                "Are you sure you want to revert all changes?",
                "Revert", "Cancel"))
            {
                changeTracker.RevertAll();

                // Refresh all fields
                foreach (var field in activeFields)
                {
                    field.RefreshField();
                }

                // Update state based on actual modifications
                UpdateModifiedState();
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

        #region Asset Helper Methods

        /// <summary>
        /// Check if the repository is an IAssetRepository
        /// </summary>
        protected bool IsAssetRepository()
        {
            if (repository == null) return false;
            return repository.GetType().GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAssetRepository<>));
        }

        /// <summary>
        /// Check if the repository is an IEditableAssetRepository
        /// </summary>
        protected bool IsEditableAssetRepository()
        {
            if (repository == null) return false;
            return repository.GetType().GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEditableAssetRepository<>));
        }

        /// <summary>
        /// Check if item is an Asset&lt;T&gt; wrapper
        /// </summary>
        protected bool IsAssetWrapper(object item)
        {
            if (item == null) return false;
            var itemType = item.GetType();
            return itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(Asset<>);
        }

        /// <summary>
        /// Get AssetId from Asset&lt;T&gt; wrapper
        /// </summary>
        protected AssetId? GetAssetId(object item)
        {
            if (!IsAssetWrapper(item)) return null;
            var idProperty = item.GetType().GetProperty("Id");
            return idProperty?.GetValue(item) as AssetId?;
        }

        /// <summary>
        /// Get Data from Asset&lt;T&gt; wrapper
        /// </summary>
        protected object GetAssetData(object item)
        {
            if (!IsAssetWrapper(item)) return item;
            var dataProperty = item.GetType().GetProperty("Data");
            return dataProperty?.GetValue(item);
        }

        /// <summary>
        /// Get FilePath from Asset&lt;T&gt; wrapper
        /// </summary>
        protected string GetAssetFilePath(object item)
        {
            if (!IsAssetWrapper(item)) return null;
            var filePathProperty = item.GetType().GetProperty("FilePath");
            return filePathProperty?.GetValue(item) as string;
        }

        /// <summary>
        /// Mark an asset as modified in the repository (for in-place property edits).
        /// This ensures the asset will be saved when SaveAsync() is called.
        /// </summary>
        protected void MarkAssetModified(object item)
        {
            if (!IsEditableAssetRepository()) return;

            var assetId = GetAssetId(item);
            if (!assetId.HasValue) return;

            // Call repository.MarkAsModified(assetId) via reflection
            var markMethod = repository.GetType().GetMethod("MarkAsModified", new[] { typeof(AssetId) });
            markMethod?.Invoke(repository, new object[] { assetId.Value });
        }

        #endregion

        /// <summary>
        /// Get filtered properties for a type, excluding properties with DatraIgnoreAttribute
        /// </summary>
        protected List<PropertyInfo> GetFilteredProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && !p.GetCustomAttributes(typeof(Datra.Attributes.DatraIgnoreAttribute), true).Any())
                .ToList();
        }

        protected object GetKeyFromItem(object item)
        {
            if (item == null) return null;

            // Handle Asset<T> wrapper - return AssetId
            if (IsAssetWrapper(item))
            {
                return GetAssetId(item);
            }

            var itemType = item.GetType();
            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var keyProperty = itemType.GetProperty("Key");
                return keyProperty?.GetValue(item);
            }

            // For non-KeyValuePair items (single data or table items)
            // If this is single data (not table data), use fixed key
            if (!IsTableData(dataType))
            {
                return "single";
            }

            // For table data items, try to get the ID property
            var idProperty = item.GetType().GetProperty("Id");
            if (idProperty != null)
            {
                return idProperty.GetValue(item);
            }

            return item;
        }

        /// <summary>
        /// Extract actual data from wrapper types (KeyValuePair or Asset&lt;T&gt;)
        /// </summary>
        protected object ExtractActualData(object item)
        {
            if (item == null) return null;

            // Handle Asset<T> wrapper - return inner Data
            if (IsAssetWrapper(item))
            {
                return GetAssetData(item);
            }

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
                // Handle Asset data differently
                if (IsEditableAssetRepository())
                {
                    AddNewAssetItem();
                    return;
                }

                // Handle Table data
                AddNewTableItem();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create new item: {e.Message}", "OK");
                Debug.LogError($"Failed to create new item: {e}");
            }
        }

        /// <summary>
        /// Add new item to table data repository
        /// </summary>
        private void AddNewTableItem()
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
                            ProcessNewTableItemWithId(intId);
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
                            ProcessNewTableItemWithId(input);
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

        /// <summary>
        /// Add new item to asset repository
        /// </summary>
        private void AddNewAssetItem()
        {
            // Prompt for file name (used as both file path and initial identifier)
            DatraInputDialog.Show("New Asset",
                "Enter file name for the new asset (without extension):",
                "NewAsset",
                (input) => {
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        EditorUtility.DisplayDialog("Invalid Name", "File name cannot be empty", "OK");
                        return;
                    }

                    ProcessNewAssetItem(input);
                });
        }

        /// <summary>
        /// Process new asset item creation
        /// </summary>
        private void ProcessNewAssetItem(string fileName)
        {
            try
            {
                // Create new data instance
                var newData = Activator.CreateInstance(dataType);

                // Set Name property if exists
                var nameProperty = dataType.GetProperty("Name");
                if (nameProperty != null && nameProperty.CanWrite && nameProperty.PropertyType == typeof(string))
                {
                    nameProperty.SetValue(newData, fileName);
                }

                // Set Id property if exists and is string type
                var idProperty = dataType.GetProperty("Id");
                if (idProperty != null && idProperty.CanWrite && idProperty.PropertyType == typeof(string))
                {
                    idProperty.SetValue(newData, fileName);
                }

                // Determine file path
                string filePath = $"{fileName}.json";

                // Find the Add method: Add(T data, string filePath)
                var addMethod = repository.GetType().GetMethod("Add", new[] { dataType, typeof(string) });
                if (addMethod != null)
                {
                    var asset = addMethod.Invoke(repository, new[] { newData, filePath });

                    // Track addition
                    if (asset != null)
                    {
                        var assetId = GetAssetId(asset);
                        if (assetId.HasValue)
                        {
                            changeTracker?.TrackAdd(assetId.Value, asset);
                        }
                    }

                    RefreshContent();
                    MarkAsModified();
                    UpdateStatus($"New asset '{fileName}' created");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Repository does not support asset creation", "OK");
                }
            }
            catch (TargetInvocationException tie)
            {
                var innerEx = tie.InnerException ?? tie;
                EditorUtility.DisplayDialog("Error", $"Failed to create asset: {innerEx.Message}", "OK");
                Debug.LogError($"Failed to create asset: {innerEx}");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create asset: {e.Message}", "OK");
                Debug.LogError($"Failed to create asset: {e}");
            }
        }
        
        protected void ProcessNewTableItemWithId(object newId)
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

            // Use EnumerateItems() - no reflection needed
            foreach (var item in repository.EnumerateItems())
            {
                var key = GetKeyFromItem(item);
                if (key != null && key.Equals(id))
                {
                    return true;
                }
            }

            return false;
        }
        
        protected void DeleteItem(object item)
        {
            if (isReadOnly) return;

            // Get display name for confirmation dialog
            string itemName = GetItemDisplayName(item);

            if (EditorUtility.DisplayDialog("Delete Item",
                $"Are you sure you want to delete '{itemName}'?",
                "Delete", "Cancel"))
            {
                try
                {
                    // Handle Asset deletion
                    if (IsAssetWrapper(item))
                    {
                        DeleteAssetItem(item);
                        return;
                    }

                    // Handle Table/Single data deletion
                    DeleteTableItem(item);
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to delete item: {e.Message}", "OK");
                    Debug.LogError($"Failed to delete item: {e}");
                }
            }
        }

        /// <summary>
        /// Get display name for an item (for confirmation dialogs)
        /// </summary>
        private string GetItemDisplayName(object item)
        {
            if (item == null) return "item";

            // For Asset wrapper, get Name from metadata or Data
            if (IsAssetWrapper(item))
            {
                var data = GetAssetData(item);
                if (data != null)
                {
                    var nameProperty = data.GetType().GetProperty("Name");
                    if (nameProperty != null)
                    {
                        var name = nameProperty.GetValue(data) as string;
                        if (!string.IsNullOrEmpty(name)) return name;
                    }
                    var idProperty = data.GetType().GetProperty("Id");
                    if (idProperty != null)
                    {
                        return idProperty.GetValue(data)?.ToString() ?? "item";
                    }
                }
                return GetAssetFilePath(item) ?? "item";
            }

            // For table data, try to get Id
            var actualData = ExtractActualData(item);
            if (actualData != null)
            {
                var idProp = actualData.GetType().GetProperty("Id");
                if (idProp != null)
                {
                    return idProp.GetValue(actualData)?.ToString() ?? "item";
                }
            }

            return "item";
        }

        /// <summary>
        /// Delete an asset item from IEditableAssetRepository
        /// </summary>
        private void DeleteAssetItem(object assetWrapper)
        {
            var assetId = GetAssetId(assetWrapper);
            if (!assetId.HasValue)
            {
                UpdateStatus("Cannot determine asset ID");
                return;
            }

            // Track deletion
            changeTracker?.TrackDelete(assetId.Value);

            // Mark item as deleted
            OnItemMarkedForDeletion(assetWrapper);

            // Find Remove(AssetId) method
            var removeMethod = repository.GetType().GetMethod("Remove", new[] { typeof(AssetId) });
            if (removeMethod != null)
            {
                var result = removeMethod.Invoke(repository, new object[] { assetId.Value });

                RefreshContent();
                MarkAsModified();
                UpdateStatus("Asset deleted");

                InvokeOnItemDeleted(assetWrapper);
            }
            else
            {
                UpdateStatus("Remove method not found on repository");
            }
        }

        /// <summary>
        /// Delete a table data item from IKeyValueDataRepository
        /// </summary>
        private void DeleteTableItem(object item)
        {
            object keyToRemove = GetKeyFromItem(item);
            if (keyToRemove == null)
            {
                UpdateStatus("Cannot determine item key");
                return;
            }

            // Track deletion
            changeTracker?.TrackDelete(keyToRemove);

            // Mark item as deleted
            OnItemMarkedForDeletion(item);

            // Find the Remove method on the repository
            var removeMethod = repository.GetType().GetMethod("Remove");
            if (removeMethod != null)
            {
                var result = removeMethod.Invoke(repository, new[] { keyToRemove });

                RefreshContent();
                MarkAsModified();
                UpdateStatus("Item deleted");

                InvokeOnItemDeleted(item);
            }
            else
            {
                UpdateStatus("Remove method not found on repository");
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
            var itemKey = GetKeyFromItem(newItem);
            if (itemKey != null)
                changeTracker.TrackAdd(itemKey, newItem);
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
        }

        #region Baseline Management for Revert Functionality

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

        #region ILocaleProvider Implementation

        /// <summary>
        /// Gets the localized text for a LocaleRef in the current language
        /// </summary>
        public string GetLocaleText(LocaleRef localeRef)
        {
            if (localizationContext == null)
                return $"[{localeRef.Key}]";

            return localeRef.Evaluate(localizationContext);
        }

        /// <summary>
        /// Shows a popup to edit the locale across all languages
        /// </summary>
        public void ShowLocaleEditPopup(LocaleRef localeRef, Rect buttonWorldBound, Action<string> onUpdated)
        {
            if (localizationContext == null)
            {
                Debug.LogWarning("LocalizationContext is not available. Cannot edit locale.");
                return;
            }

            Components.LocaleEditPopup.ShowWindow(
                localizationContext,
                localizationChangeTracker,
                localeRef.Key,
                buttonWorldBound,
                onModified: () =>
                {
                    // Get updated text in current language
                    var updatedText = localeRef.Evaluate(localizationContext);
                    onUpdated?.Invoke(updatedText);

                    // Mark as modified
                    MarkAsModified();
                    UpdateModifiedState();
                });
        }

        /// <summary>
        /// Evaluates a nested locale reference to a concrete LocaleRef using the provided context.
        /// </summary>
        public LocaleRef EvaluateNestedLocale(NestedLocaleRef nestedLocale, object rootObject, int elementIndex, object element)
        {
            if (!nestedLocale.HasValue)
                return new LocaleRef { Key = string.Empty };

            // Build the prefix from root object's type and Id
            var rootType = rootObject?.GetType();
            if (rootType == null)
                return new LocaleRef { Key = nestedLocale.PathTemplate };

            var typeName = rootType.Name;

            // Get the Id property value
            var idProperty = rootType.GetProperty("Id");
            var idValue = idProperty?.GetValue(rootObject)?.ToString() ?? string.Empty;

            var prefix = $"{typeName}.{idValue}";

            // Evaluate the nested locale with the first segment (collection name) and index
            // For NestedLocaleRef.Create("Objectives", "Description"), the first segment is "Objectives"
            if (nestedLocale.Segments.Length > 0)
            {
                var collectionSegment = nestedLocale.Segments[0];
                return nestedLocale.Evaluate(prefix, collectionSegment, elementIndex);
            }

            return nestedLocale.EvaluateNoCache(prefix);
        }

        #endregion
    }
}