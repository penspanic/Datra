#nullable enable

namespace Datra.Editor.Schema
{
    /// <summary>
    /// Platform-neutral classification of a member's CLR type for editor UI purposes.
    /// </summary>
    /// <remarks>
    /// The set is the union of every kind any current handler (Unity / Web) cares about.
    /// New kinds should be added here first, then surfaced in <see cref="TypeClassifier"/>.
    /// </remarks>
    public enum FieldKind
    {
        /// <summary>No editor representation. Fallback for opaque CLR types.</summary>
        Unsupported,

        /// <summary><see cref="string"/>.</summary>
        String,

        /// <summary><see cref="bool"/>.</summary>
        Boolean,

        /// <summary>Any integral primitive — <c>byte</c>, <c>short</c>, <c>int</c>, <c>long</c>, signed or unsigned.</summary>
        Integer,

        /// <summary><see cref="float"/>, <see cref="double"/>, <see cref="decimal"/>.</summary>
        Floating,

        /// <summary><see cref="System.DateTime"/>.</summary>
        DateTime,

        /// <summary>An <see cref="System.Enum"/> subtype.</summary>
        Enum,

        /// <summary>1-D CLR array (<c>T[]</c>).</summary>
        Array,

        /// <summary>Generic <see cref="System.Collections.Generic.IList{T}"/> that is NOT an array.</summary>
        List,

        /// <summary>Generic <see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/>.</summary>
        Dictionary,

        /// <summary><c>StringDataRef&lt;T&gt;</c> or <c>IntDataRef&lt;T&gt;</c>.</summary>
        DataRef,

        /// <summary><see cref="Datra.DataTypes.LocaleRef"/>. Currently only inline-edited when tagged with <c>FixedLocale</c>.</summary>
        LocaleRef,

        /// <summary>Composite class/struct that does not match any of the above — fallback.</summary>
        Nested,
    }
}
