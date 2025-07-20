using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.UIElements;
using Datra.DataTypes;
using UnityEditor;

namespace Datra.Unity.Editor.UI
{
    public static class DatraPropertyField
    {
        public static VisualElement CreateField(object target, PropertyInfo property, Action onChanged = null)
        {
            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("property-field");
            
            var label = new Label(property.Name);
            label.AddToClassList("field-label");
            fieldContainer.Add(label);
            
            var value = property.GetValue(target);
            var propertyType = property.PropertyType;
            
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            
            // Create appropriate field based on type
            VisualElement field = null;
            
            if (underlyingType == typeof(string))
            {
                field = CreateStringField(target, property, value as string, onChanged);
            }
            else if (underlyingType == typeof(int))
            {
                field = CreateIntField(target, property, value, onChanged);
            }
            else if (underlyingType == typeof(float))
            {
                field = CreateFloatField(target, property, value, onChanged);
            }
            else if (underlyingType == typeof(bool))
            {
                field = CreateBoolField(target, property, value, onChanged);
            }
            else if (underlyingType.IsEnum)
            {
                field = CreateEnumField(target, property, value, underlyingType, onChanged);
            }
            else if (typeof(IDataRef).IsAssignableFrom(underlyingType))
            {
                field = CreateDataRefField(target, property, value, underlyingType, onChanged);
            }
            else if (IsListType(underlyingType))
            {
                field = CreateListField(target, property, value, underlyingType, onChanged);
            }
            else if (IsArrayType(underlyingType))
            {
                field = CreateArrayField(target, property, value, underlyingType, onChanged);
            }
            else
            {
                // For complex types, try to create nested fields
                field = CreateComplexField(target, property, value, underlyingType, onChanged);
            }
            
            if (field != null)
            {
                fieldContainer.Add(field);
            }
            
            return fieldContainer;
        }
        
        private static TextField CreateStringField(object target, PropertyInfo property, string value, Action onChanged)
        {
            var textField = new TextField();
            textField.value = value ?? "";
            textField.RegisterValueChangedCallback(evt =>
            {
                property.SetValue(target, evt.newValue);
                onChanged?.Invoke();
            });
            return textField;
        }
        
        private static IntegerField CreateIntField(object target, PropertyInfo property, object value, Action onChanged)
        {
            var intField = new IntegerField();
            intField.value = value != null ? Convert.ToInt32(value) : 0;
            intField.RegisterValueChangedCallback(evt =>
            {
                property.SetValue(target, evt.newValue);
                onChanged?.Invoke();
            });
            return intField;
        }
        
        private static FloatField CreateFloatField(object target, PropertyInfo property, object value, Action onChanged)
        {
            var floatField = new FloatField();
            floatField.value = value != null ? Convert.ToSingle(value) : 0f;
            floatField.RegisterValueChangedCallback(evt =>
            {
                property.SetValue(target, evt.newValue);
                onChanged?.Invoke();
            });
            return floatField;
        }
        
        private static Toggle CreateBoolField(object target, PropertyInfo property, object value, Action onChanged)
        {
            var toggle = new Toggle();
            toggle.value = value != null && Convert.ToBoolean(value);
            toggle.RegisterValueChangedCallback(evt =>
            {
                property.SetValue(target, evt.newValue);
                onChanged?.Invoke();
            });
            return toggle;
        }
        
        private static EnumField CreateEnumField(object target, PropertyInfo property, object value, Type enumType, Action onChanged)
        {
            var enumField = new EnumField((Enum)(value ?? Enum.GetValues(enumType).GetValue(0)));
            enumField.RegisterValueChangedCallback(evt =>
            {
                property.SetValue(target, evt.newValue);
                onChanged?.Invoke();
            });
            return enumField;
        }
        
        private static VisualElement CreateDataRefField(object target, PropertyInfo property, object value, Type dataRefType, Action onChanged)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            
            // Get the generic type argument (the referenced data type)
            var referencedType = dataRefType.GetGenericArguments()[0];
            
            var textField = new TextField();
            textField.isReadOnly = true;
            textField.style.flexGrow = 1;
            
            // Display current value
            if (value != null)
            {
                var idProperty = value.GetType().GetProperty("Id");
                if (idProperty != null)
                {
                    textField.value = idProperty.GetValue(value)?.ToString() ?? "";
                }
            }
            
            var selectButton = new Button(() =>
            {
                ShowDataRefSelector(referencedType, (selectedId) =>
                {
                    // Create new DataRef instance
                    var dataRef = CreateDataRef(dataRefType, selectedId);
                    property.SetValue(target, dataRef);
                    textField.value = selectedId?.ToString() ?? "";
                    onChanged?.Invoke();
                });
            });
            selectButton.text = "...";
            selectButton.style.width = 30;
            
            var clearButton = new Button(() =>
            {
                property.SetValue(target, null);
                textField.value = "";
                onChanged?.Invoke();
            });
            clearButton.text = "×";
            clearButton.style.width = 20;
            
            container.Add(textField);
            container.Add(selectButton);
            container.Add(clearButton);
            
            return container;
        }
        
        private static VisualElement CreateListField(object target, PropertyInfo property, object value, Type listType, Action onChanged)
        {
            var container = new VisualElement();
            container.AddToClassList("list-field-container");
            
            var elementType = listType.GetGenericArguments()[0];
            var list = value as IList ?? Activator.CreateInstance(listType) as IList;
            
            var itemsContainer = new VisualElement();
            itemsContainer.AddToClassList("list-items");
            
            void RefreshList()
            {
                itemsContainer.Clear();
                
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var index = i;
                        var itemContainer = new VisualElement();
                        itemContainer.style.flexDirection = FlexDirection.Row;
                        itemContainer.style.marginBottom = 5;
                        
                        // Create field for list item
                        var itemField = CreateFieldForType(list[index], elementType, (newValue) =>
                        {
                            list[index] = newValue;
                            onChanged?.Invoke();
                        });
                        itemField.style.flexGrow = 1;
                        
                        var removeButton = new Button(() =>
                        {
                            list.RemoveAt(index);
                            property.SetValue(target, list);
                            RefreshList();
                            onChanged?.Invoke();
                        });
                        removeButton.text = "×";
                        removeButton.style.width = 20;
                        
                        itemContainer.Add(itemField);
                        itemContainer.Add(removeButton);
                        itemsContainer.Add(itemContainer);
                    }
                }
            }
            
            RefreshList();
            container.Add(itemsContainer);
            
            var addButton = new Button(() =>
            {
                if (list == null)
                {
                    list = Activator.CreateInstance(listType) as IList;
                    property.SetValue(target, list);
                }
                
                var newItem = CreateDefaultValue(elementType);
                list.Add(newItem);
                RefreshList();
                onChanged?.Invoke();
            });
            addButton.text = "+ Add Item";
            addButton.AddToClassList("add-list-item-button");
            container.Add(addButton);
            
            return container;
        }
        
        private static VisualElement CreateArrayField(object target, PropertyInfo property, object value, Type arrayType, Action onChanged)
        {
            var container = new VisualElement();
            container.AddToClassList("array-field-container");
            
            var elementType = arrayType.GetElementType();
            var array = value as Array;
            
            var itemsContainer = new VisualElement();
            itemsContainer.AddToClassList("array-items");
            
            void RefreshArray()
            {
                itemsContainer.Clear();
                
                if (array != null)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        var index = i;
                        var itemContainer = new VisualElement();
                        itemContainer.style.flexDirection = FlexDirection.Row;
                        itemContainer.style.marginBottom = 5;
                        
                        // Create field for array item
                        var itemField = CreateFieldForType(array.GetValue(index), elementType, (newValue) =>
                        {
                            array.SetValue(newValue, index);
                            onChanged?.Invoke();
                        });
                        itemField.style.flexGrow = 1;
                        
                        itemContainer.Add(itemField);
                        itemsContainer.Add(itemContainer);
                    }
                }
            }
            
            RefreshArray();
            container.Add(itemsContainer);
            
            // Array size field
            var sizeContainer = new VisualElement();
            sizeContainer.style.flexDirection = FlexDirection.Row;
            sizeContainer.style.marginTop = 5;
            
            var sizeLabel = new Label("Size:");
            sizeLabel.style.width = 40;
            
            var sizeField = new IntegerField();
            sizeField.value = array?.Length ?? 0;
            sizeField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue >= 0)
                {
                    var newArray = Array.CreateInstance(elementType, evt.newValue);
                    if (array != null)
                    {
                        Array.Copy(array, 0, newArray, 0, Math.Min(array.Length, evt.newValue));
                    }
                    array = newArray;
                    property.SetValue(target, array);
                    RefreshArray();
                    onChanged?.Invoke();
                }
            });
            
            sizeContainer.Add(sizeLabel);
            sizeContainer.Add(sizeField);
            container.Add(sizeContainer);
            
            return container;
        }
        
        private static VisualElement CreateComplexField(object target, PropertyInfo property, object value, Type type, Action onChanged)
        {
            var container = new VisualElement();
            container.AddToClassList("complex-field-container");
            
            if (value == null)
            {
                var createButton = new Button(() =>
                {
                    value = Activator.CreateInstance(type);
                    property.SetValue(target, value);
                    container.Clear();
                    container.Add(CreateComplexField(target, property, value, type, onChanged));
                    onChanged?.Invoke();
                });
                createButton.text = $"Create {type.Name}";
                container.Add(createButton);
            }
            else
            {
                // Create fields for all properties of the complex type
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties)
                {
                    if (prop.CanWrite)
                    {
                        var propField = CreateField(value, prop, onChanged);
                        container.Add(propField);
                    }
                }
            }
            
            return container;
        }
        
        private static VisualElement CreateFieldForType(object value, Type type, Action<object> onChanged)
        {
            if (type == typeof(string))
            {
                var field = new TextField { value = value as string ?? "" };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            else if (type == typeof(int))
            {
                var field = new IntegerField { value = value != null ? Convert.ToInt32(value) : 0 };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            else if (type == typeof(float))
            {
                var field = new FloatField { value = value != null ? Convert.ToSingle(value) : 0f };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            else if (type == typeof(bool))
            {
                var field = new Toggle { value = value != null && Convert.ToBoolean(value) };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            else
            {
                var field = new TextField { value = value?.ToString() ?? "", isReadOnly = true };
                return field;
            }
        }
        
        private static object CreateDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            else if (type == typeof(string))
            {
                return "";
            }
            else
            {
                try
                {
                    return Activator.CreateInstance(type);
                }
                catch
                {
                    return null;
                }
            }
        }
        
        private static bool IsListType(Type type)
        {
            return type.IsGenericType && 
                   (type.GetGenericTypeDefinition() == typeof(List<>) ||
                    type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)));
        }
        
        private static bool IsArrayType(Type type)
        {
            return type.IsArray;
        }
        
        private static object CreateDataRef(Type dataRefType, object id)
        {
            if (id == null) return null;
            
            // Create instance of IntDataRef<T> or StringDataRef<T>
            return Activator.CreateInstance(dataRefType, id);
        }
        
        private static void ShowDataRefSelector(Type referencedType, Action<object> onSelected)
        {
            // This would open a selector window showing all available instances of the referenced type
            // For now, just show a dialog
            EditorUtility.DisplayDialog("DataRef Selector", $"Select {referencedType.Name} - Not yet implemented", "OK");
        }
    }
}