#nullable enable
using System;
using System.Collections;
using System.Reflection;
using Datra.Editor.Models;
using Datra.WebEditor.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Datra.WebEditor.Handlers;

/// <summary>
/// Renders editors for <see cref="IList"/> types — typed generic lists. Each row gets a remove
/// button and a per-element widget chosen by the registry.
/// </summary>
public sealed class ListFieldHandler : IBlazorFieldHandler
{
    private readonly BlazorFieldTypeRegistry _registry;

    public ListFieldHandler(BlazorFieldTypeRegistry registry) => _registry = registry;

    public int Priority => 20;

    public bool CanHandle(Type type, MemberInfo? member = null)
    {
        if (type.IsArray) return false;
        return type.IsGenericType && typeof(IList).IsAssignableFrom(type);
    }

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var listType = context.FieldType;
        var elementType = listType.GetGenericArguments()[0];
        var list = context.Value as IList;
        var count = list?.Count ?? 0;

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "datra-list");

        // Header
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
                if (list == null) return;
                list.Add(CreateDefaultValue(elementType));
                context.OnValueChanged?.Invoke(list);
            }));
            builder.AddContent(11, "+ add");
            builder.CloseElement();
        }
        builder.CloseElement(); // header

        // Rows
        if (list != null && count > 0)
        {
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "class", "datra-list__items");

            for (var i = 0; i < count; i++)
            {
                var index = i;
                var item = list[i];

                builder.OpenElement(14, "div");
                builder.AddAttribute(15, "class", "datra-list__row");

                builder.OpenElement(16, "span");
                builder.AddAttribute(17, "class", "datra-list__index");
                builder.AddContent(18, index.ToString());
                builder.CloseElement();

                var handler = _registry.FindBlazorHandler(elementType);
                builder.OpenElement(19, "div");
                builder.AddAttribute(20, "class", "datra-list__field");
                if (handler != null)
                {
                    var elementContext = new FieldCreationContext(
                        elementType,
                        item,
                        index,
                        context.LayoutMode,
                        newValue =>
                        {
                            if (list == null || index >= list.Count) return;
                            list[index] = newValue;
                            context.OnValueChanged?.Invoke(list);
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
                        if (list == null || index >= list.Count) return;
                        list.RemoveAt(index);
                        context.OnValueChanged?.Invoke(list);
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
