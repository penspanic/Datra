using Xunit;

namespace Datra.Lab.Tests;

public class SnapshotTests
{
    [Fact]
    public async Task Snapshot_round_trips_the_scenario_and_the_result_it_gave()
    {
        using var lab = new DescentFixture();
        var scenario = new Scenario
        {
            Changes = { ["EconomyData.StepValue"] = 9, ["FloorData[B3].Fare"] = 1500 },
            Policy = "cheap",
            Bots = 16,
            Seed = 4,
        };

        var saved = await lab.Engine.SaveSnapshotAsync("cheap fares v2", scenario);
        var loaded = await lab.Engine.Snapshots!.LoadAsync("cheap fares v2");

        Assert.NotNull(loaded);
        Assert.Equal("cheap fares v2", loaded!.Name);
        Assert.Equal(saved.BaselineHash, loaded.BaselineHash);
        Assert.Equal(9, loaded.Scenario.Changes["EconomyData.StepValue"]);
        Assert.Equal(1500, loaded.Scenario.Changes["FloorData[B3].Fare"]);
        Assert.Equal(("cheap", 16, 4), (loaded.Scenario.Policy, loaded.Scenario.Bots, loaded.Scenario.Seed));
        Assert.Equal(LabJson.Serialize(saved.Result), LabJson.Serialize(loaded.Result));
        Assert.Equal(5, loaded.Result.Stages.Count);
    }

    [Fact]
    public async Task A_loaded_scenario_reproduces_the_stored_result()
    {
        using var lab = new DescentFixture();
        await lab.Engine.SaveSnapshotAsync("v1", new Scenario { Changes = { ["UpgradeData.BaseCost"] = 1.5 } });

        var loaded = await lab.Engine.Snapshots!.LoadAsync("v1");
        var again = await lab.Engine.RunAsync(loaded!.Scenario);

        Assert.Equal(loaded.Result.Total!.Median, again.Total!.Median);
        Assert.Equal(loaded.Result.Stages.Select(s => s.Median), again.Stages.Select(s => s.Median));
    }

    [Fact]
    public async Task Snapshots_are_listed_newest_first_as_plain_json_files()
    {
        using var lab = new DescentFixture();
        await lab.Engine.SaveSnapshotAsync("first", new Scenario());
        await Task.Delay(20);
        await lab.Engine.SaveSnapshotAsync("second", new Scenario { Changes = { ["EconomyData.StepValue"] = 6 } });

        var list = await lab.Engine.Snapshots!.ListAsync();

        Assert.Equal(new[] { "second", "first" }, list.Select(s => s.Name));
        Assert.Equal(new[] { 1, 0 }, list.Select(s => s.ChangedKnobs));
        Assert.True(File.Exists(Path.Combine(lab.SnapshotPath, "second.json")));
        Assert.Contains("\"EconomyData.StepValue\": 6", File.ReadAllText(Path.Combine(lab.SnapshotPath, "second.json")));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("")]
    [InlineData(".hidden")]
    [InlineData("trailing.")]
    [InlineData("a..b")]
    public async Task Names_that_could_leave_the_snapshot_directory_are_refused(string name)
    {
        using var lab = new DescentFixture();

        await Assert.ThrowsAsync<ScenarioException>(() => lab.Engine.SaveSnapshotAsync(name, new Scenario()));
        await Assert.ThrowsAsync<ArgumentException>(() => lab.Engine.Snapshots!.LoadAsync(name));
        Assert.False(Directory.Exists(lab.SnapshotPath) && Directory.GetFiles(lab.SnapshotPath).Length > 0);
    }

    [Fact]
    public async Task A_missing_snapshot_loads_as_null()
    {
        using var lab = new DescentFixture();

        Assert.Null(await lab.Engine.Snapshots!.LoadAsync("nothing here"));
        Assert.Empty(await lab.Engine.Snapshots.ListAsync());
    }
}
