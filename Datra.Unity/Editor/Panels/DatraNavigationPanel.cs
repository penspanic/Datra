using Datra;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Interfaces;
using Datra.Services;
using Datra.Unity.Editor.Models;
using Datra.Unity.Editor.Utilities;
using Datra.Editor.Interfaces;

namespace Datra.Unity.Editor.Panels
{
    public class DatraNavigationPanel : VisualElement
    {
        private TextField searchField;
        private TreeView treeView;
        private List<TreeViewItemData<DataTypeItem>> treeData;
        private Action<Type> onTypeSelected;
        private Action onLocalizationSelected;
        private HashSet<Type> modifiedTypes = new HashSet<Type>();
        private LocalizationContext localizationContext;
        
        // UI References
        private Button collapseAllButton;
        private Button expandAllButton;
        
        // Data item structure
        private class DataTypeItem
        {
            public string Name { get; set; }
            public Type DataType { get; set; }
            public DataTypeInfo DataTypeInfo { get; set; }
            public bool IsCategory { get; set; }
            public string Icon { get; set; }
            public bool IsModified { get; set; }
            public bool IsLocalization { get; set; }
        }
        
        public DatraNavigationPanel()
        {
            AddToClassList("datra-navigation-panel");
            Initialize();
        }
        
        private void Initialize()
        {
            // Header with title and actions
            var header = new VisualElement();
            header.AddToClassList("navigation-header");
            
            var titleContainer = new VisualElement();
            titleContainer.AddToClassList("navigation-title-container");
            
            var titleIcon = new VisualElement();
            titleIcon.AddToClassList("navigation-title-icon");
            titleContainer.Add(titleIcon);
            
            var titleLabel = new Label("Data Types");
            titleLabel.AddToClassList("navigation-title");
            titleContainer.Add(titleLabel);
            
            header.Add(titleContainer);
            
            // Action buttons
            var actionContainer = new VisualElement();
            actionContainer.AddToClassList("navigation-actions");
            
            expandAllButton = new Button(() => treeView?.ExpandAll());
            expandAllButton.AddToClassList("icon-button");
            expandAllButton.tooltip = "Expand All";
            expandAllButton.text = "⊞";
            actionContainer.Add(expandAllButton);
            
            collapseAllButton = new Button(() => treeView?.CollapseAll());
            collapseAllButton.AddToClassList("icon-button");
            collapseAllButton.tooltip = "Collapse All";
            collapseAllButton.text = "⊟";
            actionContainer.Add(collapseAllButton);
            
            header.Add(actionContainer);
            Add(header);
            
            // Search field
            var searchContainer = new VisualElement();
            searchContainer.AddToClassList("search-container");
            
            searchField = new TextField();
            searchField.AddToClassList("search-field");
            //searchField.label = "Search data types...";
            searchField.RegisterValueChangedCallback(OnSearchChanged);
            
            // Add search icon
            var searchIcon = new VisualElement();
            searchIcon.AddToClassList("search-icon");
            searchContainer.Add(searchIcon);
            searchContainer.Add(searchField);
            
            Add(searchContainer);
            
            // Create TreeView
            treeView = new TreeView();
            treeView.AddToClassList("data-tree-view");
            treeView.fixedItemHeight = 26;
            treeView.makeItem = MakeTreeItem;
            treeView.bindItem = BindTreeItem;
            treeView.selectionType = SelectionType.Single;
            treeView.selectionChanged += OnTreeSelectionChanged;
            
            // Wrap TreeView in ScrollView for consistent styling
            var scrollView = new ScrollView();
            scrollView.AddToClassList("navigation-scroll-view");
            scrollView.Add(treeView);
            Add(scrollView);
            
            // Status bar
            var statusBar = new VisualElement();
            statusBar.AddToClassList("navigation-status-bar");
            
            var statusLabel = new Label();
            statusLabel.name = "status-label";
            statusLabel.AddToClassList("status-label");
            statusBar.Add(statusLabel);
            
            Add(statusBar);
        }
        
        private VisualElement MakeTreeItem()
        {
            var item = new VisualElement();
            item.AddToClassList("tree-item");
            
            var itemContent = new VisualElement();
            itemContent.AddToClassList("tree-item-content");
            
            // Icon
            var icon = new VisualElement();
            icon.name = "item-icon";
            icon.AddToClassList("tree-item-icon");
            itemContent.Add(icon);
            
            // Label
            var label = new Label();
            label.name = "item-label";
            label.AddToClassList("tree-item-label");
            itemContent.Add(label);
            
            // Modified indicator
            var modifiedIndicator = new VisualElement();
            modifiedIndicator.name = "modified-indicator";
            modifiedIndicator.AddToClassList("tree-item-modified");
            modifiedIndicator.tooltip = "Modified";
            itemContent.Add(modifiedIndicator);
            
            item.Add(itemContent);
            
            return item;
        }
        
        private void BindTreeItem(VisualElement element, int index)
        {
            var itemData = treeView.GetItemDataForIndex<DataTypeItem>(index);
            
            var icon = element.Q<VisualElement>("item-icon");
            var label = element.Q<Label>("item-label");
            var modifiedIndicator = element.Q<VisualElement>("modified-indicator");
            
            if (itemData.IsCategory)
            {
                element.AddToClassList("tree-item-category");
                element.RemoveFromClassList("tree-item-data");
                icon.AddToClassList($"icon-{itemData.Icon}");
                label.text = itemData.Name;
                modifiedIndicator.style.display = DisplayStyle.None;
            }
            else
            {
                element.RemoveFromClassList("tree-item-category");
                element.AddToClassList("tree-item-data");
                icon.RemoveFromClassList("icon-single-data");
                icon.RemoveFromClassList("icon-table-data");
                label.text = itemData.Name;
                modifiedIndicator.style.display = itemData.IsModified ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            // Register context menu on the item element
            element.AddManipulator(new ContextualMenuManipulator(OnItemContextMenu));
            element.userData = itemData;
        }
        
        private void OnTreeSelectionChanged(IEnumerable<object> selectedItems)
        {
            var selectedItem = selectedItems.FirstOrDefault() as DataTypeItem;
            if (selectedItem == null)
            {
                onTypeSelected?.Invoke(null);
                return;
            }

            if (!selectedItem.IsCategory)
            {
                if (selectedItem.IsLocalization)
                {
                    // Handle localization selection
                    DatraUserPreferences.SetLastSelectedTreePath("Localization");
                    onLocalizationSelected?.Invoke();
                }
                else if (selectedItem.DataType != null)
                {
                    // Save selected type
                    DatraUserPreferences.SetLastSelectedTreePath(selectedItem.DataType.FullName);
                    onTypeSelected?.Invoke(selectedItem.DataType);
                }
            }
        }
        
        public void SetDataTypeInfos(IReadOnlyList<DataTypeInfo> dataTypeInfos, Action<Type> selectionCallback, LocalizationContext localizationCtx = null)
        {
            onTypeSelected = selectionCallback;
            localizationContext = localizationCtx;
            BuildTreeDataFromInfos(dataTypeInfos);
            UpdateStatusLabel();
            
            // Restore last selected item
            RestoreLastSelection();
        }
        
        public void SetLocalizationCallback(Action localizationCallback)
        {
            onLocalizationSelected = localizationCallback;
        }
        
        private void BuildTreeDataFromInfos(IReadOnlyList<DataTypeInfo> dataTypeInfos)
        {
            var rootItems = new List<TreeViewItemData<DataTypeItem>>();

            // Group data types by RepositoryKind
            var singleDataInfos = dataTypeInfos.Where(info => info.RepositoryKind == RepositoryKind.Single).ToList();
            var tableDataInfos = dataTypeInfos.Where(info => info.RepositoryKind == RepositoryKind.Table).ToList();
            var assetDataInfos = dataTypeInfos.Where(info => info.RepositoryKind == RepositoryKind.Asset).ToList();

            // Create Single Data category
            if (singleDataInfos.Any())
            {
                var category = CreateDataTypeCategory(
                    "SingleData",
                    "single-data",
                    "Single Data",
                    singleDataInfos);
                rootItems.Add(category);
            }

            // Create Table Data category
            if (tableDataInfos.Any())
            {
                var category = CreateDataTypeCategory(
                    "TableData",
                    "table-data",
                    "Table Data",
                    tableDataInfos);
                rootItems.Add(category);
            }

            // Create Asset Data category
            if (assetDataInfos.Any())
            {
                var category = CreateDataTypeCategory(
                    "AssetData",
                    "asset-data",
                    "Asset Data",
                    assetDataInfos);
                rootItems.Add(category);
            }

            // Create Localization category
            if (localizationContext != null)
            {
                var category = CreateLocalizationCategory();
                rootItems.Add(category);
            }

            // Store and display tree data
            treeData = rootItems;
            treeView.SetRootItems(rootItems);
            treeView.Rebuild();
            treeView.ExpandAll();
        }

        /// <summary>
        /// Create a category node with data type items as children
        /// </summary>
        private TreeViewItemData<DataTypeItem> CreateDataTypeCategory(
            string categoryId,
            string iconName,
            string categoryDisplayName,
            IReadOnlyList<DataTypeInfo> dataTypeInfos)
        {
            var items = dataTypeInfos
                .OrderBy(info => info.DataType.Name)
                .Select(info => new TreeViewItemData<DataTypeItem>(
                    info.DataType.GetHashCode(),
                    new DataTypeItem
                    {
                        Name = info.DataType.Name,
                        DataType = info.DataType,
                        DataTypeInfo = info,
                        IsCategory = false,
                        IsModified = modifiedTypes.Contains(info.DataType)
                    }))
                .ToList();

            return new TreeViewItemData<DataTypeItem>(
                categoryId.GetHashCode(),
                new DataTypeItem
                {
                    Name = $"{categoryDisplayName} ({items.Count})",
                    IsCategory = true,
                    Icon = iconName
                },
                items);
        }

        /// <summary>
        /// Create the Localization category node
        /// </summary>
        private TreeViewItemData<DataTypeItem> CreateLocalizationCategory()
        {
            var localizationItem = new TreeViewItemData<DataTypeItem>(
                "LocalizationContext".GetHashCode(),
                new DataTypeItem
                {
                    Name = "Localization",
                    DataType = typeof(LocalizationContext), // Use LocalizationContext for consistency
                    DataTypeInfo = null,
                    IsCategory = false,
                    IsLocalization = true,
                    IsModified = modifiedTypes.Contains(typeof(LocalizationContext))
                });

            return new TreeViewItemData<DataTypeItem>(
                "Localization".GetHashCode(),
                new DataTypeItem
                {
                    Name = "Localization (1)",
                    IsCategory = true,
                    Icon = "localization"
                },
                new List<TreeViewItemData<DataTypeItem>> { localizationItem });
        }
        
        
        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            var searchTerm = evt.newValue.ToLower();
            
            if (string.IsNullOrEmpty(searchTerm))
            {
                // Show all items
                if (treeData != null)
                {
                    treeView.SetRootItems(treeData);
                    treeView.Rebuild();
                    treeView.ExpandAll();
                }
            }
            else
            {
                // Filter items
                FilterTree(searchTerm);
            }
        }
        
        private void FilterTree(string searchTerm)
        {
            if (treeData == null) return;
            
            var filteredItems = new List<TreeViewItemData<DataTypeItem>>();
            
            foreach (var category in treeData)
            {
                var matchingItems = category.children
                    .Where(item => item.data.Name.ToLower().Contains(searchTerm))
                    .ToList();
                
                if (matchingItems.Any())
                {
                    var filteredCategory = new TreeViewItemData<DataTypeItem>(
                        category.id,
                        new DataTypeItem
                        {
                            Name = $"{category.data.Name.Split('(')[0].Trim()} ({matchingItems.Count})",
                            IsCategory = true,
                            Icon = category.data.Icon
                        },
                        matchingItems);
                    
                    filteredItems.Add(filteredCategory);
                }
            }
            
            treeView.SetRootItems(filteredItems);
            treeView.Rebuild();
            treeView.ExpandAll();
        }
        
        private void UpdateStatusLabel()
        {
            var statusLabel = this.Q<Label>("status-label");
            if (statusLabel != null && treeData != null)
            {
                var totalTypes = treeData.Sum(category => category.children?.Count() ?? 0);
                statusLabel.text = $"{totalTypes} data types";
            }
        }
        
        public void MarkTypeAsModified(Type type, bool isModified)
        {
            if (isModified)
                modifiedTypes.Add(type);
            else
                modifiedTypes.Remove(type);

            // Update tree view
            if (treeView != null && treeData != null)
            {
                foreach (var category in treeData)
                {
                    if (category.children != null)
                    {
                        foreach (var child in category.children)
                        {
                            if (child.data == null) continue;

                            // Special handling for LocalizationContext
                            bool isMatch = false;
                            if (type == typeof(LocalizationContext) && child.data.IsLocalization)
                            {
                                isMatch = true;
                            }
                            else if (child.data.DataType == type)
                            {
                                isMatch = true;
                            }

                            if (isMatch)
                            {
                                child.data.IsModified = isModified;
                                // Force rebind of the specific item
                                var index = treeView.viewController.GetIndexForId(child.id);
                                if (index >= 0)
                                {
                                    treeView.RefreshItem(index);
                                }
                                return;
                            }
                        }
                    }
                }
            }
        }
        
        private void OnItemContextMenu(ContextualMenuPopulateEvent evt)
        {
            var element = evt.target as VisualElement;
            var itemData = element?.userData as DataTypeItem;
            
            if (itemData == null || itemData.IsCategory || itemData.DataType == null)
                return;
            
            evt.menu.AppendAction("Open in New Window", 
                _ => OpenInNewWindow(itemData.DataType),
                DropdownMenuAction.AlwaysEnabled);
                
            evt.menu.AppendAction("Open in New Tab", 
                _ => OpenInNewTab(itemData.DataType),
                DropdownMenuAction.AlwaysEnabled);
                
            evt.menu.AppendSeparator();
            
            evt.menu.AppendAction("View as Table", 
                _ => OpenAsTable(itemData.DataType),
                DropdownMenuAction.AlwaysEnabled);
                
            evt.menu.AppendSeparator();
            
            evt.menu.AppendAction("Open with System Default", 
                _ => OpenWithSystemDefault(itemData.DataType),
                DropdownMenuAction.AlwaysEnabled);
                
            evt.menu.AppendAction("Show in Explorer", 
                _ => ShowInExplorer(itemData.DataType),
                DropdownMenuAction.AlwaysEnabled);
                
            evt.menu.AppendSeparator();
            
            evt.menu.AppendAction("Export.../JSON", 
                _ => ExportData(itemData.DataType, "json"),
                DropdownMenuAction.AlwaysEnabled);
                
            evt.menu.AppendAction("Export.../CSV", 
                _ => ExportData(itemData.DataType, "csv"),
                DropdownMenuAction.AlwaysEnabled);
            
            evt.StopPropagation();
        }
        
        private void OpenInNewWindow(Type dataType)
        {
            var window = EditorWindow.GetWindow<DatraEditorWindow>();
            if (window != null)
            {
                var repository = GetRepositoryForType(window, dataType);
                if (repository != null)
                {
                    Windows.DatraDataWindow.CreateWindow(dataType, repository, GetDataContext(window), GetDataSource(window, dataType), dataType.Name);
                }
            }
        }
        
        private void OpenInNewTab(Type dataType)
        {
            var window = EditorWindow.GetWindow<DatraEditorWindow>();
            if (window != null)
            {
                var repository = GetRepositoryForType(window, dataType);
                if (repository != null)
                {
                    window.AddDataTab(dataType, repository, GetDataContext(window));
                }
            }
        }
        
        private void OpenAsTable(Type dataType)
        {
            var window = EditorWindow.GetWindow<DatraEditorWindow>();
            if (window != null)
            {
                var repository = GetRepositoryForType(window, dataType);
                if (repository != null)
                {
                    var dataWindow = Windows.DatraDataWindow.CreateWindow(dataType, repository,
                        GetDataContext(window), GetDataSource(window, dataType), dataType.Name + " - Table View");
                    dataWindow.SetInitialViewMode(Controllers.DatraViewModeController.ViewMode.Table);
                }
            }
        }
        
        private void ExportData(Type dataType, string format)
        {
            Debug.Log($"Export {dataType.Name} as {format} - Not implemented yet");
        }
        
        private void OpenWithSystemDefault(Type dataType)
        {
            var filePath = GetFilePathForDataType(dataType);
            if (!string.IsNullOrEmpty(filePath))
            {
                // Convert to absolute path if needed
                var absolutePath = System.IO.Path.GetFullPath(filePath);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = absolutePath,
                    UseShellExecute = true
                });
            }
            else
            {
                Debug.LogWarning($"Could not find file path for {dataType.Name}");
            }
        }
        
        private void ShowInExplorer(Type dataType)
        {
            var filePath = GetFilePathForDataType(dataType);
            if (!string.IsNullOrEmpty(filePath))
            {
                EditorUtility.RevealInFinder(filePath);
            }
            else
            {
                Debug.LogWarning($"Could not find file path for {dataType.Name}");
            }
        }
        
        private string GetFilePathForDataType(Type dataType)
        {
            // Try to find DataTypeItem with DataTypeInfo from tree data
            if (treeData != null)
            {
                foreach (var category in treeData)
                {
                    if (category.children == null) continue;
                    foreach (var child in category.children)
                    {
                        if (child.data == null || child.data.DataType != dataType)
                            continue;

                        var dataTypeInfo = child.data.DataTypeInfo;
                        if (dataTypeInfo != null)
                        {
                            // Prefer LoadedFilePath if available, otherwise use configured FilePath
                            var filePath = dataTypeInfo.LoadedFilePath ?? dataTypeInfo.FilePath;
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                return filePath;
                            }
                        }
                        break;
                    }
                }
            }

            return null;
        }
        
        private IEditableRepository GetRepositoryForType(DatraEditorWindow window, Type dataType)
        {
            if (window.Repositories != null && window.Repositories.TryGetValue(dataType, out var repository))
            {
                return repository;
            }
            return null;
        }

        private IEditableDataSource GetDataSource(DatraEditorWindow window, Type dataType)
        {
            window.dataSources.TryGetValue(dataType, out var source);
            return source;
        }
        
        private IDataContext GetDataContext(DatraEditorWindow window)
        {
            return window.DataContext;
        }
        
        private void RestoreLastSelection()
        {
            var lastSelectedPath = DatraUserPreferences.GetLastSelectedTreePath();
            if (string.IsNullOrEmpty(lastSelectedPath)) return;
            
            // Find the item with matching type name
            var itemToSelect = FindItemByTypeName(treeData, lastSelectedPath);
            if (itemToSelect.HasValue)
            {
                treeView.SetSelectionById(itemToSelect.Value.id);
            }
        }
        
        private TreeViewItemData<DataTypeItem>? FindItemByTypeName(
            IEnumerable<TreeViewItemData<DataTypeItem>> items, string typeName)
        {
            foreach (var item in items)
            {
                if (item.data?.DataType?.FullName == typeName)
                    return item;
                    
                if (item.hasChildren)
                {
                    var found = FindItemByTypeName(item.children, typeName);
                    if (found.HasValue)
                        return found;
                }
            }
            return null;
        }
    }
}