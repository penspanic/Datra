#nullable enable
using System;
using System.Reflection;
using Datra.Attributes;
using Datra.Editor.Models;
using Datra.Editor.Schema;
using Datra.WebEditor.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Datra.WebEditor.Handlers;

/// <summary>
/// Renders a string property tagged with <see cref="ColorAttribute"/> as: swatch preview +
/// native <c>&lt;input type="color"&gt;</c> picker + hex text input (paste/copy convenience).
/// All three stay in sync; commit normalizes to lowercase <c>#rrggbb</c>.
/// </summary>
/// <remarks>
/// Priority 5: above <see cref="StringFieldHandler"/> (1) but below member-attribute handlers
/// (LocaleRef=100, DataRef=40) and container handlers (Nested=30, Array=22, List=20, Enum=10).
/// </remarks>
public sealed class ColorFieldHandler : IBlazorFieldHandler
{
    public int Priority => 5;

    public bool CanHandle(Type type, MemberInfo? member = null)
    {
        if (TypeClassifier.Classify(type, member) != FieldKind.String) return false;
        return member switch
        {
            PropertyInfo p => p.GetCustomAttribute<ColorAttribute>() is not null,
            FieldInfo f    => f.GetCustomAttribute<ColorAttribute>() is not null,
            _              => false,
        };
    }

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        var raw = context.Value as string;
        var hex = Normalize(raw);            // canonical 7-char "#rrggbb" for picker / swatch
        var display = raw ?? string.Empty;   // preserve user-entered string for the text input

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "datra-color");

        // Swatch — purely visual. Click delegates to the hidden native picker via <label for>.
        builder.OpenElement(2, "label");
        builder.AddAttribute(3, "class", "datra-color__swatch");
        builder.AddAttribute(4, "style", $"background:{hex}");
        builder.AddAttribute(5, "title", display);
        builder.CloseElement();

        // Native color picker — visually hidden but keyboard / click reachable through the swatch.
        builder.OpenElement(10, "input");
        builder.AddAttribute(11, "type", "color");
        builder.AddAttribute(12, "class", "datra-color__picker");
        builder.AddAttribute(13, "value", hex);
        if (context.IsReadOnly) builder.AddAttribute(14, "disabled", true);
        builder.AddAttribute(15, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            var v = e.Value?.ToString();
            if (!string.IsNullOrEmpty(v)) context.OnValueChanged?.Invoke(v!.ToLowerInvariant());
        }));
        builder.CloseElement();

        // Hex text — for paste/copy. Accepts #rgb / #rrggbb; commits normalized on change.
        builder.OpenElement(20, "input");
        builder.AddAttribute(21, "type", "text");
        builder.AddAttribute(22, "class", "datra-input datra-color__hex");
        builder.AddAttribute(23, "value", display);
        builder.AddAttribute(24, "spellcheck", "false");
        builder.AddAttribute(25, "maxlength", 9);
        if (context.IsReadOnly) builder.AddAttribute(26, "disabled", true);
        builder.AddAttribute(27, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
        {
            var v = e.Value?.ToString();
            // Always emit — even invalid strings round-trip so the user sees their mistake.
            context.OnValueChanged?.Invoke(v ?? string.Empty);
        }));
        builder.CloseElement();

        builder.CloseElement();
    };

    /// <summary>Map any plausible hex form to the 7-char "#rrggbb" the native picker requires.</summary>
    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "#000000";
        var s = value!.Trim();
        if (s[0] != '#') s = "#" + s;
        // #rgb -> #rrggbb
        if (s.Length == 4)
            return ("#" + s[1] + s[1] + s[2] + s[2] + s[3] + s[3]).ToLowerInvariant();
        // #rrggbb / #rrggbbaa -> truncate alpha for the picker preview
        if (s.Length >= 7) return s.Substring(0, 7).ToLowerInvariant();
        return "#000000";
    }
}
