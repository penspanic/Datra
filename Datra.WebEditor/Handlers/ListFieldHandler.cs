#nullable enable
using System;
using System.Collections;
using System.Reflection;
using System.Text;
using Datra.Editor.Models;
using Datra.Editor.Schema;
using Datra.WebEditor.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Datra.WebEditor.Handlers;

/// <summary>
/// Renders editors for <see cref="IList"/> types — typed generic lists. Each row gets a remove
/// button and a per-element widget chosen by the registry.
/// </summary>
/// <remarks>
/// In <see cref="FieldLayoutMode.Table"/> the editor collapses to a "summary + edit" pop-out so
/// table rows stay compact (see <see cref="ArrayFieldHandler"/> for the matching behaviour).
/// </remarks>
public sealed class ListFieldHandler : IBlazorFieldHandler
{
    private readonly BlazorFieldTypeRegistry _registry;

    public ListFieldHandler(BlazorFieldTypeRegistry registry) => _registry = registry;

    public int Priority => 20;

    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.List;

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var listType = context.FieldType;
        var elementType = TypeClassifier.GetElementType(listType)!;
        var list = context.Value as IList;
        var count = list?.Count ?? 0;

        if (context.LayoutMode == FieldLayoutMode.Table)
        {
            CollectionFieldRender.RenderCompact(
                builder, _registry, context, elementType, count,
                summary: BuildSummary(list, count),
                renderItems: (b, seq) => RenderItems(b, seq, context, list, elementType, count),
                onAdd: () => AddElement(context, list, elementType));
            return;
        }

        RenderExpanded(builder, context, list, elementType, count);
    };

    private void RenderExpanded(RenderTreeBuilder builder, FieldCreationContext context,
        IList? list, Type elementType, int count)
    {
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
                AddElement(context, list, elementType)));
            builder.AddContent(11, "+ add");
            builder.CloseElement();
        }
        builder.CloseElement(); // header

        // Rows
        if (list is not null && count > 0)
        {
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "class", "datra-list__items");
            RenderItems(builder, 14, context, list, elementType, count);
            builder.CloseElement(); // items
        }

        builder.CloseElement(); // list
    }

    private void RenderItems(RenderTreeBuilder builder, int startSeq, FieldCreationContext context,
        IList? list, Type elementType, int count)
    {
        if (list is null || count == 0) return;
        var seq = startSeq;
        for (var i = 0; i < count; i++)
        {
            var index = i;
            var item = list[i];

            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "datra-list__row");

            builder.OpenElement(seq++, "span");
            builder.AddAttribute(seq++, "class", "datra-list__index");
            builder.AddContent(seq++, index.ToString());
            builder.CloseElement();

            var handler = _registry.FindBlazorHandler(elementType, context.SourceMember);
            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "datra-list__field");
            if (handler != null)
            {
                var elementContext = new FieldCreationContext(
                    elementType,
                    item,
                    index,
                    context.LayoutMode == FieldLayoutMode.Table ? FieldLayoutMode.Form : context.LayoutMode,
                    newValue =>
                    {
                        if (list == null || index >= list.Count) return;
                        list[index] = newValue;
                        context.OnValueChanged?.Invoke(list);
                    },
                    context.LocaleService,
                    context.IsReadOnly,
                    context.SourceMember,
                    BuildElementPath(context, index))
                {
                    CollectionElement = item,
                    RootDataObject = context.RootDataObject
                };
                builder.AddContent(seq++, handler.CreateField(elementContext));
            }
            else
            {
                builder.OpenElement(seq++, "span");
                builder.AddAttribute(seq++, "class", "datra-list__missing");
                builder.AddContent(seq++, $"no handler for {elementType.Name}");
                builder.CloseElement();
            }
            builder.CloseElement(); // field

            if (!context.IsReadOnly)
            {
                builder.OpenElement(seq++, "button");
                builder.AddAttribute(seq++, "type", "button");
                builder.AddAttribute(seq++, "class", "datra-btn datra-btn--danger datra-list__remove");
                builder.AddAttribute(seq++, "title", "remove");
                builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () =>
                {
                    if (list == null || index >= list.Count) return;
                    list.RemoveAt(index);
                    context.OnValueChanged?.Invoke(list);
                }));
                builder.AddContent(seq++, "×");
                builder.CloseElement();
            }

            builder.CloseElement(); // row
        }
    }

    private static void AddElement(FieldCreationContext context, IList? list, Type elementType)
    {
        if (list is null) return;
        list.Add(DefaultValueFactory.CreateDefault(elementType));
        context.OnValueChanged?.Invoke(list);
    }

    private static string BuildSummary(IList? list, int count)
    {
        if (list is null || count == 0) return "empty";
        var sb = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(ArrayFieldHandler.FormatElement(list[i]));
            if (sb.Length > 40)
            {
                sb.Length = 37;
                sb.Append("...");
                break;
            }
        }
        return sb.ToString();
    }

    private static string BuildElementPath(FieldCreationContext context, int index)
    {
        var basePath = context.FieldPath ?? context.SourceMember?.Name;
        return string.IsNullOrEmpty(basePath) ? $"[{index}]" : $"{basePath}[{index}]";
    }
}
