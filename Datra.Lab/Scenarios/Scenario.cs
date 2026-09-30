#nullable enable
using System;
using System.Collections.Generic;

namespace Datra.Lab
{
    /// <summary>
    /// A variant to try: the baseline data plus the knobs that were moved, and how the bots are
    /// run. It records only what differs from the baseline, so it stays small enough to save,
    /// diff and put in a link.
    /// </summary>
    public sealed class Scenario
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Knob id → value, for the knobs that are off their baseline.</summary>
        public Dictionary<string, double> Changes { get; set; } =
            new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>Number of bots. Null uses the lab's default.</summary>
        public int? Bots { get; set; }

        /// <summary>Base seed. Null uses the lab's default.</summary>
        public int? Seed { get; set; }

        /// <summary>Bot policy. Null uses the lab's default.</summary>
        public string? Policy { get; set; }

        /// <summary>Policy parameter id → value, for the ones that differ from their defaults.</summary>
        public Dictionary<string, double> PolicyParams { get; set; } =
            new Dictionary<string, double>(StringComparer.Ordinal);
    }
}
