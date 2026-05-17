#nullable enable
using System;
using System.Reflection;
using Datra.Editor.Models;
using Datra.Editor.Schema;
using Datra.WebEditor.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Datra.WebEditor.Handlers;

/// <summary>
/// Catch-all for complex types — renders each writable property as its own field via the
/// registry. Priority is below all specialised handlers so DataRef / List / Enum win first.
/// </summary>
public sealed class NestedTypeFieldHandler : IBlazorFieldHandler
{
    private readonly BlazorFieldTypeRegistry _registry;

    public NestedTypeFieldHandler(BlazorFieldTypeRegistry registry) => _registry = registry;

    public int Priority => 30;

    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.Nested;

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var obj = context.Value;

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "datra-nested");

        if (obj == null)
        {
            builder.OpenElement(2, "span");
            builder.AddAttribute(3, "class", "datra-nested__null");
            builder.AddContent(4, "(null)");
            builder.CloseElement();
            builder.CloseElement();
            return;
        }

        var properties = EditableMemberEnumerator.ForType(context.FieldType);

        var seq = 2;
        foreach (var property in properties)
        {
            var propValue = property.GetValue(obj);
            var handler = _registry.FindBlazorHandler(property.PropertyType, property);

            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "datra-nested__row");

            builder.OpenElement(seq++, "span");
            builder.AddAttribute(seq++, "class", "datra-nested__label");
            builder.AddContent(seq++, property.Name);
            builder.CloseElement();

            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "datra-nested__field");
            if (handler != null)
            {
                var propContext = new FieldCreationContext(
                    property,
                    obj,
                    propValue,
                    context.LayoutMode,
                    newValue =>
                    {
                        property.SetValue(obj, newValue);
                        context.OnValueChanged?.Invoke(obj);
                    },
                    context.LocaleService,
                    context.IsReadOnly || !property.CanWrite);
                builder.AddContent(seq++, handler.CreateField(propContext));
            }
            else
            {
                builder.OpenElement(seq++, "span");
                builder.AddAttribute(seq++, "class", "datra-nested__missing");
                builder.AddContent(seq++, $"no handler for {property.PropertyType.Name}");
                builder.CloseElement();
            }
            builder.CloseElement(); // field

            builder.CloseElement(); // row
        }

        builder.CloseElement(); // nested
    };
}
