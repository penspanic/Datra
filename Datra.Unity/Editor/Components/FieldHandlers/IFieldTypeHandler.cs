using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Interface for field type handlers that create VisualElements for specific types
    /// </summary>
    public interface IFieldTypeHandler
    {
        /// <summary>
        /// Priority for handler selection (higher = checked first)
        /// Default handlers use 0, specialized handlers use higher values
        /// </summary>
        int Priority => 0;

        /// <summary>
        /// Check if this handler can handle the given type
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <param name="member">Optional member info for attribute checking</param>
        bool CanHandle(Type type, MemberInfo member = null);

        /// <summary>
        /// Create a VisualElement for editing the field
        /// </summary>
        /// <param name="context">Creation context with all necessary information</param>
        /// <returns>A VisualElement for editing the value</returns>
        VisualElement CreateField(FieldCreationContext context);
    }
}
