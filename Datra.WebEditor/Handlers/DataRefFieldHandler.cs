#nullable enable
using System;
using System.Globalization;
using System.Reflection;
using Datra.DataTypes;
using Datra.Editor.Models;
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

    public bool CanHandle(Type type, MemberInfo? member = null) => IsDataRefType(type);

    public static bool IsDataRefType(Type type) =>
        type.IsGenericType &&
        (type.GetGenericTypeDefinition() == typeof(StringDataRef<>) ||
         type.GetGenericTypeDefinition() == typeof(IntDataRef<>));

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var dataRefType = context.FieldType;
        var isStringKey = dataRefType.GetGenericTypeDefinition() == typeof(StringDataRef<>);
        var referenced = dataRefType.GetGenericArguments()[0];
        var currentValue = context.Value;

        object? currentKey = null;
        if (currentValue != null)
        {
            currentKey = currentValue.GetType().GetProperty("Value")?.GetValue(currentValue);
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "datra-dataref");

        builder.OpenElement(2, "input");
        builder.AddAttribute(3, "type", "text");
        builder.AddAttribute(4, "class", "datra-input datra-dataref__input");
        builder.AddAttribute(5, "value", currentKey?.ToString() ?? string.Empty);
        builder.AddAttribute(6, "placeholder", $"{referenced.Name} id");
        if (context.IsReadOnly) builder.AddAttribute(7, "disabled", true);
        builder.AddAttribute(8, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            var raw = e.Value?.ToString();
            var newRef = BuildDataRef(dataRefType, raw, isStringKey);
            if (newRef.success) context.OnValueChanged?.Invoke(newRef.value);
        }));
        builder.CloseElement();

        if (!context.IsReadOnly)
        {
            builder.OpenElement(9, "button");
            builder.AddAttribute(10, "type", "button");
            builder.AddAttribute(11, "class", "datra-btn datra-btn--ghost datra-dataref__clear");
            builder.AddAttribute(12, "title", "clear reference");
            builder.AddAttribute(13, "onclick", EventCallback.Factory.Create(this, () =>
                context.OnValueChanged?.Invoke(Activator.CreateInstance(dataRefType))));
            builder.AddContent(14, "×");
            builder.CloseElement();
        }

        builder.CloseElement();
    };

    private static (bool success, object? value) BuildDataRef(Type dataRefType, string? raw, bool isStringKey)
    {
        var instance = Activator.CreateInstance(dataRefType);
        if (string.IsNullOrEmpty(raw)) return (true, instance);

        var valueProp = dataRefType.GetProperty("Value");
        if (valueProp == null) return (false, null);

        if (isStringKey)
        {
            valueProp.SetValue(instance, raw);
            return (true, instance);
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            valueProp.SetValue(instance, parsed);
            return (true, instance);
        }

        return (false, null);
    }
}
