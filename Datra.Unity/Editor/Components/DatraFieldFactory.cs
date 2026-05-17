#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.UIElements;
using Datra.Editor.Models;
using Datra.Unity.Editor.Interfaces;

namespace Datra.Unity.Editor.Components
{
    /// <summary>
    /// Factory for creating property fields with consistent styling and behavior
    /// </summary>
    public static class DatraFieldFactory
    {
        private static Dictionary<Type, Func<object, PropertyInfo, FieldLayoutMode, DatraPropertyField>> customFieldCreators = new();

        /// <summary>
        /// Register a custom field creator for a specific type
        /// </summary>
        public static void RegisterCustomField<T>(Func<object, PropertyInfo, FieldLayoutMode, DatraPropertyField> creator)
        {
            customFieldCreators[typeof(T)] = creator;
        }

        /// <summary>
        /// Create a property field for the given property
        /// </summary>
        public static DatraPropertyField CreateField(
            object target,
            PropertyInfo property,
            FieldLayoutMode layoutMode = FieldLayoutMode.Form,
            ILocaleProvider localeProvider = null)
        {
            // Check for custom field creators
            if (customFieldCreators.TryGetValue(property.PropertyType, out var creator))
            {
                return creator(target, property, layoutMode);
            }

            // Default field creation
            return new DatraPropertyField(target, property, layoutMode, localeProvider);
        }

        /// <summary>
        /// Create fields for all writable properties of an object
        /// </summary>
        public static List<DatraPropertyField> CreateFieldsForObject(
            object target,
            FieldLayoutMode layoutMode = FieldLayoutMode.Form,
            bool skipId = true,
            ILocaleProvider localeProvider = null)
        {
            var fields = new List<DatraPropertyField>();
            // Shared rule via EditableMemberEnumerator
            // (public, get+set, no indexer, no [DatraIgnore], not field-shadowed).
            var properties = Datra.Editor.Schema.EditableMemberEnumerator.ForType(target.GetType());

            foreach (var property in properties)
            {
                // Use DatraPropertyField.CanHandle to check if property can be edited
                if (DatraPropertyField.CanHandle(property, localeProvider) && (!skipId || property.Name != "Id"))
                {
                    var field = CreateField(target, property, layoutMode, localeProvider);
                    fields.Add(field);
                }
            }

            return fields;
        }
        
        /// <summary>
        /// Create a grouped container for related fields
        /// </summary>
        public static VisualElement CreateFieldGroup(string groupName, List<DatraPropertyField> fields)
        {
            var groupContainer = new VisualElement();
            groupContainer.AddToClassList("property-field-group");
            
            if (!string.IsNullOrEmpty(groupName))
            {
                var groupLabel = new Label(groupName);
                groupLabel.AddToClassList("property-field-group-label");
                groupContainer.Add(groupLabel);
            }
            
            var fieldsContainer = new VisualElement();
            fieldsContainer.AddToClassList("property-field-group-fields");
            
            foreach (var field in fields)
            {
                fieldsContainer.Add(field);
            }
            
            groupContainer.Add(fieldsContainer);
            
            return groupContainer;
        }
    }
}