#nullable enable
using System.Collections.Generic;

namespace Datra.Lab
{
    /// <summary>
    /// Everything a front end needs to build the lab screen for one game without knowing the
    /// game: the knobs, the bot settings on offer, the checks and the defaults.
    /// </summary>
    public sealed class LabSchema
    {
        public string Title { get; set; } = string.Empty;

        /// <summary>CLR name of the data context the knobs were read from.</summary>
        public string Context { get; set; } = string.Empty;

        /// <summary>Hash of the baseline data. Changes when the saved data does.</summary>
        public string BaselineHash { get; set; } = string.Empty;

        public List<KnobInfo> Knobs { get; set; } = new List<KnobInfo>();
        public List<PolicyInfo> Policies { get; set; } = new List<PolicyInfo>();
        public List<PolicyParamInfo> PolicyParams { get; set; } = new List<PolicyParamInfo>();
        public List<CheckInfo> Checks { get; set; } = new List<CheckInfo>();
        public LabDefaults Defaults { get; set; } = new LabDefaults();

        /// <summary>Snapshots can be saved and loaded.</summary>
        public bool Snapshots { get; set; }

        /// <summary>Version of the stored physics outcomes, when the lab has any.</summary>
        public string? OutcomeVersion { get; set; }
    }

    public sealed class PolicyInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
    }

    /// <summary>A number a bot policy is tuned by. A scenario setting, not a knob: it is not game data.</summary>
    public sealed class PolicyParamInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double Default { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Step { get; set; }
    }

    public sealed class CheckInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
    }

    public sealed class LabDefaults
    {
        public int Bots { get; set; }

        /// <summary>Most bots one run may ask for.</summary>
        public int MaxBots { get; set; }

        public int Seed { get; set; }
        public string Policy { get; set; } = string.Empty;
    }
}
