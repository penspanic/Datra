using System;
using System.Reflection;
using Datra.Unity.Editor.Interfaces;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Context containing all information needed to create a field
    /// </summary>
    public class FieldCreationContext
    {
        /// <summary>
        /// The type of the property/field being edited
        /// </summary>
        public Type FieldType { get; }

        /// <summary>
        /// Current value of the field
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// PropertyInfo if editing a property (null for nested member fields)
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        /// MemberInfo for nested type members (FieldInfo or PropertyInfo)
        /// </summary>
        public MemberInfo Member { get; }

        /// <summary>
        /// The target object containing the property
        /// </summary>
        public object Target { get; }

        /// <summary>
        /// Parent value for nested type members
        /// </summary>
        public object ParentValue { get; }

        /// <summary>
        /// Layout mode for the field
        /// </summary>
        public DatraFieldLayoutMode LayoutMode { get; }

        /// <summary>
        /// Callback when value changes
        /// </summary>
        public Action<object> OnValueChanged { get; }

        /// <summary>
        /// Optional locale provider for localization support
        /// </summary>
        public ILocaleProvider LocaleProvider { get; }

        /// <summary>
        /// Whether this field is being edited in a popup editor (skip foldout wrapper)
        /// </summary>
        public bool IsPopupEditor { get; }

        /// <summary>
        /// Create context for a property field
        /// </summary>
        public FieldCreationContext(
            PropertyInfo property,
            object target,
            object value,
            DatraFieldLayoutMode layoutMode,
            Action<object> onValueChanged,
            ILocaleProvider localeProvider = null,
            bool isPopupEditor = false)
        {
            Property = property;
            FieldType = property.PropertyType;
            Target = target;
            Value = value;
            LayoutMode = layoutMode;
            OnValueChanged = onValueChanged;
            LocaleProvider = localeProvider;
            IsPopupEditor = isPopupEditor;
        }

        /// <summary>
        /// Create context for a nested member field
        /// </summary>
        public FieldCreationContext(
            MemberInfo member,
            Type fieldType,
            object parentValue,
            object value,
            DatraFieldLayoutMode layoutMode,
            Action<object> onValueChanged)
        {
            Member = member;
            FieldType = fieldType;
            ParentValue = parentValue;
            Value = value;
            LayoutMode = layoutMode;
            OnValueChanged = onValueChanged;
        }

        /// <summary>
        /// Create context for array element field
        /// </summary>
        public FieldCreationContext(
            Type elementType,
            object value,
            DatraFieldLayoutMode layoutMode,
            Action<object> onValueChanged)
        {
            FieldType = elementType;
            Value = value;
            LayoutMode = layoutMode;
            OnValueChanged = onValueChanged;
        }

        /// <summary>
        /// Check if this is a nested member context
        /// </summary>
        public bool IsNestedMember => Member != null;

        /// <summary>
        /// Check if this is an array element context
        /// </summary>
        public bool IsArrayElement => Property == null && Member == null;

        /// <summary>
        /// The index of this element in a collection (for nested locale evaluation)
        /// </summary>
        public int? CollectionElementIndex { get; set; }

        /// <summary>
        /// The root data object that owns this element (for nested locale evaluation)
        /// </summary>
        public object RootDataObject { get; set; }

        /// <summary>
        /// The element object containing this property (for nested locale evaluation)
        /// </summary>
        public object CollectionElement { get; set; }
    }
}
