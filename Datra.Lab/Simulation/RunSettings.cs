#nullable enable
using System;
using System.Collections.Generic;

namespace Datra.Lab
{
    /// <summary>How a batch of bots is run. Part of a scenario, not of the data.</summary>
    public sealed class RunSettings
    {
        /// <summary>Number of bots.</summary>
        public int Bots { get; set; } = 40;

        /// <summary>
        /// Base seed. Bot <c>i</c> plays with <see cref="SimRandom.Derive"/>(<c>Seed</c>, <c>i</c>), so two
        /// scenarios run with the same seed face the same luck and differ only by their knobs.
        /// </summary>
        public int Seed { get; set; } = 1;

        /// <summary>What the bot buys and when. The simulator decides what each name means.</summary>
        public string Policy { get; set; } = "default";

        /// <summary>Numbers the policy is tuned by (skill, seconds spent between runs).</summary>
        public Dictionary<string, double> PolicyParams { get; set; } =
            new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>Game-time cap per bot. A bot still playing at this point is reported as unfinished.</summary>
        public double MaxSimSeconds { get; set; } = 12 * 3600;

        /// <summary>Hard stop against a simulator that never advances time.</summary>
        public int MaxRecords { get; set; } = 2_000_000;

        /// <summary>Threads used for the bots. Zero means one per processor.</summary>
        public int MaxParallelism { get; set; }
    }
}
