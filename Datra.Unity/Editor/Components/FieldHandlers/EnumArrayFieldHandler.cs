using System;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for enum array types
    /// </summary>
    public class EnumArrayFieldHandler : BaseArrayFieldHandler
    {
        public override int Priority => 25;

        protected override string ElementFieldClassName => "enum-array-element-field";

        public override bool CanHandle(Type type, MemberInfo member = null)
        {
            return type.IsArray && type.GetElementType()?.IsEnum == true;
        }

        protected override Type GetElementType(Type arrayType)
        {
            return arrayType.GetElementType();
        }

        protected override object GetDefaultValue(Type elementType)
        {
            return Enum.GetValues(elementType).GetValue(0);
        }

        protected override string GetElementDisplayText(object element, Type elementType)
        {
            return element?.ToString() ?? "";
        }

        protected override VisualElement CreateElementField(Type elementType, object value, Action onChanged)
        {
            var defaultValue = value as Enum ?? (Enum)Enum.GetValues(elementType).GetValue(0);
            var enumField = new EnumField(defaultValue);
            enumField.RegisterValueChangedCallback(_ => onChanged());
            enumField.AddToClassList(ElementFieldClassName);
            return enumField;
        }

        protected override object GetElementValue(VisualElement elementField)
        {
            if (elementField is EnumField enumField)
                return enumField.value;
            return null;
        }
    }
}
