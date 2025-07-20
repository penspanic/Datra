using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Datra.Interfaces;
using Datra.Attributes;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor
{
    public class DatraEditorWindow : EditorWindow
    {
        private VisualElement root;
        private ListView dataListView;
        private ScrollView dataScrollView;
        private Label statusLabel;
        private TextField searchField;
        
        private List<DataInfo> availableData = new List<DataInfo>();
        private DataInfo selectedData;
        private IDataContext dataContext;
        private object currentRepository;
        
        private class DataInfo
        {
            public string Name { get; set; }
            public Type DataType { get; set; }
            public PropertyInfo Property { get; set; }
            public bool IsTableData { get; set; }
        }
        
        [MenuItem("Window/Datra/Data Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<DatraEditorWindow>();
            window.titleContent = new GUIContent("Datra Editor");
            window.minSize = new Vector2(1200, 600);
        }
        
        private void CreateGUI()
        {
            root = rootVisualElement;
            
            // Load USS for styling
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.datra.unity/Editor/DatraEditorWindow.uss");
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
            
            // Create main container
            var mainContainer = new VisualElement();
            mainContainer.AddToClassList("main-container");
            root.Add(mainContainer);
            
            // Create toolbar
            CreateToolbar(mainContainer);
            
            // Create split view
            var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            splitView.AddToClassList("split-view");
            mainContainer.Add(splitView);
            
            // Left panel - Data list
            CreateDataListPanel(splitView);
            
            // Right panel - Data editor
            CreateDataEditorPanel(splitView);
            
            // Status bar
            CreateStatusBar(mainContainer);
            
            // Try to auto-initialize DataContext
            TryAutoInitialize();
        }
        
        private void CreateToolbar(VisualElement parent)
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");
            parent.Add(toolbar);
            
            searchField = new TextField();
            searchField.AddToClassList("search-field");
            searchField.RegisterValueChangedCallback(OnSearchChanged);
            toolbar.Add(searchField);
            
            var refreshButton = new Button(LoadAvailableData);
            refreshButton.text = "Refresh";
            toolbar.Add(refreshButton);
            
            var loadButton = new Button(LoadDataContext);
            loadButton.text = "Load Context";
            loadButton.style.marginLeft = 10;
            toolbar.Add(loadButton);
            
            var saveButton = new Button(SaveCurrentData);
            saveButton.text = "Save";
            saveButton.style.marginLeft = 10;
            toolbar.Add(saveButton);
        }
        
        private void CreateDataListPanel(TwoPaneSplitView splitView)
        {
            var container = new VisualElement();
            container.AddToClassList("data-list-container");
            
            var header = new Label("Data Types");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 10;
            header.style.marginBottom = 10;
            header.style.marginLeft = 10;
            container.Add(header);
            
            dataListView = new ListView();
            dataListView.makeItem = () => new Label();
            dataListView.bindItem = (element, index) =>
            {
                var label = element as Label;
                if (index < availableData.Count)
                {
                    label.text = availableData[index].Name;
                }
            };
            dataListView.selectionType = SelectionType.Single;
            dataListView.onSelectionChange += OnDataSelected;
            dataListView.style.flexGrow = 1;
            
            container.Add(dataListView);
            splitView.Add(container);
        }
        
        private void CreateDataEditorPanel(TwoPaneSplitView splitView)
        {
            var container = new VisualElement();
            container.AddToClassList("data-editor-container");
            
            dataScrollView = new ScrollView();
            dataScrollView.style.flexGrow = 1;
            container.Add(dataScrollView);
            
            splitView.Add(container);
        }
        
        private void CreateStatusBar(VisualElement parent)
        {
            statusLabel = new Label("Ready");
            statusLabel.AddToClassList("status-bar");
            parent.Add(statusLabel);
        }
        
        private void LoadAvailableData()
        {
            availableData.Clear();
            
            if (dataContext == null)
            {
                UpdateStatus("No DataContext loaded. Click 'Load Context' to select one.");
                return;
            }
            
            var contextType = dataContext.GetType();
            var properties = contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                if (IsRepositoryProperty(property))
                {
                    var dataType = GetDataType(property);
                    if (dataType != null)
                    {
                        availableData.Add(new DataInfo
                        {
                            Name = property.Name,
                            DataType = dataType,
                            Property = property,
                            IsTableData = IsTableData(dataType)
                        });
                    }
                }
            }
            
            availableData = availableData.OrderBy(d => d.Name).ToList();
            dataListView.itemsSource = availableData;
            dataListView.Rebuild();
            
            UpdateStatus($"Loaded {availableData.Count} data types");
        }
        
        private void LoadDataContext()
        {
            var initializers = DatraBootstrapper.FindInitializers();
            
            if (initializers.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", 
                    "No Datra initialization methods found.\n\n" +
                    "Please create a static method with [DatraEditorInit] attribute that returns an IDataContext.", 
                    "OK");
                return;
            }
            
            if (initializers.Count == 1)
            {
                // Only one initializer, use it directly
                var initializer = initializers[0];
                dataContext = DatraBootstrapper.ExecuteInitializer(initializer);
                
                if (dataContext != null)
                {
                    LoadAvailableData();
                }
            }
            else
            {
                // Multiple initializers, show selection dialog
                var menu = new GenericMenu();
                
                foreach (var initializer in initializers)
                {
                    var init = initializer; // Capture for closure
                    menu.AddItem(new GUIContent(init.DisplayName), false, () =>
                    {
                        dataContext = DatraBootstrapper.ExecuteInitializer(init);
                        if (dataContext != null)
                        {
                            LoadAvailableData();
                        }
                    });
                }
                
                menu.ShowAsContext();
            }
        }
        
        private bool IsRepositoryProperty(PropertyInfo property)
        {
            var type = property.PropertyType;
            
            // Check for non-generic ISingleDataRepository first
            if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISingleDataRepository<>)))
            {
                return true;
            }
            
            if (!type.IsGenericType) return false;
            
            var genericType = type.GetGenericTypeDefinition();
            return genericType == typeof(IDataRepository<,>) || 
                   genericType == typeof(ISingleDataRepository<>) ||
                   type.GetInterfaces().Any(i => i.IsGenericType && 
                       (i.GetGenericTypeDefinition() == typeof(IDataRepository<,>) || 
                        i.GetGenericTypeDefinition() == typeof(ISingleDataRepository<>)));
        }
        
        private Type GetDataType(PropertyInfo property)
        {
            var type = property.PropertyType;
            
            // Check if it's ISingleDataRepository<T> interface implementation
            var singleRepoInterface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISingleDataRepository<>));
            
            if (singleRepoInterface != null)
            {
                return singleRepoInterface.GetGenericArguments()[0];
            }
            
            if (!type.IsGenericType) return null;
            
            var genericArgs = type.GetGenericArguments();
            return genericArgs.Last(); // Last generic argument is the data type
        }
        
        private bool IsTableData(Type type)
        {
            return type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITableData<>));
        }
        
        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            var searchTerm = evt.newValue.ToLower();
            
            if (string.IsNullOrEmpty(searchTerm))
            {
                dataListView.itemsSource = availableData;
            }
            else
            {
                var filtered = availableData.Where(d => d.Name.ToLower().Contains(searchTerm)).ToList();
                dataListView.itemsSource = filtered;
            }
            
            dataListView.Rebuild();
        }
        
        private void OnDataSelected(IEnumerable<object> selection)
        {
            var selected = selection.FirstOrDefault() as DataInfo;
            if (selected == null) return;
            
            selectedData = selected;
            LoadDataForEditing(selected);
        }
        
        private void LoadDataForEditing(DataInfo dataInfo)
        {
            try
            {
                currentRepository = dataInfo.Property.GetValue(dataContext);
                if (currentRepository == null)
                {
                    UpdateStatus($"Repository for {dataInfo.Name} is not loaded");
                    return;
                }
                
                DisplayData();
                UpdateStatus($"Loaded {dataInfo.Name} for editing");
            }
            catch (Exception e)
            {
                UpdateStatus($"Failed to load {dataInfo.Name}: {e.Message}");
                Debug.LogError(e);
            }
        }
        
        private void DisplayData()
        {
            dataScrollView.Clear();
            
            if (selectedData == null || currentRepository == null) return;
            
            // Add header
            var headerLabel = new Label(selectedData.Name);
            headerLabel.style.fontSize = 16;
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.marginBottom = 10;
            dataScrollView.Add(headerLabel);
            
            if (selectedData.IsTableData)
            {
                DisplayTableData();
            }
            else
            {
                DisplaySingleData();
            }
        }
        
        private void DisplayTableData()
        {
            // Add "Add Item" button
            var addButton = new Button(() => AddNewItem());
            addButton.text = "+ Add New Item";
            addButton.AddToClassList("add-item-button");
            dataScrollView.Add(addButton);
            
            // Get all items from repository
            var getAllMethod = currentRepository.GetType().GetMethod("GetAll");
            var items = getAllMethod.Invoke(currentRepository, null) as System.Collections.IEnumerable;
            
            if (items != null)
            {
                int index = 0;
                foreach (var item in items)
                {
                    var itemContainer = CreateItemElement(item, index++);
                    dataScrollView.Add(itemContainer);
                }
            }
        }
        
        private void DisplaySingleData()
        {
            var getMethod = currentRepository.GetType().GetMethod("Get");
            var data = getMethod.Invoke(currentRepository, null);
            
            if (data != null)
            {
                var properties = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                
                foreach (var property in properties)
                {
                    if (property.CanWrite)
                    {
                        var fieldContainer = CreatePropertyField(data, property);
                        dataScrollView.Add(fieldContainer);
                    }
                }
            }
        }
        
        private VisualElement CreateItemElement(object item, int index)
        {
            var itemContainer = new VisualElement();
            itemContainer.AddToClassList("data-item");
            
            // Add item header
            var itemHeader = new VisualElement();
            itemHeader.AddToClassList("item-header");
            
            var idProperty = item.GetType().GetProperty("Id");
            var idValue = idProperty?.GetValue(item)?.ToString() ?? "Unknown";
            
            var idLabel = new Label($"Item {index + 1} - ID: {idValue}");
            idLabel.AddToClassList("item-id-label");
            itemHeader.Add(idLabel);
            
            // Add delete button
            var deleteButton = new Button(() => DeleteItem(item));
            deleteButton.text = "Delete";
            deleteButton.AddToClassList("delete-item-button");
            itemHeader.Add(deleteButton);
            
            itemContainer.Add(itemHeader);
            
            // Create fields container
            var fieldsContainer = new VisualElement();
            fieldsContainer.AddToClassList("item-fields");
            
            // Create fields for each property
            var properties = item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.CanWrite && property.Name != "Id") // Id is usually read-only
                {
                    var fieldContainer = CreatePropertyField(item, property);
                    fieldsContainer.Add(fieldContainer);
                }
            }
            
            itemContainer.Add(fieldsContainer);
            
            return itemContainer;
        }
        
        private VisualElement CreatePropertyField(object target, PropertyInfo property)
        {
            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("property-field");
            
            var label = new Label(property.Name);
            label.AddToClassList("field-label");
            fieldContainer.Add(label);
            
            var value = property.GetValue(target);
            var propertyType = property.PropertyType;
            
            // Handle different property types
            if (propertyType == typeof(string))
            {
                var textField = new TextField();
                textField.value = value as string ?? "";
                textField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                });
                fieldContainer.Add(textField);
            }
            else if (propertyType == typeof(int))
            {
                var intField = new IntegerField();
                intField.value = (int)(value ?? 0);
                intField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                });
                fieldContainer.Add(intField);
            }
            else if (propertyType == typeof(float))
            {
                var floatField = new FloatField();
                floatField.value = (float)(value ?? 0f);
                floatField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                });
                fieldContainer.Add(floatField);
            }
            else if (propertyType == typeof(bool))
            {
                var toggle = new Toggle();
                toggle.value = (bool)(value ?? false);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                });
                fieldContainer.Add(toggle);
            }
            // Add more type handlers as needed (IntDataRef, StringDataRef, etc.)
            
            return fieldContainer;
        }
        
        private void AddNewItem()
        {
            // This is a placeholder - in real implementation, you'd create a new instance
            // and add it to the repository
            UpdateStatus("Add new item functionality not yet implemented");
        }
        
        private void DeleteItem(object item)
        {
            // This is a placeholder - in real implementation, you'd remove the item
            // from the repository
            UpdateStatus("Delete item functionality not yet implemented");
        }
        
        private void SaveCurrentData()
        {
            if (dataContext == null)
            {
                UpdateStatus("No DataContext loaded");
                return;
            }
            
            SaveDataAsync();
        }
        
        private async void SaveDataAsync()
        {
            try
            {
                UpdateStatus("Saving data...");
                await dataContext.SaveAllAsync();
                UpdateStatus("Data saved successfully");
                
                // Show confirmation dialog
                EditorUtility.DisplayDialog("Success", "Data saved successfully!", "OK");
            }
            catch (Exception e)
            {
                UpdateStatus($"Failed to save data: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to save data:\n{e.Message}", "OK");
                Debug.LogError($"Failed to save data: {e}");
            }
        }
        
        private void UpdateStatus(string message)
        {
            statusLabel.text = $"[{DateTime.Now:HH:mm:ss}] {message}";
        }
        
        private void TryAutoInitialize()
        {
            // Try to auto-initialize DataContext using bootstrapper
            dataContext = DatraBootstrapper.AutoInitialize();
            
            if (dataContext != null)
            {
                LoadAvailableData();
                UpdateStatus("DataContext auto-initialized successfully");
            }
            else
            {
                UpdateStatus("No DataContext loaded. Click 'Load Context' to initialize.");
            }
        }
    }
}