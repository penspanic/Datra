#nullable enable
using System;
using System.Reflection;
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Editor.Models;
using Datra.WebEditor.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Datra.WebEditor.Handlers;

/// <summary>
/// Inline edit for a <see cref="LocaleRef"/> tagged with <see cref="FixedLocaleAttribute"/>.
/// Writes through to <see cref="Datra.Editor.Interfaces.ILocaleEditorService"/>; the LocaleRef
/// key itself is immutable — only the text body changes.
/// </summary>
public sealed class LocaleRefFieldHandler : IBlazorFieldHandler
{
    public int Priority => 100;

    public bool CanHandle(Type type, MemberInfo? member = null)
    {
        if (type != typeof(LocaleRef)) return false;
        return member is PropertyInfo property &&
               property.GetCustomAttribute<FixedLocaleAttribute>() != null;
    }

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var localeRef = context.Value is LocaleRef lr ? lr : (LocaleRef?)null;
        var localeService = context.LocaleService;

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "datra-locale");

        builder.OpenElement(2, "input");
        builder.AddAttribute(3, "type", "text");
        builder.AddAttribute(4, "class", "datra-input datra-locale__text");

        if (localeRef.HasValue && localeService != null)
        {
            var key = localeRef.Value.Key;
            var localized = localeService.GetText(key);
            builder.AddAttribute(5, "value", localized ?? "(missing)");
            builder.AddAttribute(6, "title", $"key: {key}");
            if (context.IsReadOnly) builder.AddAttribute(7, "disabled", true);
            builder.AddAttribute(8, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
            {
                var text = e.Value?.ToString() ?? string.Empty;
                localeService.SetText(key, text, localeService.CurrentLanguage);
            }));
        }
        else
        {
            builder.AddAttribute(5, "value", localeService == null ? "(no locale service)" : "(no key)");
            builder.AddAttribute(6, "readonly", true);
        }

        builder.CloseElement();
        builder.CloseElement();
    };
}
