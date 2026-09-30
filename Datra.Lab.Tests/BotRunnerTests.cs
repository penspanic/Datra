using Datra.Lab.Sample;
using Datra.Lab.Sample.Generated;
using Xunit;

namespace Datra.Lab.Tests;

public class BotRunnerTests
{
    /// <summary>The result as JSON, minus the wall-clock time — the one part allowed to differ.</summary>
    private static string Fingerprint(LabResult result)
    {
        var timing = result.Timing;
        result.Timing = null;
        try { return LabJson.Serialize(result); }
        finally { result.Timing = timing; }
    }

    [Fact]
    public async Task Same_seed_gives_the_same_result()
    {
        using var lab = new DescentFixture();
        var scenario = new Scenario { Seed = 7, Bots = 24, Changes = { ["EconomyData.StepValue"] = 8 } };

        var first = await lab.Engine.RunAsync(scenario);
        var second = await lab.Engine.RunAsync(scenario);

        Assert.Equal(Fingerprint(first), Fingerprint(second));
        Assert.Equal(24, first.Finished);
    }

    [Fact]
    public async Task Result_does_not_depend_on_how_many_threads_ran_the_bots()
    {
        using var serial = new DescentFixture(o => o.MaxParallelism = 1);
        using var parallel = new DescentFixture(o => o.MaxParallelism = 8);
        var scenario = new Scenario { Seed = 3, Bots = 32 };

        var one = await serial.Engine.RunAsync(scenario);
        var many = await parallel.Engine.RunAsync(scenario);

        Assert.Equal(Fingerprint(one), Fingerprint(many));
    }

    [Fact]
    public async Task A_different_seed_gives_a_different_result()
    {
        using var lab = new DescentFixture();

        var a = await lab.Engine.RunAsync(new Scenario { Seed = 1 });
        var b = await lab.Engine.RunAsync(new Scenario { Seed = 2 });

        Assert.NotEqual(a.Total!.Median, b.Total!.Median);
    }

    [Fact]
    public async Task Each_bot_replays_exactly_from_its_own_seed()
    {
        using var lab = new DescentFixture();
        var data = await lab.Engine.ForkAsync();
        var outcomes = DescentPhysics.Bake(data);
        var settings = new RunSettings { Bots = 12, Seed = 5, Policy = DescentSim.Efficient };

        var batch = BotRunner.Run(new DescentSim(), data, settings, outcomes);
        var alone = BotRunner.RunOne(new DescentSim(), data, settings, 9, outcomes);

        Assert.Equal(SimRandom.Derive(5, 9), batch[9].Seed);
        Assert.Equal(batch[9].Seed, alone.Seed);
        Assert.Equal(batch[9].TotalSeconds, alone.TotalSeconds);
        Assert.Equal(batch[9].Purchases.Select(p => (p.Time, p.Id)), alone.Purchases.Select(p => (p.Time, p.Id)));
        Assert.Equal(batch[9].Ticks.Select(t => t.Earned), alone.Ticks.Select(t => t.Earned));
        Assert.Equal(Enumerable.Range(0, 12), batch.Select(r => r.Bot));
        Assert.Equal(12, batch.Select(r => r.Seed).Distinct().Count());
    }

    [Fact]
    public async Task Bots_share_luck_across_scenarios_so_a_knob_is_the_only_difference()
    {
        using var lab = new DescentFixture();
        var data = await lab.Engine.ForkAsync();
        var outcomes = DescentPhysics.Bake(data);
        var settings = new RunSettings { Bots = 6, Seed = 11, Policy = DescentSim.Cheap };

        var a = BotRunner.Run(new DescentSim(), data, settings, outcomes);
        var b = BotRunner.Run(new DescentSim(), data, settings, outcomes);

        Assert.Equal(a.Select(r => r.Seed), b.Select(r => r.Seed));
        Assert.Equal(a.Select(r => r.TotalEarned), b.Select(r => r.TotalEarned));
    }

    [Fact]
    public async Task Policy_and_policy_parameters_reach_the_simulator()
    {
        using var lab = new DescentFixture();

        var quick = await lab.Engine.RunAsync(new Scenario { PolicyParams = { ["shopSeconds"] = 2 } });
        var slow = await lab.Engine.RunAsync(new Scenario { PolicyParams = { ["shopSeconds"] = 30 } });
        var cheap = await lab.Engine.RunAsync(new Scenario { Policy = DescentSim.Cheap });

        Assert.True(quick.Total!.Median < slow.Total!.Median);
        Assert.True(cheap.Purchases.CountMedian > quick.Purchases.CountMedian);
        Assert.Equal(DescentSim.Cheap, cheap.Scenario.Policy);
        Assert.Equal(2, quick.Scenario.PolicyParams["shopSeconds"]);

        await Assert.ThrowsAsync<ScenarioException>(() => lab.Engine.RunAsync(new Scenario { Policy = "nope" }));
        await Assert.ThrowsAsync<ScenarioException>(() => lab.Engine.RunAsync(new Scenario { PolicyParams = { ["nope"] = 1 } }));
    }

    [Fact]
    public async Task A_cheaper_fare_shortens_that_floor_and_the_run()
    {
        using var lab = new DescentFixture();

        var baseline = await lab.Engine.RunAsync(new Scenario());
        var cheaper = await lab.Engine.RunAsync(new Scenario { Changes = { ["FloorData[B3].Fare"] = 800 } });

        var before = baseline.Stages.Single(s => s.Id == "B3").Median!.Value;
        var after = cheaper.Stages.Single(s => s.Id == "B3").Median!.Value;
        Assert.True(after < before, $"B3 took {after}s with the cheaper fare, {before}s before");
        Assert.True(cheaper.Total!.Median < baseline.Total!.Median);
        // The floors above B3 are played with the same luck and the same numbers.
        Assert.Equal(baseline.Stages[0].Median, cheaper.Stages[0].Median);
    }

    [Fact]
    public async Task A_knob_left_on_its_baseline_is_not_a_change()
    {
        using var lab = new DescentFixture();

        var plain = await lab.Engine.RunAsync(new Scenario());
        var same = await lab.Engine.RunAsync(new Scenario { Changes = { ["EconomyData.StepValue"] = 5 } });

        Assert.Empty(same.Scenario.Changes);
        Assert.Equal(Fingerprint(plain), Fingerprint(same));
    }

    [Fact]
    public void A_bot_that_never_finishes_is_stopped_at_the_time_cap_and_reported()
    {
        var records = BotRunner.Run(new Stuck(), new DescentContext(new Datra.Providers.FileSystemRawDataProvider(".")),
            new RunSettings { Bots = 3, MaxSimSeconds = 600 });
        var result = ResultAggregator.Aggregate(records);

        Assert.Equal(0, result.Finished);
        Assert.Null(result.Total);
        Assert.All(records, r => Assert.True(r.TotalSeconds >= 600));
        Assert.Equal(3, result.Stages[0].Reached);
        Assert.Equal(0, result.Stages[0].Completed);
        Assert.Null(result.Stages[0].Median);
        var problem = Assert.Single(result.Problems);
        Assert.Equal("unfinished", problem.Kind);
        Assert.Equal(3, problem.Bots);
    }

    [Fact]
    public void A_simulator_that_never_advances_time_is_cut_off_and_reported()
    {
        var records = BotRunner.Run(new Frozen(), new DescentContext(new Datra.Providers.FileSystemRawDataProvider(".")),
            new RunSettings { Bots = 2, MaxRecords = 1000 });

        Assert.All(records, r => Assert.False(r.Finished));
        Assert.All(records, r => Assert.Contains("looping", r.Error));
        var result = ResultAggregator.Aggregate(records);
        Assert.Equal("error", result.Problems[0].Kind);
        Assert.Equal(2, result.Problems[0].Bots);
    }

    private sealed class Stuck : ISimulator<DescentContext>
    {
        public void Play(DescentContext data, SimRun run)
        {
            run.Stage("only");
            while (!run.OutOfTime) run.Tick(30, 0);
        }
    }

    private sealed class Frozen : ISimulator<DescentContext>
    {
        public void Play(DescentContext data, SimRun run)
        {
            while (!run.OutOfTime) run.Tick(0, 1);
        }
    }
}
