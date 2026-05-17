#nullable enable
using System;
using System.Globalization;
using System.Reflection;
using Datra.Editor.Models;
using Datra.Editor.Schema;
using Datra.WebEditor.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Datra.WebEditor.Handlers;

// CSS class convention: `datra-input` is the base style. Variants add `datra-input--<modifier>`.
// All handlers are vanilla CSS — consumers theme via `--datra-*` custom properties.
//
// CanHandle gates use TypeClassifier first; primitive/int/long/float/double handlers still
// narrow on exact type identity since Integer and Floating kinds cover multiple CLR types.

public sealed class StringFieldHandler : IBlazorFieldHandler
{
    public int Priority => 1;
    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.String;

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "type", "text");
        builder.AddAttribute(2, "class", "datra-input");
        builder.AddAttribute(3, "value", context.Value as string ?? string.Empty);
        if (context.IsReadOnly) builder.AddAttribute(4, "disabled", true);
        builder.AddAttribute(5, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this,
            e => context.OnValueChanged?.Invoke(e.Value?.ToString())));
        builder.CloseElement();
    };
}

public sealed class IntFieldHandler : IBlazorFieldHandler
{
    public int Priority => 1;
    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.Integer && type == typeof(int);

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "type", "number");
        builder.AddAttribute(2, "class", "datra-input datra-input--number");
        builder.AddAttribute(3, "value", context.Value?.ToString() ?? "0");
        if (context.IsReadOnly) builder.AddAttribute(4, "disabled", true);
        builder.AddAttribute(5, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                context.OnValueChanged?.Invoke(v);
        }));
        builder.CloseElement();
    };
}

public sealed class LongFieldHandler : IBlazorFieldHandler
{
    public int Priority => 1;
    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.Integer && type == typeof(long);

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "type", "number");
        builder.AddAttribute(2, "class", "datra-input datra-input--number");
        builder.AddAttribute(3, "value", context.Value?.ToString() ?? "0");
        if (context.IsReadOnly) builder.AddAttribute(4, "disabled", true);
        builder.AddAttribute(5, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            if (long.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                context.OnValueChanged?.Invoke(v);
        }));
        builder.CloseElement();
    };
}

public sealed class FloatFieldHandler : IBlazorFieldHandler
{
    public int Priority => 1;
    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.Floating && type == typeof(float);

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "type", "number");
        builder.AddAttribute(2, "step", "any");
        builder.AddAttribute(3, "class", "datra-input datra-input--number");
        builder.AddAttribute(4, "value", FormatFloat(context.Value));
        if (context.IsReadOnly) builder.AddAttribute(5, "disabled", true);
        builder.AddAttribute(6, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            if (float.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                context.OnValueChanged?.Invoke(v);
        }));
        builder.CloseElement();
    };

    private static string FormatFloat(object? value) => value switch
    {
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        _ => "0",
    };
}

public sealed class DoubleFieldHandler : IBlazorFieldHandler
{
    public int Priority => 1;
    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.Floating && type == typeof(double);

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "type", "number");
        builder.AddAttribute(2, "step", "any");
        builder.AddAttribute(3, "class", "datra-input datra-input--number");
        builder.AddAttribute(4, "value", FormatDouble(context.Value));
        if (context.IsReadOnly) builder.AddAttribute(5, "disabled", true);
        builder.AddAttribute(6, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                context.OnValueChanged?.Invoke(v);
        }));
        builder.CloseElement();
    };

    private static string FormatDouble(object? value) => value switch
    {
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        _ => "0",
    };
}

public sealed class BoolFieldHandler : IBlazorFieldHandler
{
    public int Priority => 1;
    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.Boolean;

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        builder.OpenElement(0, "label");
        builder.AddAttribute(1, "class", "datra-checkbox");
        builder.OpenElement(2, "input");
        builder.AddAttribute(3, "type", "checkbox");
        builder.AddAttribute(4, "checked", context.Value is true);
        if (context.IsReadOnly) builder.AddAttribute(5, "disabled", true);
        builder.AddAttribute(6, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this,
            e => context.OnValueChanged?.Invoke(e.Value is true)));
        builder.CloseElement();
        builder.OpenElement(7, "span");
        builder.AddAttribute(8, "class", "datra-checkbox__indicator");
        builder.CloseElement();
        builder.CloseElement();
    };
}

public sealed class DateTimeFieldHandler : IBlazorFieldHandler
{
    public int Priority => 1;
    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.DateTime;

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var current = context.Value is DateTime dt ? dt.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) : string.Empty;
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "type", "datetime-local");
        builder.AddAttribute(2, "class", "datra-input");
        builder.AddAttribute(3, "value", current);
        if (context.IsReadOnly) builder.AddAttribute(4, "disabled", true);
        builder.AddAttribute(5, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            if (DateTime.TryParse(e.Value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var v))
                context.OnValueChanged?.Invoke(v);
        }));
        builder.CloseElement();
    };
}
