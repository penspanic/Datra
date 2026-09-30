#nullable enable
using Datra.Attributes;

namespace Datra.Lab
{
    /// <summary>
    /// A knob as a tool sees it: identity, presentation, range and the value it rests at in
    /// the baseline data. Serialised as part of <see cref="LabSchema"/>.
    /// </summary>
    public sealed class KnobInfo
    {
        /// <summary>Stable id, e.g. <c>Economy.StepValue</c> or <c>Floors[B3].Fare</c>.</summary>
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public double Min { get; set; }
        public double Max { get; set; }
        public double Step { get; set; }
        public KnobLayer Layer { get; set; }
        public KnobApply Apply { get; set; }

        /// <summary>The value the knob has when the scenario does not mention it.</summary>
        public double Baseline { get; set; }

        /// <summary>The field behind the knob holds whole numbers.</summary>
        public bool Integer { get; set; }

        /// <summary>
        /// A <see cref="KnobApply.Set"/> knob over several rows that do not share one value:
        /// moving it overwrites the per-row differences.
        /// </summary>
        public bool Mixed { get; set; }

        /// <summary>False for knobs the tool shows but does not let a scenario change yet (physics in P0).</summary>
        public bool Editable { get; set; } = true;

        /// <summary>Where the value lives in the data. Null for knobs declared in code.</summary>
        public KnobSource? Source { get; set; }
    }

    /// <summary>The table field a knob writes to.</summary>
    public sealed class KnobSource
    {
        public string DataType { get; set; } = string.Empty;
        public string Property { get; set; } = string.Empty;

        /// <summary>The row Id, or null when the knob covers every row (or the data is single).</summary>
        public string? Row { get; set; }

        /// <summary>The data file, as declared on <c>[TableData]</c> / <c>[SingleData]</c>.</summary>
        public string File { get; set; } = string.Empty;
    }
}
