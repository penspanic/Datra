#nullable enable
using System;
using System.Globalization;
using System.Reflection;
using Datra.DataTypes;
using Datra.Editor.Models;
using Datra.Editor.Schema;
using Datra.WebEditor.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Datra.WebEditor.Handlers;

/// <summary>
/// Inline editor for <see cref="StringDataRef{T}"/> / <see cref="IntDataRef{T}"/>. For richer
/// pick-from-list UX, consumers can replace this handler with one that opens a modal selector.
/// </summary>
public sealed class DataRefFieldHandler : IBlazorFieldHandler
{
    public int Priority => 40;

    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.DataRef;

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var info = DataRefTypeInfo.TryCreate(context.FieldType);
        if (info is null)
        {
            // Defensive — registry shouldn't route non-DataRef types here.
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "datra-dataref__missing");
            builder.AddContent(2, $"unsupported DataRef: {context.FieldType.Name}");
            builder.CloseElement();
            return;
        }

        var currentKey = context.Value is null ? null : info.GetKey(context.Value);

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "datra-dataref");

        builder.OpenElement(2, "input");
        builder.AddAttribute(3, "type", "text");
        builder.AddAttribute(4, "class", "datra-input datra-dataref__input");
        builder.AddAttribute(5, "value", currentKey?.ToString() ?? string.Empty);
        builder.AddAttribute(6, "placeholder", $"{info.ReferencedType.Name} id");
        if (context.IsReadOnly) builder.AddAttribute(7, "disabled", true);
        builder.AddAttribute(8, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            var raw = e.Value?.ToString();
            if (TryBuild(info, raw, out var built))
                context.OnValueChanged?.Invoke(built);
        }));
        builder.CloseElement();

        if (!context.IsReadOnly)
        {
            builder.OpenElement(9, "button");
            builder.AddAttribute(10, "type", "button");
            builder.AddAttribute(11, "class", "datra-btn datra-btn--ghost datra-dataref__clear");
            builder.AddAttribute(12, "title", "clear reference");
            builder.AddAttribute(13, "onclick", EventCallback.Factory.Create(this, () =>
                context.OnValueChanged?.Invoke(info.CreateEmpty())));
            builder.AddContent(14, "×");
            builder.CloseElement();
        }

        builder.CloseElement();
    };

    private static bool TryBuild(DataRefTypeInfo info, string? raw, out object? value)
    {
        if (string.IsNullOrEmpty(raw))
        {
            value = info.CreateEmpty();
            return true;
        }

        if (info.IsStringKey)
        {
            value = info.Build(raw);
            return true;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            value = info.Build(parsed);
            return true;
        }

        value = null;
        return false;
    }
}
