#nullable enable
using System;
using Datra.Editor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Datra.WebEditor.Handlers;

/// <summary>
/// Shared render helpers for collection-type handlers (Array, List). Pulls together the table-mode
/// "compact + popover" affordance so <see cref="ArrayFieldHandler"/> and
/// <see cref="ListFieldHandler"/> stay free of duplicated layout code.
/// </summary>
/// <remarks>
/// Implementation note: we use a <c>&lt;details&gt;</c> element so the popover is self-managed —
/// no Blazor state on the handler instance (handlers are singletons) and no JS interop. Outside
/// click closes it because <c>&lt;details&gt;</c> uses native browser behaviour for toggling.
/// </remarks>
internal static class CollectionFieldRender
{
    /// <summary>
    /// Render the table-mode compact view: a read-only summary plus an edit button that opens a
    /// popover whose content is the <paramref name="renderItems"/> delegate.
    /// </summary>
    public static void RenderCompact(
        RenderTreeBuilder builder,
        BlazorFieldTypeRegistry registry,
        FieldCreationContext context,
        Type elementType,
        int count,
        string summary,
        Action<RenderTreeBuilder, int> renderItems,
        Action onAdd)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "datra-compact");

        builder.OpenElement(2, "details");
        builder.AddAttribute(3, "class", "datra-popover");

        builder.OpenElement(4, "summary");
        builder.AddAttribute(5, "class", "datra-popover__trigger");
        builder.AddAttribute(6, "title", $"edit {count} item{(count == 1 ? "" : "s")}");
        builder.AddContent(7, context.IsReadOnly ? "view" : "edit");
        builder.CloseElement(); // summary

        builder.OpenElement(8, "div");
        builder.AddAttribute(9, "class", "datra-popover__panel");

        // Header inside the popover (count + add button).
        builder.OpenElement(10, "div");
        builder.AddAttribute(11, "class", "datra-list__header");
        builder.OpenElement(12, "span");
        builder.AddAttribute(13, "class", "datra-list__count");
        builder.AddContent(14, count == 0 ? "empty" : $"{count} item{(count == 1 ? "" : "s")}");
        builder.CloseElement();
        if (!context.IsReadOnly)
        {
            builder.OpenElement(15, "button");
            builder.AddAttribute(16, "type", "button");
            builder.AddAttribute(17, "class", "datra-btn datra-btn--ghost datra-list__add");
            builder.AddAttribute(18, "onclick", EventCallback.Factory.Create(typeof(CollectionFieldRender), onAdd));
            builder.AddContent(19, "+ add");
            builder.CloseElement();
        }
        builder.CloseElement(); // header

        if (count > 0)
        {
            builder.OpenElement(20, "div");
            builder.AddAttribute(21, "class", "datra-list__items");
            renderItems(builder, 22);
            builder.CloseElement();
        }

        builder.CloseElement(); // panel
        builder.CloseElement(); // details

        builder.OpenElement(50, "span");
        builder.AddAttribute(51, "class", "datra-compact__summary");
        builder.AddAttribute(52, "title", summary);
        builder.AddContent(53, summary);
        builder.CloseElement();

        builder.CloseElement(); // wrapper
    }
}
