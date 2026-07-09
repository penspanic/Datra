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
/// Renders editors for 1D arrays (<c>int[]</c>, <c>StatType[]</c>, etc.). Add / remove rebuilds
/// the array via <see cref="Array.Copy(Array,Array,int)"/> — arrays are fixed-size in CLR, so each
/// mutation produces a new instance the caller is expected to propagate via
/// <see cref="FieldCreationContext.OnValueChanged"/>.
/// </summary>
/// <remarks>
/// In <see cref="FieldLayoutMode.Table"/> the editor collapses to a "summary + edit" pop-out so
/// table rows stay compact (matches Unity's <c>BaseArrayFieldHandler</c> behaviour). The popover
/// is a self-managed <c>&lt;details&gt;</c> element — no JS interop.
/// </remarks>
public sealed class ArrayFieldHandler : IBlazorFieldHandler
{
    private readonly BlazorFieldTypeRegistry _registry;

    public ArrayFieldHandler(BlazorFieldTypeRegistry registry) => _registry = registry;

    // Higher than ListFieldHandler so arrays don't fall through to it.
    public int Priority => 22;

    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.Array;

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var arrayType = context.FieldType;
        var elementType = arrayType.GetElementType()!;
        var array = context.Value as Array;
        var count = array?.Length ?? 0;

        if (context.LayoutMode == FieldLayoutMode.Table)
        {
            CollectionFieldRender.RenderCompact(
                builder, _registry, context, elementType, count,
                summary: BuildSummary(array, count),
                renderItems: (b, seq) => RenderItems(b, seq, context, array, elementType, count),
                onAdd: () => AddElement(context, array, elementType, count));
            return;
        }

        RenderExpanded(builder, context, array, elementType, count);
    };

    private void RenderExpanded(RenderTreeBuilder builder, FieldCreationContext context,
        Array? array, Type elementType, int count)
    {
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
                AddElement(context, array, elementType, count)));
            builder.AddContent(11, "+ add");
            builder.CloseElement();
        }
        builder.CloseElement(); // header

        if (array is not null && count > 0)
        {
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "class", "datra-list__items");
            RenderItems(builder, 14, context, array, elementType, count);
            builder.CloseElement(); // items
        }

        builder.CloseElement(); // list
    }

    private void RenderItems(RenderTreeBuilder builder, int startSeq, FieldCreationContext context,
        Array? array, Type elementType, int count)
    {
        if (array is null || count == 0) return;
        var seq = startSeq;
        for (var i = 0; i < count; i++)
        {
            var index = i;
            var item = array.GetValue(index);

            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "datra-list__row");

            builder.OpenElement(seq++, "span");
            builder.AddAttribute(seq++, "class", "datra-list__index");
            builder.AddContent(seq++, index.ToString());
            builder.CloseElement();

            var handler = _registry.FindBlazorHandler(elementType, context.SourceMember);
            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "datra-list__field");
            if (handler is not null)
            {
                var elementContext = new FieldCreationContext(
                    elementType,
                    item,
                    index,
                    // Force Form layout inside the popover so element editors expand.
                    context.LayoutMode == FieldLayoutMode.Table ? FieldLayoutMode.Form : context.LayoutMode,
                    newValue =>
                    {
                        array.SetValue(newValue, index);
                        context.OnValueChanged?.Invoke(array);
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
                    var shrunk = Array.CreateInstance(elementType, count - 1);
                    if (index > 0) Array.Copy(array, 0, shrunk, 0, index);
                    if (index < count - 1) Array.Copy(array, index + 1, shrunk, index, count - 1 - index);
                    context.OnValueChanged?.Invoke(shrunk);
                }));
                builder.AddContent(seq++, "×");
                builder.CloseElement();
            }

            builder.CloseElement(); // row
        }
    }

    private static void AddElement(FieldCreationContext context, Array? array, Type elementType, int count)
    {
        var grown = Array.CreateInstance(elementType, count + 1);
        if (array is not null) Array.Copy(array, grown, count);
        grown.SetValue(DefaultValueFactory.CreateDefault(elementType), count);
        context.OnValueChanged?.Invoke(grown);
    }

    private static string BuildSummary(Array? array, int count)
    {
        if (array is null || count == 0) return "empty";
        var sb = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(FormatElement(array.GetValue(i)));
            if (sb.Length > 40)
            {
                sb.Length = 37;
                sb.Append("...");
                break;
            }
        }
        return sb.ToString();
    }

    internal static string FormatElement(object? value)
    {
        if (value is null) return "null";
        // DataRef structs: print their Value property instead of "StringDataRef`1[Foo]".
        var info = DataRefTypeInfo.TryCreate(value.GetType());
        if (info is not null)
        {
            var key = info.GetKey(value);
            return key?.ToString() ?? string.Empty;
        }
        return value.ToString() ?? string.Empty;
    }

    private static string BuildElementPath(FieldCreationContext context, int index)
    {
        var basePath = context.FieldPath ?? context.SourceMember?.Name;
        return string.IsNullOrEmpty(basePath) ? $"[{index}]" : $"{basePath}[{index}]";
    }
}
