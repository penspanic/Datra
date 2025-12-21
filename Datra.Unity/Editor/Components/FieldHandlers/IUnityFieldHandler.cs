using UnityEngine.UIElements;
using EditorInterfaces = Datra.Editor.Interfaces;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Unity-specific field type handler interface.
    /// Extends the base IFieldTypeHandler with Unity VisualElement rendering.
    /// </summary>
    public interface IUnityFieldHandler : EditorInterfaces.IFieldTypeHandler
    {
        /// <summary>
        /// Create a VisualElement for editing the field
        /// </summary>
        /// <param name="context">Creation context with all necessary information</param>
        /// <returns>A VisualElement for editing the value</returns>
        VisualElement CreateField(FieldCreationContext context);
    }
}
