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
            Debug.LogWarning($"[FieldTypeRegistry] Unsupported type: {context.FieldType.FullName}");

            var container = new VisualElement();
            container.AddToClassList("unsupported-field-container");

            var readOnlyField = new TextField();
            readOnlyField.value = context.Value?.ToString() ?? "null";
            readOnlyField.isReadOnly = true;
            readOnlyField.AddToClassList("unsupported-field");
            container.Add(readOnlyField);

            var typeInfo = new Label($"Type: {context.FieldType.Name}");
            typeInfo.AddToClassList("type-info");
            container.Add(typeInfo);

            return container;
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
