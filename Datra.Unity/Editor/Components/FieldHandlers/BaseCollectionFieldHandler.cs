using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.Attributes;
using Datra.Localization;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Base class for collection field handlers (Array, List, Dictionary)
    /// Provides common UI scaffolding and polymorphism support via TypeCache
    /// </summary>
    public abstract class BaseCollectionFieldHandler : IFieldTypeHandler
    {
        public abstract int Priority { get; }
        public abstract bool CanHandle(Type type, MemberInfo member = null);

        /// <summary>
        /// Get the element type of the collection
        /// </summary>
        protected abstract Type GetElementType(Type collectionType);

        /// <summary>
        /// Get elements from the collection as a list
        /// </summary>
        protected abstract IList GetElementsAsList(object collection);

        /// <summary>
        /// Create a new collection from elements
        /// </summary>
        protected abstract object CreateCollectionFromList(IList elements, Type elementType);

        /// <summary>
        /// Get display text for the collection (e.g., "[3 items]")
        /// </summary>
        protected abstract string GetCollectionDisplayText(object collection);

        /// <summary>
        /// Get default value for new element
        /// </summary>
        protected virtual object GetDefaultValue(Type elementType)
        {
            if (elementType == typeof(string)) return "";
            if (elementType.IsValueType) return Activator.CreateInstance(elementType);
            if (!elementType.IsAbstract && !elementType.IsInterface)
            {
                try { return Activator.CreateInstance(elementType); }
                catch { return null; }
            }
            return null;
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var elementType = GetElementType(context.FieldType);
            var collection = context.Value;

            var container = new VisualElement();
            container.AddToClassList("collection-field-container");

            if (context.LayoutMode == DatraFieldLayoutMode.Table)
            {
                return CreateCompactDisplay(container, collection, elementType, context);
            }

            return CreateFullLayout(container, collection, elementType, context);
        }

        #region Compact Display (Table Mode)

        protected virtual VisualElement CreateCompactDisplay(
            VisualElement container,
            object collection,
            Type elementType,
            FieldCreationContext context)
        {
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var displayText = GetCollectionDisplayText(collection);

            // Display field (declared first for lambda capture)
            var displayField = new TextField();
            displayField.isReadOnly = true;
            displayField.value = displayText;
            displayField.style.flexGrow = 1;
            displayField.AddToClassList("collection-compact-display");

            // Edit button
            var editButton = new Button(() =>
            {
                if (context.Property != null && context.Target != null)
                {
                    DatraPropertyEditorPopup.ShowEditor(context.Property, context.Target, () =>
                    {
                        var newCollection = context.Property.GetValue(context.Target);
                        displayField.value = GetCollectionDisplayText(newCollection);
                        context.OnValueChanged?.Invoke(newCollection);
                    });
                }
            });
            editButton.text = "✏";
            editButton.tooltip = $"Edit collection ({displayText})";
            editButton.AddToClassList("collection-edit-button");
            editButton.style.marginRight = 4;

            container.Add(editButton);
            container.Add(displayField);
            return container;
        }

        #endregion

        #region Full Layout (Form Mode)

        protected virtual VisualElement CreateFullLayout(
            VisualElement container,
            object collection,
            Type elementType,
            FieldCreationContext context)
        {
            container.style.flexDirection = FlexDirection.Column;

            var elements = collection != null ? GetElementsAsList(collection) : new List<object>();

            // Elements container
            var elementsContainer = new VisualElement();
            elementsContainer.AddToClassList("collection-elements");

            // Add existing elements
            for (int i = 0; i < elements.Count; i++)
            {
                var elementContainer = CreateElementContainer(i, elements[i], elementType, container, context);
                elementsContainer.Add(elementContainer);
            }

            Foldout foldout = null;

            if (context.IsPopupEditor)
            {
                // Popup mode: no foldout, direct layout
                // Add button at top (so it's always visible)
                var headerContainer = CreateHeaderContainerSimple(elements.Count, elementType, container, context);
                headerContainer.style.marginBottom = 8;
                container.Add(headerContainer);

                elementsContainer.style.marginLeft = 0;
                container.Add(elementsContainer);
            }
            else
            {
                // Form mode: wrap in foldout
                foldout = new Foldout();
                foldout.text = GetFoldoutText(elements, elementType);
                foldout.value = false; // Collapsed by default
                foldout.AddToClassList("collection-foldout");

                elementsContainer.style.marginLeft = 8;
                foldout.Add(elementsContainer);

                // Header container (inside foldout)
                var headerContainer = CreateHeaderContainer(elements.Count, elementType, container, foldout, context);
                foldout.Add(headerContainer);

                container.Add(foldout);
            }

            container.userData = new CollectionUserData
            {
                ElementsContainer = elementsContainer,
                ElementType = elementType,
                Foldout = foldout
            };

            return container;
        }

        protected virtual VisualElement CreateHeaderContainer(
            int count,
            Type elementType,
            VisualElement collectionContainer,
            Foldout foldout,
            FieldCreationContext context)
        {
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("collection-header");
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.justifyContent = Justify.FlexEnd;
            headerContainer.style.alignItems = Align.Center;
            headerContainer.style.marginTop = 4;
            headerContainer.style.marginBottom = 4;

            // Check if element type is polymorphic
            var derivedTypes = GetDerivedTypes(elementType);

            if (derivedTypes.Count > 1)
            {
                // Polymorphic: dropdown + add button
                var addContainer = new VisualElement();
                addContainer.style.flexDirection = FlexDirection.Row;
                addContainer.style.alignItems = Align.Center;

                var typeDropdown = new PopupField<Type>(
                    derivedTypes,
                    0,
                    FormatTypeName,
                    FormatTypeName);
                typeDropdown.AddToClassList("collection-type-dropdown");
                typeDropdown.style.minWidth = 100;
                addContainer.Add(typeDropdown);

                var addButton = new Button(() =>
                {
                    var selectedType = typeDropdown.value;
                    AddElement(collectionContainer, selectedType, context);
                    foldout.value = true; // Expand when adding
                });
                addButton.text = "+";
                addButton.tooltip = "Add element";
                addButton.AddToClassList("collection-add-button");
                addButton.style.width = 24;
                addButton.style.height = 20;
                addButton.style.marginLeft = 4;
                addContainer.Add(addButton);

                headerContainer.Add(addContainer);
            }
            else
            {
                // Non-polymorphic: simple add button
                var addButton = new Button(() =>
                {
                    AddElement(collectionContainer, elementType, context);
                    foldout.value = true; // Expand when adding
                });
                addButton.text = "+";
                addButton.tooltip = "Add element";
                addButton.AddToClassList("collection-add-button");
                addButton.style.width = 24;
                addButton.style.height = 20;
                headerContainer.Add(addButton);
            }

            return headerContainer;
        }

        /// <summary>
        /// Create header container for popup mode (no foldout)
        /// </summary>
        protected virtual VisualElement CreateHeaderContainerSimple(
            int count,
            Type elementType,
            VisualElement collectionContainer,
            FieldCreationContext context)
        {
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("collection-header");
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.justifyContent = Justify.FlexEnd;
            headerContainer.style.alignItems = Align.Center;
            headerContainer.style.marginTop = 8;

            // Check if element type is polymorphic
            var derivedTypes = GetDerivedTypes(elementType);

            if (derivedTypes.Count > 1)
            {
                // Polymorphic: dropdown + add button
                var addContainer = new VisualElement();
                addContainer.style.flexDirection = FlexDirection.Row;
                addContainer.style.alignItems = Align.Center;

                var typeDropdown = new PopupField<Type>(
                    derivedTypes,
                    0,
                    FormatTypeName,
                    FormatTypeName);
                typeDropdown.AddToClassList("collection-type-dropdown");
                typeDropdown.style.minWidth = 100;
                addContainer.Add(typeDropdown);

                var addButton = new Button(() =>
                {
                    var selectedType = typeDropdown.value;
                    AddElement(collectionContainer, selectedType, context);
                });
                addButton.text = "+";
                addButton.tooltip = "Add element";
                addButton.AddToClassList("collection-add-button");
                addButton.style.width = 24;
                addButton.style.height = 20;
                addButton.style.marginLeft = 4;
                addContainer.Add(addButton);

                headerContainer.Add(addContainer);
            }
            else
            {
                // Non-polymorphic: simple add button
                var addButton = new Button(() => AddElement(collectionContainer, elementType, context));
                addButton.text = "+";
                addButton.tooltip = "Add element";
                addButton.AddToClassList("collection-add-button");
                addButton.style.width = 24;
                addButton.style.height = 20;
                headerContainer.Add(addButton);
            }

            return headerContainer;
        }

        protected virtual VisualElement CreateElementContainer(
            int index,
            object element,
            Type declaredElementType,
            VisualElement collectionContainer,
            FieldCreationContext context)
        {
            var actualType = element?.GetType() ?? declaredElementType;

            var elementContainer = new VisualElement();
            elementContainer.AddToClassList("collection-element");
            elementContainer.style.flexDirection = FlexDirection.Column;
            elementContainer.style.marginBottom = 4;
            elementContainer.style.paddingLeft = 8;
            elementContainer.style.paddingRight = 8;
            elementContainer.style.paddingTop = 4;
            elementContainer.style.paddingBottom = 4;
            elementContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            elementContainer.style.borderLeftWidth = 2;
            elementContainer.style.borderLeftColor = new Color(0.4f, 0.6f, 0.8f, 0.8f);
            elementContainer.userData = element;

            // Header row with index, type name, reorder buttons, and remove button
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            var indexLabel = new Label($"[{index}]");
            indexLabel.AddToClassList("collection-element-index");
            indexLabel.style.minWidth = 30;
            indexLabel.style.marginRight = 8;
            indexLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(indexLabel);

            // Show type name for polymorphic types
            if (IsPolymorphicType(declaredElementType))
            {
                var typeLabel = new Label(FormatTypeName(actualType));
                typeLabel.AddToClassList("collection-element-type");
                typeLabel.style.color = new Color(0.6f, 0.8f, 1f);
                typeLabel.style.marginRight = 8;
                headerRow.Add(typeLabel);
            }

            // Spacer
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            headerRow.Add(spacer);

            // Move up button
            var moveUpButton = new Button(() => MoveElement(elementContainer, collectionContainer, declaredElementType, context, -1));
            moveUpButton.text = "▲";
            moveUpButton.tooltip = "Move up";
            moveUpButton.AddToClassList("collection-move-button");
            moveUpButton.style.width = 20;
            moveUpButton.style.height = 20;
            moveUpButton.style.marginRight = 2;
            headerRow.Add(moveUpButton);

            // Move down button
            var moveDownButton = new Button(() => MoveElement(elementContainer, collectionContainer, declaredElementType, context, 1));
            moveDownButton.text = "▼";
            moveDownButton.tooltip = "Move down";
            moveDownButton.AddToClassList("collection-move-button");
            moveDownButton.style.width = 20;
            moveDownButton.style.height = 20;
            moveDownButton.style.marginRight = 4;
            headerRow.Add(moveDownButton);

            // Remove button
            var removeButton = new Button(() => RemoveElement(elementContainer, collectionContainer, declaredElementType, context));
            removeButton.text = "−";
            removeButton.tooltip = "Remove element";
            removeButton.AddToClassList("collection-remove-button");
            removeButton.style.width = 20;
            removeButton.style.height = 20;
            headerRow.Add(removeButton);

            elementContainer.Add(headerRow);

            // Create foldout for fields (expanded by default)
            var foldout = new Foldout();
            foldout.text = "";
            foldout.value = true; // Expanded by default
            foldout.style.marginLeft = 0;
            foldout.style.marginTop = 0;

            // Hide the foldout's own toggle text (we use our header row instead)
            var toggle = foldout.Q<Toggle>();
            if (toggle != null)
            {
                toggle.style.marginBottom = 0;
                toggle.style.marginTop = 0;
            }

            // Fields container inside foldout
            var fieldsContainer = new VisualElement();
            fieldsContainer.AddToClassList("collection-element-fields");

            if (element != null)
            {
                CreateFieldsForElement(fieldsContainer, element, actualType, collectionContainer, declaredElementType, context, index);
            }

            foldout.Add(fieldsContainer);
            elementContainer.Add(foldout);

            return elementContainer;
        }

        protected virtual void CreateFieldsForElement(
            VisualElement fieldsContainer,
            object element,
            Type actualType,
            VisualElement collectionContainer,
            Type declaredElementType,
            FieldCreationContext context,
            int elementIndex = -1)
        {
            // For primitive types, create a single field
            if (IsPrimitiveOrSimpleType(actualType))
            {
                var field = CreateSimpleField(actualType, element, newValue =>
                {
                    UpdateElementInContainer(fieldsContainer.parent, newValue, collectionContainer, declaredElementType, context);
                });
                fieldsContainer.Add(field);
                return;
            }

            // For complex types, create fields for each property
            // Include writable properties and read-only properties with [NestedLocale] attribute
            var properties = actualType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && (p.CanWrite || p.GetCustomAttribute<NestedLocaleAttribute>() != null))
                .ToList();

            foreach (var prop in properties)
            {
                var propContainer = new VisualElement();
                propContainer.style.flexDirection = FlexDirection.Row;
                propContainer.style.alignItems = Align.Center;
                propContainer.style.marginBottom = 2;

                var label = new Label(ObjectNames.NicifyVariableName(prop.Name));
                label.style.minWidth = 100;
                label.style.marginRight = 8;
                propContainer.Add(label);

                var propValue = prop.GetValue(element);
                VisualElement propField;

                // Check if this property needs special handling via FieldTypeRegistry
                var hasNestedLocale = prop.GetCustomAttribute<NestedLocaleAttribute>() != null;
                if (hasNestedLocale && prop.PropertyType == typeof(NestedLocaleRef) && context.LocaleProvider != null)
                {
                    // Create a context with collection element info for nested locale evaluation
                    var nestedContext = new FieldCreationContext(
                        prop,
                        element,
                        propValue,
                        DatraFieldLayoutMode.Form,
                        newValue =>
                        {
                            // NestedLocaleRef is read-only, changes are tracked via LocalizationContext
                            UpdateCollectionValue(collectionContainer, declaredElementType, context);
                        },
                        context.LocaleProvider)
                    {
                        CollectionElementIndex = elementIndex,
                        RootDataObject = context.Target,  // The root data object (e.g., QuestData)
                        CollectionElement = element       // The element containing the nested locale
                    };

                    propField = FieldTypeRegistry.CreateField(nestedContext);
                }
                else if (prop.CanWrite)
                {
                    propField = CreateSimpleField(prop.PropertyType, propValue, newValue =>
                    {
                        prop.SetValue(element, newValue);
                        UpdateCollectionValue(collectionContainer, declaredElementType, context);
                    });
                }
                else
                {
                    // Read-only property without special handling - skip
                    continue;
                }

                propField.style.flexGrow = 1;
                propContainer.Add(propField);

                fieldsContainer.Add(propContainer);
            }

            // Also handle public fields
            var fields = actualType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.Name.Contains("<"))  // Skip backing fields
                .ToList();

            foreach (var field in fields)
            {
                var fieldContainer = new VisualElement();
                fieldContainer.style.flexDirection = FlexDirection.Row;
                fieldContainer.style.alignItems = Align.Center;
                fieldContainer.style.marginBottom = 2;

                var label = new Label(ObjectNames.NicifyVariableName(field.Name));
                label.style.minWidth = 100;
                label.style.marginRight = 8;
                fieldContainer.Add(label);

                var fieldValue = field.GetValue(element);
                var fieldElement = CreateSimpleField(field.FieldType, fieldValue, newValue =>
                {
                    field.SetValue(element, newValue);
                    UpdateCollectionValue(collectionContainer, declaredElementType, context);
                });
                fieldElement.style.flexGrow = 1;
                fieldContainer.Add(fieldElement);

                fieldsContainer.Add(fieldContainer);
            }
        }

        protected VisualElement CreateSimpleField(Type type, object value, Action<object> onChanged)
        {
            if (type == typeof(int))
            {
                var field = new IntegerField();
                field.value = value != null ? Convert.ToInt32(value) : 0;
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            if (type == typeof(float))
            {
                var field = new FloatField();
                field.value = value != null ? Convert.ToSingle(value) : 0f;
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            if (type == typeof(double))
            {
                var field = new DoubleField();
                field.value = value != null ? Convert.ToDouble(value) : 0.0;
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            if (type == typeof(string))
            {
                var field = new TextField();
                field.value = value as string ?? "";
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            if (type == typeof(bool))
            {
                var field = new Toggle();
                field.value = value != null && Convert.ToBoolean(value);
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            if (type.IsEnum)
            {
                var enumValue = value as Enum ?? (Enum)Enum.GetValues(type).GetValue(0);
                var field = new EnumField(enumValue);
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            // Fallback: read-only text field
            var readOnly = new TextField();
            readOnly.value = value?.ToString() ?? "(null)";
            readOnly.isReadOnly = true;
            return readOnly;
        }

        #endregion

        #region Element Operations

        protected void AddElement(VisualElement collectionContainer, Type typeToCreate, FieldCreationContext context)
        {
            var userData = collectionContainer.userData as CollectionUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            var declaredElementType = userData.ElementType;
            var currentCount = elementsContainer.childCount;

            object newElement;
            try
            {
                newElement = Activator.CreateInstance(typeToCreate);
            }
            catch
            {
                newElement = GetDefaultValue(typeToCreate);
            }

            var elementContainer = CreateElementContainer(currentCount, newElement, declaredElementType, collectionContainer, context);
            elementsContainer.Add(elementContainer);

            UpdateCollectionValue(collectionContainer, declaredElementType, context);
            UpdateSizeLabel(collectionContainer, elementsContainer.childCount);
        }

        protected void RemoveElement(
            VisualElement elementToRemove,
            VisualElement collectionContainer,
            Type elementType,
            FieldCreationContext context)
        {
            var userData = collectionContainer.userData as CollectionUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            elementsContainer.Remove(elementToRemove);

            UpdateAllIndices(collectionContainer);
            UpdateCollectionValue(collectionContainer, elementType, context);
            UpdateSizeLabel(collectionContainer, elementsContainer.childCount);
        }

        protected void UpdateElementInContainer(
            VisualElement elementContainer,
            object newValue,
            VisualElement collectionContainer,
            Type declaredElementType,
            FieldCreationContext context)
        {
            elementContainer.userData = newValue;
            UpdateCollectionValue(collectionContainer, declaredElementType, context);
        }

        protected virtual void UpdateCollectionValue(
            VisualElement collectionContainer,
            Type elementType,
            FieldCreationContext context)
        {
            var userData = collectionContainer.userData as CollectionUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            var elements = elementsContainer.Query<VisualElement>(className: "collection-element").ToList();

            var list = new List<object>();
            foreach (var element in elements)
            {
                list.Add(element.userData);
            }

            var newCollection = CreateCollectionFromList(list, elementType);
            context.OnValueChanged?.Invoke(newCollection);
        }

        protected void MoveElement(
            VisualElement elementToMove,
            VisualElement collectionContainer,
            Type elementType,
            FieldCreationContext context,
            int direction)
        {
            var userData = collectionContainer.userData as CollectionUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            var elements = elementsContainer.Query<VisualElement>(className: "collection-element").ToList();
            var currentIndex = elements.IndexOf(elementToMove);

            if (currentIndex < 0) return;

            var newIndex = currentIndex + direction;
            if (newIndex < 0 || newIndex >= elements.Count) return;

            // Remove and reinsert at new position
            elementsContainer.Remove(elementToMove);
            elementsContainer.Insert(newIndex, elementToMove);

            // Update all indices
            UpdateAllIndices(collectionContainer);
            UpdateCollectionValue(collectionContainer, elementType, context);
        }

        protected void UpdateAllIndices(VisualElement collectionContainer)
        {
            var userData = collectionContainer.userData as CollectionUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            var elements = elementsContainer.Query<VisualElement>(className: "collection-element").ToList();

            for (int i = 0; i < elements.Count; i++)
            {
                var indexLabel = elements[i].Q<Label>(className: "collection-element-index");
                if (indexLabel != null)
                {
                    indexLabel.text = $"[{i}]";
                }
            }
        }

        protected void UpdateSizeLabel(VisualElement collectionContainer, int count)
        {
            var userData = collectionContainer.userData as CollectionUserData;
            if (userData?.Foldout != null)
            {
                var elementsContainer = userData.ElementsContainer;
                var elements = elementsContainer.Query<VisualElement>(className: "collection-element").ToList();
                var elementsList = elements.Select(e => e.userData).ToList();
                userData.Foldout.text = GetFoldoutText(elementsList, userData.ElementType);
            }
        }

        /// <summary>
        /// Get foldout display text with element type summary
        /// </summary>
        protected string GetFoldoutText(IList elements, Type elementType)
        {
            if (elements == null || elements.Count == 0)
                return "Size: 0";

            // For polymorphic types, show type names
            if (IsPolymorphicType(elementType))
            {
                var typeNames = new List<string>();
                foreach (var element in elements)
                {
                    if (element != null)
                    {
                        var typeName = FormatTypeName(element.GetType());
                        typeNames.Add(typeName);
                    }
                }

                if (typeNames.Count > 0)
                {
                    // Show up to 5 types, then "..."
                    var displayNames = typeNames.Count <= 5
                        ? string.Join(", ", typeNames)
                        : string.Join(", ", typeNames.Take(5)) + ", ...";
                    return $"Size: {elements.Count} ({displayNames})";
                }
            }

            return $"Size: {elements.Count}";
        }

        #endregion

        #region Polymorphism Support

        /// <summary>
        /// Get all concrete derived types using Unity's TypeCache
        /// </summary>
        protected List<Type> GetDerivedTypes(Type baseType)
        {
            if (IsPrimitiveOrSimpleType(baseType))
            {
                return new List<Type> { baseType };
            }

            if (!baseType.IsAbstract && !baseType.IsInterface)
            {
                // Concrete type - check if there are derived types
                var derived = TypeCache.GetTypesDerivedFrom(baseType)
                    .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition)
                    .ToList();

                if (derived.Count == 0)
                {
                    return new List<Type> { baseType };
                }

                // Include base type and derived types
                var result = new List<Type> { baseType };
                result.AddRange(derived);
                return result;
            }

            // Abstract or interface - get all concrete implementations
            var types = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition)
                .ToList();

            return types.Count > 0 ? types : new List<Type> { baseType };
        }

        protected bool IsPolymorphicType(Type type)
        {
            if (IsPrimitiveOrSimpleType(type)) return false;
            return type.IsAbstract || type.IsInterface || GetDerivedTypes(type).Count > 1;
        }

        protected bool IsPrimitiveOrSimpleType(Type type)
        {
            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   type.IsEnum;
        }

        protected string FormatTypeName(Type type)
        {
            if (type == null) return "(null)";

            var name = type.Name;

            // Remove common suffixes for cleaner display
            var suffixes = new[] { "Objective", "Data", "Info", "Item" };
            foreach (var suffix in suffixes)
            {
                if (name.EndsWith(suffix) && name.Length > suffix.Length)
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                    break;
                }
            }

            // Add spaces before capitals (CamelCase to "Camel Case")
            return ObjectNames.NicifyVariableName(name);
        }

        #endregion

        protected class CollectionUserData
        {
            public VisualElement ElementsContainer { get; set; }
            public Type ElementType { get; set; }
            public Foldout Foldout { get; set; }
        }
    }
}
