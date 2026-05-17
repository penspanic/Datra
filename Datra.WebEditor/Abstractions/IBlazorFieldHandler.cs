#nullable enable
using Datra.Editor.Interfaces;
using Datra.Editor.Models;
using Microsoft.AspNetCore.Components;

namespace Datra.WebEditor.Abstractions;

/// <summary>
/// Blazor-specific field type handler. Extends <see cref="IFieldTypeHandler"/> with a
/// <see cref="RenderFragment"/> producer.
/// </summary>
/// <remarks>
/// Mirrors the Unity-side <c>IUnityFieldHandler</c> contract — same <see cref="FieldCreationContext"/>,
/// different render target. A consumer that needs to plug a custom widget for a domain-specific
/// type (e.g. a color picker for a <c>Color</c> struct) implements this interface and registers
/// the instance with <see cref="Handlers.BlazorFieldTypeRegistry"/>.
/// </remarks>
public interface IBlazorFieldHandler : IFieldTypeHandler
{
    /// <summary>
    /// Build a <see cref="RenderFragment"/> that renders an editor for the field described by
    /// <paramref name="context"/>. The fragment should invoke
    /// <see cref="FieldCreationContext.OnValueChanged"/> when the user commits a new value.
    /// </summary>
    RenderFragment CreateField(FieldCreationContext context);
}
