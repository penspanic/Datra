#nullable enable
using System.Collections.Generic;

namespace Datra.Lab
{
    /// <summary>
    /// What a batch of bots amounted to, in the shape the views draw from. All times are
    /// seconds of game time.
    /// </summary>
    public sealed class LabResult
    {
        /// <summary>The scenario as it was run, with defaults filled in.</summary>
        public Scenario Scenario { get; set; } = new Scenario();

        /// <summary>Hash of the baseline data the scenario was laid over.</summary>
        public string BaselineHash { get; set; } = string.Empty;

        /// <summary>Version of the stored physics outcomes the bots drew from, if any.</summary>
        public string? OutcomeVersion { get; set; }

        public int Bots { get; set; }

        /// <summary>Bots that played to the end.</summary>
        public int Finished { get; set; }

        /// <summary>Time to play through, over the bots that finished. Null when none did.</summary>
        public Quantiles? Total { get; set; }

        public List<StageResult> Stages { get; set; } = new List<StageResult>();
        public ProgressionResult Progression { get; set; } = new ProgressionResult();
        public PurchaseTimeline Purchases { get; set; } = new PurchaseTimeline();
        public TickSummary Ticks { get; set; } = new TickSummary();
        public List<CheckResult> Checks { get; set; } = new List<CheckResult>();
        public List<MarkResult> Marks { get; set; } = new List<MarkResult>();
        public List<ExplainResult> Explains { get; set; } = new List<ExplainResult>();

        /// <summary>Things that make the numbers less trustworthy: unfinished bots, errors, missing physics.</summary>
        public List<ResultProblem> Problems { get; set; } = new List<ResultProblem>();

        /// <summary>How long the computation took. The only part that differs between two identical runs.</summary>
        public RunTiming? Timing { get; set; }
    }

    /// <summary>Median and the 10th / 90th percentile across bots.</summary>
    public sealed class Quantiles
    {
        public double Median { get; set; }
        public double P10 { get; set; }
        public double P90 { get; set; }
    }

    public sealed class StageResult
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double? TargetSeconds { get; set; }

        /// <summary>Bots that entered the stage.</summary>
        public int Reached { get; set; }

        /// <summary>Bots that left it again. The quantiles are over these.</summary>
        public int Completed { get; set; }

        public double? Median { get; set; }
        public double? P10 { get; set; }
        public double? P90 { get; set; }
    }

    /// <summary>
    /// Where the bots are over time. Depth is the stage index plus the fraction of the stage
    /// done, so 2.5 is halfway through the third stage and <c>stages.Count</c> is the end.
    /// </summary>
    public sealed class ProgressionResult
    {
        public double TMax { get; set; }
        public List<ProgressionPoint> Points { get; set; } = new List<ProgressionPoint>();
    }

    public sealed class ProgressionPoint
    {
        public double T { get; set; }
        public double P10 { get; set; }
        public double Median { get; set; }
        public double P90 { get; set; }
    }

    /// <summary>The purchases of one representative bot: the one whose total time is closest to the median.</summary>
    public sealed class PurchaseTimeline
    {
        public int Bot { get; set; }
        public int Seed { get; set; }

        /// <summary>Median time of the first purchase across all bots. Null when no bot bought anything.</summary>
        public double? FirstSeconds { get; set; }

        /// <summary>Median number of purchases per bot.</summary>
        public double CountMedian { get; set; }

        /// <summary>Lanes, in order of first appearance.</summary>
        public List<string> Groups { get; set; } = new List<string>();

        /// <summary>When the representative bot left each stage.</summary>
        public List<StageEnd> StageEnds { get; set; } = new List<StageEnd>();

        public List<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
    }

    public sealed class StageEnd
    {
        public string Id { get; set; } = string.Empty;
        public double T { get; set; }
    }

    public sealed class PurchaseItem
    {
        public double T { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Cost { get; set; }
        public string Group { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
    }

    public sealed class TickSummary
    {
        /// <summary>Median number of ticks per bot.</summary>
        public double CountMedian { get; set; }

        /// <summary>Longest single tick of each kind, across all bots.</summary>
        public Dictionary<string, double> LongestSeconds { get; set; } = new Dictionary<string, double>();

        /// <summary>Median total earned per bot.</summary>
        public double EarnedMedian { get; set; }
    }

    public sealed class CheckResult
    {
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;

        /// <summary>Median of the metric across bots. Null when no bot produced a value.</summary>
        public double? Value { get; set; }

        /// <summary>Whether the value meets the target. Null when there is no value.</summary>
        public bool? Ok { get; set; }
    }

    public sealed class MarkResult
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Bots that wrote the mark at least once.</summary>
        public int Bots { get; set; }

        /// <summary>Median time of the first occurrence, over those bots.</summary>
        public double FirstSeconds { get; set; }
    }

    public sealed class ExplainResult
    {
        public string Stage { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        /// <summary>Bots that gave this reason.</summary>
        public int Bots { get; set; }
    }

    public sealed class ResultProblem
    {
        /// <summary><c>unfinished</c>, <c>error</c> or <c>missingOutcome</c>.</summary>
        public string Kind { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Bots { get; set; }
    }

    public sealed class RunTiming
    {
        public double ElapsedMs { get; set; }

        /// <summary>Game time played by all bots together.</summary>
        public double SimulatedSeconds { get; set; }
    }
}
