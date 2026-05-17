#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Datra.DataTypes;
using Datra.Editor.Models;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for nested struct/class types (user-defined types)
    /// </summary>
    public class NestedTypeFieldHandler : IUnityFieldHandler
    {
        public int Priority => 30;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return IsNestedType(type);
        }

        // NOTE: We do NOT delegate to TypeClassifier.Classify(...) == FieldKind.Nested here because
        // the classifier treats UnityEngine types (Vector2/Vector3/Color/...) as Nested too. Unity
        // has dedicated handlers for those, but they run at Priority 0 while NestedTypeFieldHandler
        // is at Priority 30 — so a classifier-driven CanHandle would steal Vector3 etc. from
        // Vector3FieldHandler. Until Unity's specialized handlers are bumped above Priority 30 (or
        // the classifier gains an "exclude UnityEngine" knob) we keep the original predicate.
        // TODO: classifier doesn't currently exclude System.* / UnityEngine.* namespaces.
        public static bool IsNestedType(Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return false;
            if (type.IsArray || type.IsEnum)
                return false;
            if (type.Namespace != null && type.Namespace.StartsWith("System"))
                return false;
            if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine"))
                return false;
            if (Datra.Editor.Schema.TypeClassifier.IsDataRefType(type))
                return false;
            if (type == typeof(LocaleRef))
                return false;

            return type.IsValueType || type.IsClass;
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var nestedType = context.FieldType;
            var value = context.Value;

            var container = new VisualElement();
            container.AddToClassList("nested-type-field-container");

            // Table mode: compact display with edit button
            if (context.LayoutMode == FieldLayoutMode.Table)
            {
                return CreateCompactDisplay(container, nestedType, value, context);
            }

            // Form/Inline mode: show nested fields
            return CreateFullLayout(container, nestedType, value, context);
        }

        private VisualElement CreateCompactDisplay(VisualElement container, Type nestedType, object value, FieldCreationContext context)
        {
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            // Compact display (declare first for closure)
            var displayField = new TextField();
            displayField.isReadOnly = true;
            displayField.style.flexGrow = 1;
            displayField.AddToClassList("nested-type-compact-display");
            UpdateDisplayValue(displayField, nestedType, value);

            // Edit button - opens property editor popup
            var editButton = new Button(() =>
            {
                if (context.Property != null && context.Target != null)
                {
                    DatraPropertyEditorPopup.ShowEditor(context.Property, context.Target, () =>
                    {
                        var newValue = context.Property.GetValue(context.Target);
                        UpdateDisplayValue(displayField, nestedType, newValue);
                        context.OnValueChanged?.Invoke(newValue);
                    });
                }
            });
            editButton.text = "✏";
            editButton.tooltip = $"Edit {nestedType.Name}";
            editButton.AddToClassList("nested-type-edit-button");
            editButton.style.marginRight = 4;

            container.Add(editButton);
            container.Add(displayField);
            return container;
        }

        private void UpdateDisplayValue(TextField displayField, Type nestedType, object value)
        {
            if (value != null)
            {
                var displayText = GetDisplayText(nestedType, value);
                if (displayText.Length > 60)
                {
                    displayText = displayText.Substring(0, 57) + "...";
                }
                displayField.value = displayText;
            }
            else
            {
                displayField.value = "(null)";
            }
        }

        private VisualElement CreateFullLayout(VisualElement container, Type nestedType, object value, FieldCreationContext context)
        {
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginLeft = 8;
            container.style.paddingLeft = 8;
            container.style.borderLeftWidth = 2;
            container.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 1f);

            if (value == null)
            {
                if (nestedType.IsValueType)
                {
                    value = Activator.CreateInstance(nestedType);
                    context.OnValueChanged?.Invoke(value);
                }
                else
                {
                    var createButton = new Button(() =>
                    {
                        var newValue = Activator.CreateInstance(nestedType);
                        context.OnValueChanged?.Invoke(newValue);
                    });
                    createButton.text = $"Create {nestedType.Name}";
                    container.Add(createButton);
                    return container;
                }
            }

            // Get all public fields and properties
            var members = GetMembers(nestedType);
            var currentValue = value;

            foreach (var member in members)
            {
                var memberContainer = new VisualElement();
                memberContainer.AddToClassList("nested-member-container");
                memberContainer.style.flexDirection = FlexDirection.Row;
                memberContainer.style.alignItems = Align.Center;
                memberContainer.style.marginBottom = 2;

                var memberLabel = new Label(ObjectNames.NicifyVariableName(member.Name));
                memberLabel.style.minWidth = 100;
                memberLabel.style.marginRight = 8;
                memberContainer.Add(memberLabel);

                var memberType = GetMemberType(member);
                var memberValue = GetMemberValue(member, currentValue);

                var memberContext = new FieldCreationContext(
                    member,
                    memberType,
                    currentValue,
                    memberValue,
                    FieldLayoutMode.Inline,
                    newValue =>
                    {
                        SetMemberValue(member, currentValue, newValue);
                        // For structs, we need to reassign the entire struct
                        if (nestedType.IsValueType)
                        {
                            context.OnValueChanged?.Invoke(currentValue);
                        }
                        else
                        {
                            context.OnValueChanged?.Invoke(currentValue);
                        }
                    });

                var memberField = FieldTypeRegistry.CreateField(memberContext);
                if (memberField != null)
                {
                    memberField.style.flexGrow = 1;
                    memberContainer.Add(memberField);
                }

                container.Add(memberContainer);
            }

            return container;
        }

        private string GetDisplayText(Type nestedType, object value)
        {
            var toStringResult = value.ToString();
            if (toStringResult != nestedType.FullName && toStringResult != nestedType.Name)
            {
                return toStringResult;
            }

            // Fallback: show field values
            var members = GetMembers(nestedType);
            var values = new List<string>();

            foreach (var member in members)
            {
                var memberValue = GetMemberValue(member, value);
                if (memberValue != null)
                {
                    var strValue = memberValue.ToString();
                    if (!string.IsNullOrEmpty(strValue))
                    {
                        values.Add(strValue);
                    }
                }
            }

            return values.Count > 0 ? string.Join(", ", values) : "(empty)";
        }

        private List<MemberInfo> GetMembers(Type type)
        {
            var members = new List<MemberInfo>();

            // Get public fields. EditableMemberEnumerator only enumerates properties; we keep the
            // field walk so user-authored nested structs/classes with public fields still render.
            // TODO: classifier doesn't currently surface public fields — drop this branch when
            //       confirmed unused by real models.
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.Name.Contains("<") || field.Name.Contains(">"))
                    continue;
                members.Add(field);
            }

            // Properties — shared rules via EditableMemberEnumerator
            // (public, get+set, no indexer, no [DatraIgnore], not field-shadowed).
            foreach (var prop in Datra.Editor.Schema.EditableMemberEnumerator.ForType(type))
            {
                members.Add(prop);
            }

            return members;
        }

        private Type GetMemberType(MemberInfo member)
        {
            return member switch
            {
                FieldInfo field => field.FieldType,
                PropertyInfo prop => prop.PropertyType,
                _ => typeof(object)
            };
        }

        private object GetMemberValue(MemberInfo member, object target)
        {
            return member switch
            {
                FieldInfo field => field.GetValue(target),
                PropertyInfo prop => prop.GetValue(target),
                _ => null
            };
        }

        private void SetMemberValue(MemberInfo member, object target, object value)
        {
            switch (member)
            {
                case FieldInfo field:
                    field.SetValue(target, value);
                    break;
                case PropertyInfo prop:
                    prop.SetValue(target, value);
                    break;
            }
        }
    }
}
