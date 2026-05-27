using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Marks a <see cref="string"/> property as an RGB color (hex form, e.g. <c>"#88aaff"</c>) so
    /// editors can render a swatch + native color picker instead of a plain text input.
    /// </summary>
    /// <remarks>
    /// Value contract: <c>#RRGGBB</c>, lowercase. Editors normalize on commit. Alpha is not part
    /// of the contract — a later <c>RgbaColorAttribute</c> can extend without breaking this one.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class ColorAttribute : Attribute
    {
    }
}
