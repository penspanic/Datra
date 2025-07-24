using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;

namespace Datra.Unity.Editor.Components
{
    /// <summary>
    /// Factory for creating property fields with consistent styling and behavior
    /// </summary>
    public static class DatraFieldFactory
    {
        private static Dictionary<Type, Func<object, PropertyInfo, DatraPropertyTracker, DatraPropertyField>> customFieldCreators = 
            new Dictionary<Type, Func<object, PropertyInfo, DatraPropertyTracker, DatraPropertyField>>();
        
        /// <summary>
        /// Register a custom field creator for a specific type
        /// </summary>
        public static void RegisterCustomField<T>(Func<object, PropertyInfo, DatraPropertyTracker, DatraPropertyField> creator)
        {
            customFieldCreators[typeof(T)] = creator;
        }
        
        /// <summary>
        /// Create a property field for the given property
        /// </summary>
        public static DatraPropertyField CreateField(object target, PropertyInfo property, DatraPropertyTracker tracker)
        {
            // Check for custom field creators
            if (customFieldCreators.TryGetValue(property.PropertyType, out var creator))
            {
                return creator(target, property, tracker);
            }
            
            // Default field creation
            return new DatraPropertyField(target, property, tracker);
        }
        
        /// <summary>
        /// Create fields for all writable properties of an object
        /// </summary>
        public static List<DatraPropertyField> CreateFieldsForObject(object target, DatraPropertyTracker tracker, bool skipId = true)
        {
            var fields = new List<DatraPropertyField>();
            var properties = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                if (property.CanWrite && (!skipId || property.Name != "Id"))
                {
                    var field = CreateField(target, property, tracker);
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