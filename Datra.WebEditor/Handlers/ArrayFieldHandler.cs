#nullable enable
using System;
using System.Reflection;
using Datra.Editor.Models;
using Datra.WebEditor.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Datra.WebEditor.Handlers;

/// <summary>
/// Renders editors for 1D arrays (<c>int[]</c>, <c>StatType[]</c>, etc.). Add / remove rebuilds
/// the array via <see cref="Array.Copy(Array,Array,int)"/> — arrays are fixed-size in CLR, so each
/// mutation produces a new instance the caller is expected to propagate via
/// <see cref="FieldCreationContext.OnValueChanged"/>.
/// </summary>
public sealed class ArrayFieldHandler : IBlazorFieldHandler
{
    private readonly BlazorFieldTypeRegistry _registry;

    public ArrayFieldHandler(BlazorFieldTypeRegistry registry) => _registry = registry;

    // Higher than ListFieldHandler so arrays don't fall through to it.
    public int Priority => 22;

    public bool CanHandle(Type type, MemberInfo? member = null) =>
        type.IsArray && type.GetArrayRank() == 1;

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var arrayType = context.FieldType;
        var elementType = arrayType.GetElementType()!;
        var array = context.Value as Array;
        var count = array?.Length ?? 0;

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "datra-list");

        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "datra-list__header");

        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "class", "datra-list__count");
        builder.AddContent(6, count == 0 ? "empty" : $"{count} item{(count == 1 ? "" : "s")}");
        builder.CloseElement();

        if (!context.IsReadOnly)
        {
            builder.OpenElement(7, "button");
            builder.AddAttribute(8, "type", "button");
            builder.AddAttribute(9, "class", "datra-btn datra-btn--ghost datra-list__add");
            builder.AddAttribute(10, "onclick", EventCallback.Factory.Create(this, () =>
            {
                var grown = Array.CreateInstance(elementType, count + 1);
                if (array is not null) Array.Copy(array, grown, count);
                grown.SetValue(CreateDefaultValue(elementType), count);
                context.OnValueChanged?.Invoke(grown);
            }));
            builder.AddContent(11, "+ add");
            builder.CloseElement();
        }
        builder.CloseElement(); // header

        if (array is not null && count > 0)
        {
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "class", "datra-list__items");

            for (var i = 0; i < count; i++)
            {
                var index = i;
                var item = array.GetValue(index);

                builder.OpenElement(14, "div");
                builder.AddAttribute(15, "class", "datra-list__row");

                builder.OpenElement(16, "span");
                builder.AddAttribute(17, "class", "datra-list__index");
                builder.AddContent(18, index.ToString());
                builder.CloseElement();

                var handler = _registry.FindBlazorHandler(elementType);
                builder.OpenElement(19, "div");
                builder.AddAttribute(20, "class", "datra-list__field");
                if (handler is not null)
                {
                    var elementContext = new FieldCreationContext(
                        elementType,
                        item,
                        index,
                        context.LayoutMode,
                        newValue =>
                        {
                            array.SetValue(newValue, index);
                            context.OnValueChanged?.Invoke(array);
                        },
                        context.LocaleService,
                        context.IsReadOnly);
                    builder.AddContent(21, handler.CreateField(elementContext));
                }
                else
                {
                    builder.OpenElement(21, "span");
                    builder.AddAttribute(22, "class", "datra-list__missing");
                    builder.AddContent(23, $"no handler for {elementType.Name}");
                    builder.CloseElement();
                }
                builder.CloseElement(); // field

                if (!context.IsReadOnly)
                {
                    builder.OpenElement(24, "button");
                    builder.AddAttribute(25, "type", "button");
                    builder.AddAttribute(26, "class", "datra-btn datra-btn--danger datra-list__remove");
                    builder.AddAttribute(27, "title", "remove");
                    builder.AddAttribute(28, "onclick", EventCallback.Factory.Create(this, () =>
                    {
                        var shrunk = Array.CreateInstance(elementType, count - 1);
                        if (index > 0) Array.Copy(array, 0, shrunk, 0, index);
                        if (index < count - 1) Array.Copy(array, index + 1, shrunk, index, count - 1 - index);
                        context.OnValueChanged?.Invoke(shrunk);
                    }));
                    builder.AddContent(29, "×");
                    builder.CloseElement();
                }

                builder.CloseElement(); // row
            }
            builder.CloseElement(); // items
        }

        builder.CloseElement(); // list
    };

    private static object? CreateDefaultValue(Type type)
    {
        if (type == typeof(string)) return string.Empty;
        if (type.IsValueType) return Activator.CreateInstance(type);
        return type.GetConstructor(Type.EmptyTypes) != null
            ? Activator.CreateInstance(type)
            : null;
    }
}
