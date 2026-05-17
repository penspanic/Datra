#nullable enable
using System;
using System.Reflection;
using Datra.Editor.Models;
using Datra.WebEditor.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Datra.WebEditor.Handlers;

public sealed class EnumFieldHandler : IBlazorFieldHandler
{
    public int Priority => 10;
    public bool CanHandle(Type type, MemberInfo? member = null) => type.IsEnum;

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var enumType = context.FieldType;
        var current = context.Value?.ToString() ?? string.Empty;

        builder.OpenElement(0, "select");
        builder.AddAttribute(1, "class", "datra-input datra-input--select");
        if (context.IsReadOnly) builder.AddAttribute(2, "disabled", true);
        builder.AddAttribute(3, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            if (e.Value is null) return;
            if (Enum.TryParse(enumType, e.Value.ToString(), out var v))
                context.OnValueChanged?.Invoke(v);
        }));

        var seq = 4;
        foreach (var raw in Enum.GetValues(enumType))
        {
            var name = raw?.ToString() ?? string.Empty;
            builder.OpenElement(seq++, "option");
            builder.AddAttribute(seq++, "value", name);
            if (name == current) builder.AddAttribute(seq++, "selected", true);
            builder.AddContent(seq++, name);
            builder.CloseElement();
        }

        builder.CloseElement();
    };
}
