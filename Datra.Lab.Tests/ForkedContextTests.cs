using Datra.Lab.Sample;
using Datra.Lab.Sample.Generated;
using Xunit;

namespace Datra.Lab.Tests;

/// <summary>
/// A scenario's knobs land in a fork. The saved files, a context loaded from them, and other
/// forks must never see them.
/// </summary>
public class ForkedContextTests
{
    private static readonly Dictionary<string, double> Changes = new()
    {
        ["EconomyData.StepValue"] = 12,          // single data, Set
        ["FloorData[B3].Fare"] = 1234,           // one row, Set
        ["UpgradeData.BaseCost"] = 2,            // every row, Scale
        ["UpgradeData[carpet].Amount"] = 0.6,    // one row of a repeated attribute
        ["Floors.ValueGrowth"] = 3,              // declared in code
    };

    [Fact]
    public async Task Fork_reads_the_knob_values_through_the_ordinary_context_api()
    {
        using var lab = new DescentFixture();

        var fork = await lab.Engine.ForkAsync(new Scenario { Changes = Changes });

        Assert.Equal(12f, fork.Economy.Current!.StepValue);
        Assert.Equal(1234, fork.Floor.LoadedItems["B3"].Fare);
        Assert.Equal(600, fork.Floor.LoadedItems["B2"].Fare);
        Assert.Equal(240f, fork.Upgrade.LoadedItems["push"].BaseCost);
        Assert.Equal(180f, fork.Upgrade.LoadedItems["carpet"].BaseCost);
        Assert.Equal(0.6f, fork.Upgrade.LoadedItems["carpet"].Amount);
        Assert.Equal(0.5f, fork.Upgrade.LoadedItems["box"].Amount);
        Assert.Equal(3, DescentData.ValueGrowth(fork), 4);
    }

    [Fact]
    public async Task Running_a_scenario_leaves_the_saved_files_byte_for_byte()
    {
        using var lab = new DescentFixture();
        var before = lab.ReadFiles();

        await lab.Engine.RunAsync(new Scenario { Changes = Changes, Bots = 8 });

        Assert.Equal(before, lab.ReadFiles());
        Assert.Equal(before.Keys.OrderBy(k => k), Directory.GetFiles(lab.DataPath).Select(Path.GetFileName).OrderBy(k => k));
    }

    [Fact]
    public async Task A_context_loaded_from_the_saved_data_does_not_see_the_scenario()
    {
        using var lab = new DescentFixture();

        await lab.Engine.RunAsync(new Scenario { Changes = Changes, Bots = 8 });
        var saved = lab.LoadSaved();

        Assert.Equal(5f, saved.Economy.Current!.StepValue);
        Assert.Equal(2000, saved.Floor.LoadedItems["B3"].Fare);
        Assert.Equal(120f, saved.Upgrade.LoadedItems["push"].BaseCost);
        Assert.Equal(0.25f, saved.Upgrade.LoadedItems["carpet"].Amount);
    }

    [Fact]
    public async Task Forks_do_not_share_objects_with_each_other()
    {
        using var lab = new DescentFixture();

        var changed = await lab.Engine.ForkAsync(new Scenario { Changes = Changes });
        var plain = await lab.Engine.ForkAsync();

        Assert.NotSame(changed.Floor.LoadedItems["B3"], plain.Floor.LoadedItems["B3"]);
        Assert.Equal(2000, plain.Floor.LoadedItems["B3"].Fare);
        Assert.Equal(5f, plain.Economy.Current!.StepValue);
    }

    [Fact]
    public async Task The_baseline_in_the_schema_is_not_moved_by_a_scenario()
    {
        using var lab = new DescentFixture();

        await lab.Engine.RunAsync(new Scenario { Changes = Changes, Bots = 8 });
        var schema = await lab.Engine.GetSchemaAsync();

        Assert.Equal(5, schema.Knobs.Single(k => k.Id == "EconomyData.StepValue").Baseline);
        Assert.Equal(2000, schema.Knobs.Single(k => k.Id == "FloorData[B3].Fare").Baseline);
    }

    [Fact]
    public async Task Saving_a_fork_writes_to_its_overlay_and_not_to_disk()
    {
        using var lab = new DescentFixture();
        var before = lab.ReadFiles();
        OverlayRawDataProvider? overlay = null;
        var forker = new ContextForker<DescentContext>(lab.Provider, provider =>
        {
            overlay = (OverlayRawDataProvider)provider;
            return new DescentContext(provider);
        });

        var fork = await forker.ForkAsync();
        var floor = fork.Floor.GetWorkingCopy("B1");
        ForkedData.Set(floor, f => f.Fare, 99999);
        fork.Floor.MarkAsModified("B1");
        await fork.SaveAllAsync();

        // The save happened, and it went to the overlay.
        Assert.Contains("Floors.yaml", overlay!.WrittenPaths);
        Assert.Contains("99999", await overlay.LoadTextAsync("Floors.yaml"));
        Assert.Equal(before, lab.ReadFiles());
        Assert.Equal(280, lab.LoadSaved().Floor.LoadedItems["B1"].Fare);
    }

    [Fact]
    public async Task Overlay_provider_keeps_writes_and_deletes_to_itself()
    {
        using var lab = new DescentFixture();
        var snapshot = new RawDataSnapshot(lab.Provider);
        var overlay = new OverlayRawDataProvider(snapshot);
        var other = new OverlayRawDataProvider(snapshot);
        var original = await lab.Provider.LoadTextAsync("Economy.yaml");

        await overlay.SaveTextAsync("Economy.yaml", "StepValue: 99");
        await overlay.SaveTextAsync("New.yaml", "x: 1");
        await overlay.DeleteAsync("Floors.yaml");

        Assert.Equal("StepValue: 99", await overlay.LoadTextAsync("Economy.yaml"));
        Assert.False(overlay.Exists("Floors.yaml"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => overlay.LoadTextAsync("Floors.yaml"));

        Assert.Equal(original, await other.LoadTextAsync("Economy.yaml"));
        Assert.True(other.Exists("Floors.yaml"));
        Assert.False(other.Exists("New.yaml"));

        Assert.Equal(original, await lab.Provider.LoadTextAsync("Economy.yaml"));
        Assert.False(lab.Provider.Exists("New.yaml"));
        Assert.True(lab.Provider.Exists("Floors.yaml"));
    }

    [Fact]
    public async Task Baseline_is_cached_until_invalidated()
    {
        using var lab = new DescentFixture();
        var first = await lab.Engine.GetSchemaAsync();

        File.WriteAllText(Path.Combine(lab.DataPath, "Economy.yaml"), "StepValue: 7\nIntactBonus: 40\nBottlesPerRun: 3\nGlassToughness: 1\n");
        var stale = await lab.Engine.GetSchemaAsync();
        lab.Engine.InvalidateBaseline();
        var fresh = await lab.Engine.GetSchemaAsync();

        Assert.Equal(5, stale.Knobs.Single(k => k.Id == "EconomyData.StepValue").Baseline);
        Assert.Equal(7, fresh.Knobs.Single(k => k.Id == "EconomyData.StepValue").Baseline);
        Assert.Equal(first.BaselineHash, stale.BaselineHash);
        Assert.NotEqual(first.BaselineHash, fresh.BaselineHash);
    }
}
