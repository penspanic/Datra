#nullable disable
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for basic array types (int[], string[], float[])
    /// </summary>
    public class ArrayFieldHandler : BaseArrayFieldHandler
    {
        public override int Priority => 20;

        protected override string ElementFieldClassName => "array-element-field";

        public override bool CanHandle(Type type, MemberInfo member = null)
        {
            if (!type.IsArray)
                return false;

            var elementType = type.GetElementType();
            return elementType == typeof(int) ||
                   elementType == typeof(string) ||
                   elementType == typeof(float);
        }

        protected override Type GetElementType(Type arrayType)
        {
            return arrayType.GetElementType();
        }

        protected override object GetDefaultValue(Type elementType)
        {
            if (elementType == typeof(string)) return "";
            return Activator.CreateInstance(elementType);
        }

        protected override string GetElementDisplayText(object element, Type elementType)
        {
            return element?.ToString() ?? "";
        }

        protected override VisualElement CreateElementField(Type elementType, object value, Action onChanged)
        {
            VisualElement field;

            if (elementType == typeof(int))
            {
                var intField = new IntegerField();
                intField.value = value != null ? Convert.ToInt32(value) : 0;
                intField.RegisterValueChangedCallback(_ => onChanged());
                field = intField;
            }
            else if (elementType == typeof(string))
            {
                var textField = new TextField();
                textField.value = value as string ?? "";
                textField.RegisterValueChangedCallback(_ => onChanged());
                field = textField;
            }
            else if (elementType == typeof(float))
            {
                var floatField = new FloatField();
                floatField.value = value != null ? Convert.ToSingle(value) : 0f;
                floatField.RegisterValueChangedCallback(_ => onChanged());
                field = floatField;
            }
            else
            {
                // Fallback
                var readOnly = new TextField();
                readOnly.value = value?.ToString() ?? "";
                readOnly.isReadOnly = true;
                field = readOnly;
            }

            field.AddToClassList(ElementFieldClassName);
            return field;
        }

        protected override object GetElementValue(VisualElement elementField)
        {
            if (elementField is IntegerField intField)
                return intField.value;
            if (elementField is TextField textField)
                return textField.value;
            if (elementField is FloatField floatField)
                return floatField.value;

            return null;
        }

        protected override void UpdateArrayValue(VisualElement arrayContainer, Type elementType, FieldCreationContext context)
        {
            var userData = arrayContainer.userData as ArrayUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            Array newArray;

            if (elementType == typeof(int))
            {
                var values = new List<int>();
                var fields = elementsContainer.Query<IntegerField>().ToList();
                foreach (var field in fields)
                {
                    values.Add(field.value);
                }
                newArray = values.ToArray();
            }
            else if (elementType == typeof(string))
            {
                var values = new List<string>();
                var fields = elementsContainer.Query<TextField>().ToList();
                foreach (var field in fields)
                {
                    values.Add(field.value);
                }
                newArray = values.ToArray();
            }
            else if (elementType == typeof(float))
            {
                var values = new List<float>();
                var fields = elementsContainer.Query<FloatField>().ToList();
                foreach (var field in fields)
                {
                    values.Add(field.value);
                }
                newArray = values.ToArray();
            }
            else
            {
                return;
            }

            context.OnValueChanged?.Invoke(newArray);
        }
    }
}
