using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Datra.Interfaces;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.UI
{
    public class DatraReferenceSelector : EditorWindow
    {
        private Type _referencedType;
        private Action<object> _onSelected;
        private IDataContext _dataContext;
        private List<object> _availableItems = new List<object>();
        private ListView _listView;
        private TextField _searchField;
        
        public static void Show(Type referencedType, IDataContext dataContext, Action<object> onSelected)
        {
            var window = CreateInstance<DatraReferenceSelector>();
            window._referencedType = referencedType;
            window._dataContext = dataContext;
            window._onSelected = onSelected;
            window.titleContent = new GUIContent($"Select {referencedType.Name}");
            window.minSize = new Vector2(400, 300);
            window.ShowModal();
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.SetPadding(10);
            
            // Search field
            _searchField = new TextField();
            _searchField.style.marginBottom = 10;
            _searchField.RegisterValueChangedCallback(OnSearchChanged);
            root.Add(_searchField);
            
            // List view
            _listView = new ListView();
            _listView.makeItem = () =>
            {
                var container = new VisualElement();
                container.style.flexDirection = FlexDirection.Row;
                container.style.SetPadding(5);
                
                var label = new Label();
                label.style.flexGrow = 1;
                container.Add(label);
                
                return container;
            };
            
            _listView.bindItem = (element, index) =>
            {
                if (index < _availableItems.Count)
                {
                    var item = _availableItems[index];
                    var label = element.Q<Label>();
                    
                    // Try to get Id property
                    var idProperty = item.GetType().GetProperty("Id");
                    var id = idProperty?.GetValue(item)?.ToString() ?? "Unknown";
                    
                    // Try to get a display name
                    var nameProperty = item.GetType().GetProperty("Name") ??
                                      item.GetType().GetProperty("Title") ??
                                      item.GetType().GetProperty("DisplayName");
                    
                    var displayName = nameProperty?.GetValue(item)?.ToString() ?? item.ToString();
                    
                    label.text = $"{id} - {displayName}";
                }
            };
            
            _listView.selectionType = SelectionType.Single;
            _listView.style.flexGrow = 1;
            _listView.selectionChanged += OnSelectionChanged;
            
            root.Add(_listView);
            
            // Buttons
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.justifyContent = Justify.FlexEnd;
            buttonContainer.style.marginTop = 10;
            
            var selectButton = new Button(OnSelectClicked) { text = "Select" };
            selectButton.style.width = 80;
            buttonContainer.Add(selectButton);
            
            var cancelButton = new Button(() => Close()) { text = "Cancel" };
            cancelButton.style.width = 80;
            cancelButton.style.marginLeft = 10;
            buttonContainer.Add(cancelButton);
            
            root.Add(buttonContainer);
            
            // Load data
            LoadAvailableItems();
        }
        
        private void LoadAvailableItems()
        {
            _availableItems.Clear();
            
            if (_dataContext == null) return;
            
            // Find the repository that contains the referenced type
            var contextType = _dataContext.GetType();
            
            // First try to find repository through properties
            var properties = contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            object repository = null;
            
            foreach (var prop in properties)
            {
                var propType = prop.PropertyType;
                if (propType.IsGenericType)
                {
                    var genericDef = propType.GetGenericTypeDefinition();
                    if (genericDef == typeof(IDataRepository<,>))
                    {
                        var genericArgs = propType.GetGenericArguments();
                        if (genericArgs[1] == _referencedType)
                        {
                            repository = prop.GetValue(_dataContext);
                            break;
                        }
                    }
                }
            }
            
            // If not found through properties, try the internal Repositories field
            if (repository == null)
            {
                var repositories = contextType.GetField("Repositories", BindingFlags.NonPublic | BindingFlags.Instance);
                if (repositories != null)
                {
                    var repositoryDict = repositories.GetValue(_dataContext) as Dictionary<string, object>;
                    if (repositoryDict != null)
                    {
                        // Look for repository by type name
                        var typeName = _referencedType.FullName;
                        repositoryDict.TryGetValue(typeName, out repository);
                    }
                }
            }
            
            // Get all items from repository
            if (repository != null)
            {
                var getAllMethod = repository.GetType().GetMethod("GetAll");
                if (getAllMethod != null)
                {
                    var result = getAllMethod.Invoke(repository, null);
                    
                    // Handle both dictionary and enumerable results
                    if (result is System.Collections.IDictionary dict)
                    {
                        foreach (var value in dict.Values)
                        {
                            _availableItems.Add(value);
                        }
                    }
                    else if (result is System.Collections.IEnumerable items)
                    {
                        foreach (var item in items)
                        {
                            _availableItems.Add(item);
                        }
                    }
                }
            }
            
            UpdateListView();
        }
        
        private void UpdateListView()
        {
            _listView.itemsSource = _availableItems;
            _listView.Rebuild();
        }
        
        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            var searchTerm = evt.newValue.ToLower();
            
            if (string.IsNullOrEmpty(searchTerm))
            {
                UpdateListView();
                return;
            }
            
            var filtered = _availableItems.Where(item =>
            {
                var str = item.ToString().ToLower();
                
                // Also search in Id and Name properties
                var idProp = item.GetType().GetProperty("Id");
                if (idProp != null)
                {
                    var id = idProp.GetValue(item)?.ToString()?.ToLower();
                    if (!string.IsNullOrEmpty(id) && id.Contains(searchTerm))
                        return true;
                }
                
                var nameProp = item.GetType().GetProperty("Name") ??
                              item.GetType().GetProperty("Title") ??
                              item.GetType().GetProperty("DisplayName");
                
                if (nameProp != null)
                {
                    var name = nameProp.GetValue(item)?.ToString()?.ToLower();
                    if (!string.IsNullOrEmpty(name) && name.Contains(searchTerm))
                        return true;
                }
                
                return str.Contains(searchTerm);
            }).ToList();
            
            _listView.itemsSource = filtered;
            _listView.Rebuild();
        }
        
        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            // Enable/disable select button based on selection
            var selectButton = rootVisualElement.Q<Button>("select-button");
            if (selectButton != null)
            {
                selectButton.SetEnabled(selection.Any());
            }
        }
        
        private void OnSelectClicked()
        {
            var selected = _listView.selectedItem;
            if (selected != null)
            {
                // Get the Id of the selected item
                var idProperty = selected.GetType().GetProperty("Id");
                var id = idProperty?.GetValue(selected);
                
                _onSelected?.Invoke(id);
                Close();
            }
        }
    }
}