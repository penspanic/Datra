using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Registry for field type handlers. Manages handler registration and field creation dispatch.
    /// </summary>
    public static class FieldTypeRegistry
    {
        private static readonly List<IFieldTypeHandler> _handlers = new();
        private static bool _initialized = false;

        /// <summary>
        /// Initialize the registry with default handlers
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            // Register handlers in priority order (higher priority first)
            // Specialized handlers should be registered with higher priority
            RegisterHandler(new LocaleRefFieldHandler());      // Priority 100
            RegisterHandler(new AssetStringFieldHandler());    // Priority 50
            RegisterHandler(new DataRefFieldHandler());        // Priority 40
            RegisterHandler(new DataRefArrayFieldHandler());   // Priority 35
            RegisterHandler(new NestedTypeFieldHandler());     // Priority 30
            RegisterHandler(new EnumArrayFieldHandler());      // Priority 25
            RegisterHandler(new DictionaryFieldHandler());     // Priority 23
            RegisterHandler(new ListFieldHandler());           // Priority 22
            RegisterHandler(new ArrayFieldHandler());          // Priority 20

            // Basic type handlers (Priority 0)
            RegisterHandler(new StringFieldHandler());
            RegisterHandler(new IntFieldHandler());
            RegisterHandler(new FloatFieldHandler());
            RegisterHandler(new BoolFieldHandler());
            RegisterHandler(new EnumFieldHandler());
            RegisterHandler(new Vector2FieldHandler());
            RegisterHandler(new Vector3FieldHandler());
            RegisterHandler(new ColorFieldHandler());

            _initialized = true;
        }

        /// <summary>
        /// Register a custom field type handler
        /// </summary>
        public static void RegisterHandler(IFieldTypeHandler handler)
        {
            _handlers.Add(handler);
            // Keep sorted by priority (descending)
            _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>
        /// Create a field for the given context
        /// </summary>
        public static VisualElement CreateField(FieldCreationContext context)
        {
            Initialize();

            var member = context.Property as MemberInfo ?? context.Member;

            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(context.FieldType, member))
                {
                    return handler.CreateField(context);
                }
            }

            // Fallback: create a read-only field for unsupported types
            return CreateUnsupportedField(context);
        }

        /// <summary>
        /// Check if any handler can handle the given type
        /// </summary>
        public static bool CanHandle(Type type, MemberInfo member = null)
        {
            Initialize();

            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(type, member))
                {
                    return true;
                }
            }

            return false;
        }

        private static VisualElement CreateUnsupportedField(FieldCreationContext context)
        {
            var container = new VisualElement();
            container.AddToClassList("unsupported-field-container");

            var displayValue = GetDisplayValueForUnsupportedType(context.FieldType, context.Value);

            var readOnlyField = new TextField();
            readOnlyField.value = displayValue;
            readOnlyField.isReadOnly = true;
            readOnlyField.AddToClassList("unsupported-field");
            container.Add(readOnlyField);

            return container;
        }

        private static string GetDisplayValueForUnsupportedType(Type type, object value)
        {
            if (value == null) return "(null)";

            // Handle arrays
            if (type.IsArray)
            {
                var array = value as Array;
                return $"[{array?.Length ?? 0} items]";
            }

            // Handle generic collection types
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();

                // List<T>
                if (genericDef == typeof(List<>))
                {
                    var countProp = type.GetProperty("Count");
                    var count = countProp?.GetValue(value) ?? 0;
                    return $"[{count} items]";
                }

                // Dictionary<K,V>
                if (genericDef == typeof(Dictionary<,>))
                {
                    var countProp = type.GetProperty("Count");
                    var count = countProp?.GetValue(value) ?? 0;
                    return $"{{{count} entries}}";
                }
            }

            // Handle ICollection interface (catches other collection types)
            if (value is System.Collections.ICollection collection)
            {
                return $"[{collection.Count} items]";
            }

            // Default: use ToString but truncate if too long
            var str = value.ToString();
            if (str.Length > 50)
            {
                str = str.Substring(0, 47) + "...";
            }
            return str;
        }

        /// <summary>
        /// Reset the registry (useful for testing)
        /// </summary>
        internal static void Reset()
        {
            _handlers.Clear();
            _initialized = false;
        }
    }
}
