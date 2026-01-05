using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for string fields.
    /// Uses Unity's isDelayed mode - OnValueChanged is only called when editing is complete
    /// (Enter key or FocusOut), preventing UI rebuild during typing.
    /// </summary>
    public class StringFieldHandler : IFieldTypeHandler
    {
        public int Priority => 0;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return type == typeof(string);
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var textField = new TextField();
            var originalValue = context.Value as string ?? "";
            textField.value = originalValue;

            // Use Unity's built-in delayed mode:
            // - Value change event only fires on Enter or FocusOut
            // - Prevents event spam during typing
            // - Works correctly with IME input (Korean, Japanese, etc.)
            textField.isDelayed = true;

            textField.RegisterValueChangedCallback(evt =>
            {
                // Only fire OnValueChanged if value actually changed from original
                if (evt.newValue != originalValue)
                {
                    // Update target object's property
                    if (context.Property != null && context.Target != null)
                    {
                        try
                        {
                            context.Property.SetValue(context.Target, evt.newValue);
                        }
                        catch
                        {
                            // Property might be read-only or have other issues
                        }
                    }

                    // Fire the commit callback
                    context.OnValueChanged?.Invoke(evt.newValue);

                    // Update original value for subsequent edits
                    originalValue = evt.newValue;
                }
            });

            return textField;
        }
    }

    /// <summary>
    /// Handler for int fields
    /// </summary>
    public class IntFieldHandler : IFieldTypeHandler
    {
        public int Priority => 0;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return type == typeof(int);
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var intField = new IntegerField();
            intField.value = context.Value != null ? Convert.ToInt32(context.Value) : 0;
            intField.RegisterValueChangedCallback(evt =>
            {
                context.OnValueChanged?.Invoke(evt.newValue);
            });
            return intField;
        }
    }

    /// <summary>
    /// Handler for float fields
    /// </summary>
    public class FloatFieldHandler : IFieldTypeHandler
    {
        public int Priority => 0;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return type == typeof(float);
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var floatField = new FloatField();
            floatField.value = context.Value != null ? Convert.ToSingle(context.Value) : 0f;
            floatField.RegisterValueChangedCallback(evt =>
            {
                context.OnValueChanged?.Invoke(evt.newValue);
            });
            return floatField;
        }
    }

    /// <summary>
    /// Handler for bool fields
    /// </summary>
    public class BoolFieldHandler : IFieldTypeHandler
    {
        public int Priority => 0;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return type == typeof(bool);
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var toggle = new Toggle();
            toggle.value = context.Value != null && Convert.ToBoolean(context.Value);
            toggle.RegisterValueChangedCallback(evt =>
            {
                context.OnValueChanged?.Invoke(evt.newValue);
            });
            return toggle;
        }
    }

    /// <summary>
    /// Handler for enum fields
    /// </summary>
    public class EnumFieldHandler : IFieldTypeHandler
    {
        public int Priority => 0;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return type.IsEnum;
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var defaultValue = context.Value as Enum ?? (Enum)Enum.GetValues(context.FieldType).GetValue(0);
            var enumField = new EnumField(defaultValue);
            enumField.RegisterValueChangedCallback(evt =>
            {
                context.OnValueChanged?.Invoke(evt.newValue);
            });
            return enumField;
        }
    }

    /// <summary>
    /// Handler for Vector2 fields
    /// </summary>
    public class Vector2FieldHandler : IFieldTypeHandler
    {
        public int Priority => 0;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return type == typeof(Vector2);
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var vector2Field = new Vector2Field();
            vector2Field.value = context.Value != null ? (Vector2)context.Value : Vector2.zero;
            vector2Field.RegisterValueChangedCallback(evt =>
            {
                context.OnValueChanged?.Invoke(evt.newValue);
            });
            return vector2Field;
        }
    }

    /// <summary>
    /// Handler for Vector3 fields
    /// </summary>
    public class Vector3FieldHandler : IFieldTypeHandler
    {
        public int Priority => 0;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return type == typeof(Vector3);
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var vector3Field = new Vector3Field();
            vector3Field.value = context.Value != null ? (Vector3)context.Value : Vector3.zero;
            vector3Field.RegisterValueChangedCallback(evt =>
            {
                context.OnValueChanged?.Invoke(evt.newValue);
            });
            return vector3Field;
        }
    }

    /// <summary>
    /// Handler for Color fields
    /// </summary>
    public class ColorFieldHandler : IFieldTypeHandler
    {
        public int Priority => 0;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return type == typeof(Color);
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var colorField = new ColorField();
            colorField.value = context.Value != null ? (Color)context.Value : Color.white;
            colorField.RegisterValueChangedCallback(evt =>
            {
                context.OnValueChanged?.Invoke(evt.newValue);
            });
            return colorField;
        }
    }
}
