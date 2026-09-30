using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Which layer of a balance simulation a knob reaches.
    /// </summary>
    public enum KnobLayer
    {
        /// <summary>Re-pricing only: the simulator can be re-run immediately.</summary>
        Economy,

        /// <summary>
        /// Changes the outcome of a physics run, so stored outcomes no longer apply and have to
        /// be recomputed. Tools show these read-only until they can recompute.
        /// </summary>
        Physics
    }

    /// <summary>
    /// How a knob value is written to the field it points at.
    /// </summary>
    public enum KnobApply
    {
        /// <summary>The field becomes the knob value.</summary>
        Set,

        /// <summary>The field is multiplied by the knob value. The knob rests at 1.</summary>
        Scale,

        /// <summary>The knob value is added to the field. The knob rests at 0.</summary>
        Offset
    }

    /// <summary>
    /// Marks a numeric property as a tuning knob: a named value with a unit and a range that a
    /// balance tool (Datra.Lab) can move without editing the data file.
    /// </summary>
    /// <remarks>
    /// <para>On a <c>[SingleData]</c> type the knob addresses that one object. On a
    /// <c>[TableData]</c> type it addresses every row unless <see cref="Row"/> or
    /// <see cref="EachRow"/> narrows it.</para>
    /// <para>The attribute may be repeated to expose the same column as several knobs, one per
    /// <see cref="Row"/>.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Knob("Pellet value", Group = "Rewards", Unit = "coins", Min = 0.5, Max = 5, Step = 0.1)]
    /// public float PelletValue { get; set; } = 1f;
    ///
    /// [Knob("Base push", Group = "Hand", Unit = "m/s", Min = 0.5, Max = 1.4,
    ///       Layer = KnobLayer.Physics, Row = "push-strength")]
    /// public float Base { get; set; }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class KnobAttribute : Attribute
    {
        public KnobAttribute(string label)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        /// <summary>The name shown to the person turning the knob.</summary>
        public string Label { get; }

        /// <summary>Heading the knob is listed under.</summary>
        public string Group { get; set; } = string.Empty;

        /// <summary>Unit shown after the value, e.g. <c>"coins/step"</c>.</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>One line on what the value does.</summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>Lower bound. <see cref="double.NaN"/> (the default) lets the tool pick one.</summary>
        public double Min { get; set; } = double.NaN;

        /// <summary>Upper bound. <see cref="double.NaN"/> (the default) lets the tool pick one.</summary>
        public double Max { get; set; } = double.NaN;

        /// <summary>Slider step. Zero (the default) lets the tool pick one.</summary>
        public double Step { get; set; }

        /// <summary>Whether moving the knob invalidates stored physics outcomes.</summary>
        public KnobLayer Layer { get; set; } = KnobLayer.Economy;

        /// <summary>Table data only: the Id of the one row this knob addresses. Empty means every row.</summary>
        public string Row { get; set; } = string.Empty;

        /// <summary>
        /// Table data only: expand into one knob per row, labelled <c>"{Label} · {row Id}"</c>.
        /// Ignored when <see cref="Row"/> is set.
        /// </summary>
        public bool EachRow { get; set; }

        /// <summary>How the value is written to the field.</summary>
        public KnobApply Apply { get; set; } = KnobApply.Set;
    }
}
