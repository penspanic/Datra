#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Datra.Lab
{
    /// <summary>A phase of the game the bot went through (a floor, a tier). Times are seconds of game time.</summary>
    public sealed class StageRecord
    {
        internal StageRecord(string id, string label, double? targetSeconds, double start)
        {
            Id = id; Label = label; TargetSeconds = targetSeconds; StartSeconds = start;
        }

        public string Id { get; }
        public string Label { get; }
        public double? TargetSeconds { get; }
        public double StartSeconds { get; }

        /// <summary>When the bot left the stage. Null while it is still in it (the run stopped here).</summary>
        public double? EndSeconds { get; internal set; }
    }

    /// <summary>One slice of play: a run, a wait, a shop visit.</summary>
    public readonly struct TickRecord
    {
        public TickRecord(double time, double seconds, double earned, string kind, int stage)
        {
            Time = time; Seconds = seconds; Earned = earned; Kind = kind; Stage = stage;
        }

        /// <summary>Game time at the start of the tick.</summary>
        public double Time { get; }
        public double Seconds { get; }
        public double Earned { get; }
        public string Kind { get; }
        /// <summary>Index into <see cref="BotRecord.Stages"/>, or -1 before the first stage.</summary>
        public int Stage { get; }
    }

    public sealed class PurchaseRecord
    {
        internal PurchaseRecord(double time, string id, string label, double cost, string group, int stage)
        {
            Time = time; Id = id; Label = label; Cost = cost; Group = group; Stage = stage;
        }

        public double Time { get; }
        public string Id { get; }
        public string Label { get; }
        public double Cost { get; }
        public string Group { get; }
        public int Stage { get; }
    }

    public sealed class MarkRecord
    {
        internal MarkRecord(double time, string name, double value, int stage)
        {
            Time = time; Name = name; Value = value; Stage = stage;
        }

        public double Time { get; }
        public string Name { get; }
        public double Value { get; }
        public int Stage { get; }
    }

    /// <summary>How far through a stage the bot was at a moment: 0 on entry, 1 when it can leave.</summary>
    public readonly struct ProgressPoint
    {
        public ProgressPoint(double time, int stage, double fraction)
        {
            Time = time; Stage = stage; Fraction = fraction;
        }

        public double Time { get; }
        public int Stage { get; }
        public double Fraction { get; }
    }

    /// <summary>Everything one bot wrote during <see cref="ISimulator{TContext}.Play"/>.</summary>
    public sealed class BotRecord
    {
        internal BotRecord(int bot, int seed)
        {
            Bot = bot;
            Seed = seed;
        }

        internal List<StageRecord> StageList { get; } = new List<StageRecord>();
        internal List<TickRecord> TickList { get; } = new List<TickRecord>();
        internal List<PurchaseRecord> PurchaseList { get; } = new List<PurchaseRecord>();
        internal List<MarkRecord> MarkList { get; } = new List<MarkRecord>();
        internal List<ProgressPoint> ProgressList { get; } = new List<ProgressPoint>();
        internal List<KeyValuePair<string, string>> ExplainList { get; } = new List<KeyValuePair<string, string>>();
        internal List<string> MissingList { get; } = new List<string>();

        public int Bot { get; }
        public int Seed { get; }

        /// <summary>Game time when the bot stopped.</summary>
        public double TotalSeconds { get; internal set; }

        /// <summary>The bot played to the end: <c>Play</c> returned before the time cap and without an error.</summary>
        public bool Finished { get; internal set; }

        /// <summary>Why the bot stopped early, when it threw.</summary>
        public string? Error { get; internal set; }

        public IReadOnlyList<StageRecord> Stages => StageList;
        public IReadOnlyList<TickRecord> Ticks => TickList;
        public IReadOnlyList<PurchaseRecord> Purchases => PurchaseList;
        public IReadOnlyList<MarkRecord> Marks => MarkList;
        public IReadOnlyList<ProgressPoint> Progress => ProgressList;

        /// <summary>Stage id and sentence, as passed to <see cref="SimRun.Explain"/>.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Explains => ExplainList;

        /// <summary>Physics keys the bot asked for that had no stored outcomes.</summary>
        public IReadOnlyList<string> MissingOutcomes => MissingList;

        public double TotalMinutes => TotalSeconds / 60.0;
        public double TotalEarned => TickList.Sum(t => t.Earned);

        /// <summary>Game time of the first purchase, or NaN when the bot bought nothing.</summary>
        public double FirstPurchaseSeconds => PurchaseList.Count > 0 ? PurchaseList[0].Time : double.NaN;
        public double FirstPurchaseMinutes => FirstPurchaseSeconds / 60.0;

        /// <summary>Length of the longest tick of <paramref name="kind"/> (any kind when null).</summary>
        public double LongestTickSeconds(string? kind = null)
        {
            var longest = 0.0;
            foreach (var tick in TickList)
            {
                if (kind != null && !string.Equals(tick.Kind, kind, StringComparison.Ordinal)) continue;
                if (tick.Seconds > longest) longest = tick.Seconds;
            }
            return longest;
        }
    }
}
