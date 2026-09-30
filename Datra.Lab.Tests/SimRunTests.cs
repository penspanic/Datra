using Datra.Lab.Sample.Generated;
using Datra.Providers;
using Xunit;

namespace Datra.Lab.Tests;

/// <summary>The record contract: what a simulator writes and what the aggregator makes of it.</summary>
public class SimRunTests
{
    private static readonly DescentContext NoData = new(new FileSystemRawDataProvider("."));

    private sealed class Script : ISimulator<DescentContext>
    {
        private readonly Action<SimRun> _play;
        public Script(Action<SimRun> play) => _play = play;
        public void Play(DescentContext data, SimRun run) => _play(run);
    }

    private static IReadOnlyList<BotRecord> Run(Action<SimRun> play, int bots = 1, IOutcomeReader? outcomes = null) =>
        BotRunner.Run(new Script(play), NoData, new RunSettings { Bots = bots }, outcomes);

    [Fact]
    public void Stages_ticks_purchases_and_marks_are_recorded_against_game_time()
    {
        var record = Run(run =>
        {
            run.Stage("A", targetSeconds: 60, label: "First");
            run.Tick(20, earned: 10);
            run.Purchase("hat", 5, group: "Account", label: "Hat Lv1");
            run.Tick(10, earned: 0, kind: "shop");
            run.Mark("first hat");
            run.Stage("B");
            run.Tick(40, earned: 30);
        })[0];

        Assert.True(record.Finished);
        Assert.Equal(70, record.TotalSeconds);
        Assert.Equal(40, record.TotalEarned);

        Assert.Equal(new[] { "A", "B" }, record.Stages.Select(s => s.Id));
        Assert.Equal("First", record.Stages[0].Label);
        Assert.Equal(60, record.Stages[0].TargetSeconds);
        Assert.Equal((0d, 30d), (record.Stages[0].StartSeconds, record.Stages[0].EndSeconds!.Value));
        Assert.Equal((30d, 70d), (record.Stages[1].StartSeconds, record.Stages[1].EndSeconds!.Value));

        var purchase = Assert.Single(record.Purchases);
        Assert.Equal((20d, "hat", "Hat Lv1", 5d, "Account", 0),
            (purchase.Time, purchase.Id, purchase.Label, purchase.Cost, purchase.Group, purchase.Stage));
        Assert.Equal(20, record.FirstPurchaseSeconds);

        var mark = Assert.Single(record.Marks);
        Assert.Equal((30d, "first hat", 0), (mark.Time, mark.Name, mark.Stage));

        Assert.Equal(new[] { "run", "shop", "run" }, record.Ticks.Select(t => t.Kind));
        Assert.Equal(40, record.LongestTickSeconds("run"));
        Assert.Equal(10, record.LongestTickSeconds("shop"));
    }

    [Fact]
    public void Entering_the_stage_the_bot_is_already_in_does_nothing()
    {
        var record = Run(run =>
        {
            run.Stage("A");
            run.Tick(10);
            run.Stage("A");
            run.Tick(10);
        })[0];

        var stage = Assert.Single(record.Stages);
        Assert.Equal(20, stage.EndSeconds);
    }

    [Fact]
    public void Progress_can_fall_when_spending_sets_the_bot_back()
    {
        var records = Run(run =>
        {
            run.Stage("A");
            run.Tick(10, 60);
            run.Progress(0.6);
            run.Purchase("x", 40);
            run.Progress(0.2);      // the wallet is the fare box
            run.Tick(10, 80);
            run.Progress(1);
            run.Stage("B");
            run.Tick(20, 0);
        });
        var result = ResultAggregator.Aggregate(records);

        double DepthAt(double t) => result.Progression.Points.Last(p => p.T <= t + 1e-9).Median;

        Assert.Equal(40, result.Progression.TMax);
        Assert.Equal(ResultAggregator.ProgressionSamples + 1, result.Progression.Points.Count);
        Assert.Equal(0, DepthAt(5));
        Assert.Equal(0.2, DepthAt(15), 6);     // after the purchase, below where it was
        Assert.Equal(1, DepthAt(25), 6);       // in stage B, nothing done yet
        Assert.Equal(2, DepthAt(40), 6);       // finished: past the last stage
    }

    [Fact]
    public void Stage_times_are_the_median_across_bots_with_a_p10_p90_range()
    {
        var records = Run(run =>
        {
            run.Stage("A", targetSeconds: 100);
            run.Tick(100 + run.Bot * 10);      // bots 0..10 take 100..200 s
            run.Stage("B");
            run.Tick(50);
        }, bots: 11);
        var result = ResultAggregator.Aggregate(records);

        var a = result.Stages[0];
        Assert.Equal((11, 11), (a.Reached, a.Completed));
        Assert.Equal(150, a.Median);
        Assert.Equal(110, a.P10);
        Assert.Equal(190, a.P90);
        Assert.Equal(100, a.TargetSeconds);
        Assert.Equal(200, result.Total!.Median);
        Assert.Equal(11, result.Finished);

        // The representative bot is the one closest to the median total.
        Assert.Equal(5, result.Purchases.Bot);
        Assert.Equal(new[] { 150d, 200d }, result.Purchases.StageEnds.Select(s => s.T));
    }

    [Fact]
    public void Purchase_timeline_is_the_representative_bots_with_medians_across_all()
    {
        var records = Run(run =>
        {
            run.Stage("A");
            run.Tick(10 + run.Bot);
            run.Purchase("a", 10, "Account");
            run.Tick(10);
            if (run.Bot % 2 == 0) run.Purchase("b", 20, "Floor device");
        }, bots: 5);
        var result = ResultAggregator.Aggregate(records);

        Assert.Equal(12, result.Purchases.FirstSeconds);
        Assert.Equal(2, result.Purchases.CountMedian);
        Assert.Equal(2, result.Purchases.Bot);
        Assert.Equal(new[] { "Account", "Floor device" }, result.Purchases.Groups);
        Assert.Equal(new[] { "a", "b" }, result.Purchases.Items.Select(i => i.Id));
        Assert.Equal("A", result.Purchases.Items[0].Stage);
    }

    [Fact]
    public void Checks_report_the_median_against_the_target()
    {
        var records = Run(run =>
        {
            run.Stage("A");
            run.Tick(30 + run.Bot * 30);
            if (run.Bot < 4) run.Purchase("a", 1);
        }, bots: 5);

        var result = ResultAggregator.Aggregate(records, new[]
        {
            new LabCheck("First purchase", r => r.FirstPurchaseSeconds, "<= 60", "s"),
            new LabCheck("Longest run", r => r.LongestTickSeconds("run"), "< 90", "s"),
            new LabCheck("Never", r => double.NaN, ">= 1"),
        });

        // Bot 4 never bought: it has no value and is left out rather than counted as zero.
        Assert.Equal((75d, false), (result.Checks[0].Value!.Value, result.Checks[0].Ok!.Value));
        Assert.Equal("<= 60", result.Checks[0].Target);
        Assert.Equal((90d, false), (result.Checks[1].Value!.Value, result.Checks[1].Ok!.Value));
        Assert.Null(result.Checks[2].Value);
        Assert.Null(result.Checks[2].Ok);
        Assert.Throws<ArgumentException>(() => new LabCheck("bad", r => 0, "about 5"));
    }

    [Fact]
    public void Explanations_and_marks_are_counted_per_bot()
    {
        var records = Run(run =>
        {
            run.Stage("A");
            run.Tick(10);
            run.Stage("B");
            run.Tick(10 + run.Bot);
            if (run.Bot > 0) run.Mark("late");
            run.Explain("B", "slow stairs");
            run.Explain("B", "slow stairs");
            if (run.Bot == 0) run.Explain("A", "cold start");
        }, bots: 3);
        var result = ResultAggregator.Aggregate(records);

        Assert.Equal(new[] { ("A", "cold start", 1), ("B", "slow stairs", 3) },
            result.Explains.Select(e => (e.Stage, e.Text, e.Bots)));
        var mark = Assert.Single(result.Marks);
        Assert.Equal(("late", 2, 21.5), (mark.Name, mark.Bots, mark.FirstSeconds));
    }

    [Fact]
    public void Outcome_draws_from_the_stored_pool_with_the_bots_own_randomness()
    {
        var key = PhysicsKey.Of(("floor", "B1"), ("push", 2));
        var outcomes = new InMemoryOutcomes("v1").Add(key, Enumerable.Range(0, 50));
        var drawn = new List<int>[2];

        for (var pass = 0; pass < 2; pass++)
        {
            var seen = new List<int>();
            Run(run => { for (var i = 0; i < 20; i++) seen.Add(run.Outcome<int>(key)); }, outcomes: outcomes);
            drawn[pass] = seen;
        }

        Assert.Equal(drawn[0], drawn[1]);
        Assert.True(drawn[0].Distinct().Count() > 5);
        Assert.All(drawn[0], v => Assert.InRange(v, 0, 49));
    }

    [Fact]
    public void A_missing_physics_key_stops_that_bot_and_is_named_in_the_result()
    {
        var known = PhysicsKey.Of(("floor", "B1"));
        var unknown = PhysicsKey.Of(("floor", "B9"));
        var outcomes = new InMemoryOutcomes().Add(known, new[] { 1 });

        var records = Run(run =>
        {
            run.Stage("A");
            run.Tick(10, run.Outcome<int>(known));
            if (run.Bot == 1) run.Outcome<int>(unknown);
            run.Tick(10);
        }, bots: 2, outcomes: outcomes);
        var result = ResultAggregator.Aggregate(records);

        Assert.True(records[0].Finished);
        Assert.False(records[1].Finished);
        Assert.Equal(new[] { "floor=B9" }, records[1].MissingOutcomes);
        Assert.Contains(result.Problems, p => p.Kind == "missingOutcome" && p.Message == "floor=B9" && p.Bots == 1);
        Assert.Contains(result.Problems, p => p.Kind == "error" && p.Bots == 1);
        Assert.Equal(1, result.Finished);
    }

    [Fact]
    public void TryOutcome_lets_the_simulator_fall_back_but_still_reports_the_key()
    {
        var records = Run(run =>
        {
            run.Stage("A");
            var found = run.TryOutcome<int>(PhysicsKey.Of(("floor", "B9")), out var value);
            run.Tick(10, found ? value : 3);
        });

        Assert.True(records[0].Finished);
        Assert.Equal(3, records[0].TotalEarned);
        Assert.Equal(new[] { "floor=B9" }, records[0].MissingOutcomes);
    }

    [Fact]
    public void Physics_key_is_the_same_whatever_order_its_parts_are_given_in()
    {
        var a = PhysicsKey.Of(("push", 2), ("floor", "B1"), ("scale", 1.5));
        var b = PhysicsKey.Of(("floor", "B1"), ("scale", 1.5), ("push", 2));

        Assert.Equal("floor=B1;push=2;scale=1.5", a.Text);
        Assert.Equal(a, b);
        Assert.Equal(a.Hash("v1"), b.Hash("v1"));
        Assert.NotEqual(a.Hash("v1"), a.Hash("v2"));
        Assert.Equal(a, PhysicsKey.Parse(a.Text));
    }

    [Fact]
    public void Jsonl_reader_loads_one_pool_per_key_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), "datra-lab-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var reader = new JsonlOutcomeReader(directory, "pin-7");
            var key = PhysicsKey.Of(("floor", "B1"), ("push", 0));
            File.WriteAllLines(reader.PathFor(key), new[]
            {
                """{"steps":3,"intact":false,"seconds":5.4}""",
                "",
                """{"steps":8,"intact":true,"seconds":9.4}""",
            });

            var pool = reader.Pool<Datra.Lab.Sample.BottleOutcome>(key);

            Assert.Equal("pin-7", reader.Version);
            Assert.Equal(new[] { 3, 8 }, pool!.Select(o => o.Steps));
            Assert.True(pool![1].Intact);
            Assert.Null(reader.Pool<Datra.Lab.Sample.BottleOutcome>(PhysicsKey.Of(("floor", "B2"))));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SimRandom_is_a_fixed_sequence_per_seed()
    {
        var a = new SimRandom(42);
        var b = new SimRandom(42);
        var first = Enumerable.Range(0, 5).Select(_ => a.NextULong()).ToList();

        Assert.Equal(first, Enumerable.Range(0, 5).Select(_ => b.NextULong()));
        // Pinned: a change to the generator would silently change every stored result.
        Assert.Equal(13679457532755275413UL, first[0]);
        Assert.All(Enumerable.Range(0, 1000).Select(_ => a.NextDouble()), v => Assert.InRange(v, 0, 0.9999999999999999));
    }
}
