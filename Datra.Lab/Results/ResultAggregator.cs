#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Datra.Lab
{
    /// <summary>
    /// Turns the bots' records into one <see cref="LabResult"/>. Pure: the same records in the
    /// same order give the same result.
    /// </summary>
    public static class ResultAggregator
    {
        /// <summary>Number of points on the progression curve.</summary>
        public const int ProgressionSamples = 120;

        public static LabResult Aggregate(IReadOnlyList<BotRecord> records, IEnumerable<LabCheck>? checks = null)
        {
            if (records is null) throw new ArgumentNullException(nameof(records));

            var result = new LabResult { Bots = records.Count };
            var finished = records.Where(r => r.Finished).ToList();
            result.Finished = finished.Count;

            if (finished.Count > 0)
                result.Total = Of(finished.Select(r => r.TotalSeconds));

            var stageOrder = StageOrder(records);
            result.Stages = Stages(records, stageOrder);
            result.Progression = Progression(records, stageOrder);

            var representative = Representative(records, finished, result.Total);
            result.Purchases = Purchases(records, representative);
            result.Ticks = Ticks(records);
            result.Marks = Marks(records);
            result.Explains = Explains(records, stageOrder);
            result.Problems = Problems(records);

            if (checks != null)
            {
                foreach (var check in checks)
                {
                    var values = records.Select(check.Metric).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                    var entry = new CheckResult { Name = check.Name, Unit = check.Unit, Target = check.Target };
                    if (values.Count > 0)
                    {
                        entry.Value = Percentile(Sorted(values), 0.5);
                        entry.Ok = check.IsMet(entry.Value.Value);
                    }
                    result.Checks.Add(entry);
                }
            }

            return result;
        }

        /// <summary>Linear-interpolated percentile of an ascending list.</summary>
        public static double Percentile(IReadOnlyList<double> sorted, double q)
        {
            if (sorted.Count == 0) return double.NaN;
            if (sorted.Count == 1) return sorted[0];
            var position = q * (sorted.Count - 1);
            var lower = (int)Math.Floor(position);
            var upper = Math.Min(sorted.Count - 1, lower + 1);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }

        private static List<double> Sorted(IEnumerable<double> values)
        {
            var list = values.ToList();
            list.Sort();
            return list;
        }

        private static Quantiles Of(IEnumerable<double> values)
        {
            var sorted = Sorted(values);
            return new Quantiles
            {
                Median = Percentile(sorted, 0.5),
                P10 = Percentile(sorted, 0.1),
                P90 = Percentile(sorted, 0.9),
            };
        }

        /// <summary>Stage ids in the order bots first meet them.</summary>
        private static List<StageRecord> StageOrder(IReadOnlyList<BotRecord> records)
        {
            var order = new List<StageRecord>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                foreach (var stage in record.Stages)
                {
                    if (seen.Add(stage.Id)) order.Add(stage);
                }
            }
            return order;
        }

        private static List<StageResult> Stages(IReadOnlyList<BotRecord> records, List<StageRecord> order)
        {
            var list = new List<StageResult>(order.Count);
            foreach (var first in order)
            {
                var durations = new List<double>();
                var reached = 0;
                foreach (var record in records)
                {
                    var visits = record.Stages.Where(s => s.Id == first.Id).ToList();
                    if (visits.Count == 0) continue;
                    reached++;
                    // A stage the bot was still in when it stopped has no length yet.
                    if (visits.All(v => v.EndSeconds.HasValue))
                        durations.Add(visits.Sum(v => v.EndSeconds!.Value - v.StartSeconds));
                }

                var entry = new StageResult
                {
                    Id = first.Id,
                    Label = first.Label,
                    TargetSeconds = first.TargetSeconds,
                    Reached = reached,
                    Completed = durations.Count,
                };
                if (durations.Count > 0)
                {
                    var q = Of(durations);
                    entry.Median = q.Median;
                    entry.P10 = q.P10;
                    entry.P90 = q.P90;
                }
                list.Add(entry);
            }
            return list;
        }

        private static ProgressionResult Progression(IReadOnlyList<BotRecord> records, List<StageRecord> order)
        {
            var progression = new ProgressionResult();
            if (records.Count == 0 || order.Count == 0) return progression;

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < order.Count; i++) index[order[i].Id] = i;

            var tMax = records.Max(r => r.TotalSeconds);
            progression.TMax = tMax;
            if (!(tMax > 0)) return progression;

            var depths = new double[records.Count];
            var cursors = new int[records.Count];
            for (var sample = 0; sample <= ProgressionSamples; sample++)
            {
                var t = tMax * sample / ProgressionSamples;
                for (var b = 0; b < records.Count; b++)
                {
                    var record = records[b];
                    if (record.Finished && t >= record.TotalSeconds)
                    {
                        depths[b] = order.Count;
                        continue;
                    }

                    // Progress points are in time order and t only grows, so walk a cursor.
                    var points = record.Progress;
                    var cursor = cursors[b];
                    while (cursor + 1 < points.Count && points[cursor + 1].Time <= t) cursor++;
                    cursors[b] = cursor;

                    if (points.Count == 0 || points[cursor].Time > t)
                    {
                        depths[b] = 0;
                        continue;
                    }

                    var point = points[cursor];
                    depths[b] = index[record.Stages[point.Stage].Id] + point.Fraction;
                }

                var sorted = Sorted(depths);
                progression.Points.Add(new ProgressionPoint
                {
                    T = t,
                    P10 = Percentile(sorted, 0.1),
                    Median = Percentile(sorted, 0.5),
                    P90 = Percentile(sorted, 0.9),
                });
            }
            return progression;
        }

        /// <summary>The finished bot whose total is closest to the median; the first bot when none finished.</summary>
        private static BotRecord? Representative(IReadOnlyList<BotRecord> records, List<BotRecord> finished, Quantiles? total)
        {
            if (records.Count == 0) return null;
            if (finished.Count == 0 || total is null) return records[0];

            var best = finished[0];
            foreach (var record in finished)
            {
                if (Math.Abs(record.TotalSeconds - total.Median) < Math.Abs(best.TotalSeconds - total.Median))
                    best = record;
            }
            return best;
        }

        private static PurchaseTimeline Purchases(IReadOnlyList<BotRecord> records, BotRecord? representative)
        {
            var timeline = new PurchaseTimeline();
            if (records.Count == 0) return timeline;

            var firsts = records.Where(r => r.Purchases.Count > 0).Select(r => r.FirstPurchaseSeconds).ToList();
            if (firsts.Count > 0) timeline.FirstSeconds = Percentile(Sorted(firsts), 0.5);
            timeline.CountMedian = Percentile(Sorted(records.Select(r => (double)r.Purchases.Count)), 0.5);

            if (representative is null) return timeline;
            timeline.Bot = representative.Bot;
            timeline.Seed = representative.Seed;

            foreach (var stage in representative.Stages)
            {
                if (stage.EndSeconds.HasValue)
                    timeline.StageEnds.Add(new StageEnd { Id = stage.Id, T = stage.EndSeconds.Value });
            }

            foreach (var purchase in representative.Purchases)
            {
                if (!timeline.Groups.Contains(purchase.Group)) timeline.Groups.Add(purchase.Group);
                timeline.Items.Add(new PurchaseItem
                {
                    T = purchase.Time,
                    Id = purchase.Id,
                    Label = purchase.Label,
                    Cost = purchase.Cost,
                    Group = purchase.Group,
                    Stage = purchase.Stage >= 0 ? representative.Stages[purchase.Stage].Id : string.Empty,
                });
            }
            return timeline;
        }

        private static TickSummary Ticks(IReadOnlyList<BotRecord> records)
        {
            var summary = new TickSummary();
            if (records.Count == 0) return summary;

            summary.CountMedian = Percentile(Sorted(records.Select(r => (double)r.Ticks.Count)), 0.5);
            summary.EarnedMedian = Percentile(Sorted(records.Select(r => r.TotalEarned)), 0.5);

            var longest = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                foreach (var tick in record.Ticks)
                {
                    if (!longest.TryGetValue(tick.Kind, out var current) || tick.Seconds > current)
                        longest[tick.Kind] = tick.Seconds;
                }
            }
            foreach (var pair in longest) summary.LongestSeconds[pair.Key] = pair.Value;
            return summary;
        }

        private static List<MarkResult> Marks(IReadOnlyList<BotRecord> records)
        {
            var firsts = new Dictionary<string, List<double>>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var record in records)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var mark in record.Marks)
                {
                    if (!seen.Add(mark.Name)) continue;
                    if (!firsts.TryGetValue(mark.Name, out var list))
                    {
                        firsts[mark.Name] = list = new List<double>();
                        order.Add(mark.Name);
                    }
                    list.Add(mark.Time);
                }
            }

            return order.Select(name => new MarkResult
            {
                Name = name,
                Bots = firsts[name].Count,
                FirstSeconds = Percentile(Sorted(firsts[name]), 0.5),
            }).ToList();
        }

        private static List<ExplainResult> Explains(IReadOnlyList<BotRecord> records, List<StageRecord> order)
        {
            var counts = new Dictionary<KeyValuePair<string, string>, int>();
            var firstSeen = new List<KeyValuePair<string, string>>();
            foreach (var record in records)
            {
                foreach (var explain in record.Explains.Distinct())
                {
                    if (!counts.ContainsKey(explain))
                    {
                        counts[explain] = 0;
                        firstSeen.Add(explain);
                    }
                    counts[explain]++;
                }
            }

            int StageIndex(string id)
            {
                var i = order.FindIndex(s => s.Id == id);
                return i < 0 ? int.MaxValue : i;
            }

            return firstSeen
                .Select((e, i) => new { Explain = e, Seen = i })
                .OrderBy(e => StageIndex(e.Explain.Key))
                .ThenByDescending(e => counts[e.Explain])
                .ThenBy(e => e.Seen)
                .Select(e => new ExplainResult { Stage = e.Explain.Key, Text = e.Explain.Value, Bots = counts[e.Explain] })
                .ToList();
        }

        private static List<ResultProblem> Problems(IReadOnlyList<BotRecord> records)
        {
            var problems = new List<ResultProblem>();

            var errors = records.Where(r => r.Error != null).GroupBy(r => r.Error!, StringComparer.Ordinal);
            foreach (var group in errors.OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
                problems.Add(new ResultProblem { Kind = "error", Message = group.Key, Bots = group.Count() });

            var timedOut = records.Count(r => !r.Finished && r.Error is null);
            if (timedOut > 0)
                problems.Add(new ResultProblem { Kind = "unfinished", Message = "Still playing at the time cap.", Bots = timedOut });

            var missing = records.SelectMany(r => r.MissingOutcomes.Select(key => key))
                .GroupBy(key => key, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal);
            foreach (var group in missing)
                problems.Add(new ResultProblem { Kind = "missingOutcome", Message = group.Key, Bots = group.Count() });

            return problems;
        }
    }
}
