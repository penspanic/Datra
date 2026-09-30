using Datra.Attributes;
using Xunit;

namespace Datra.Lab.Tests;

public class KnobSchemaTests
{
    [Fact]
    public async Task Schema_lists_attribute_knobs_with_their_metadata()
    {
        using var lab = new DescentFixture();

        var schema = await lab.Engine.GetSchemaAsync();

        var step = schema.Knobs.Single(k => k.Id == "EconomyData.StepValue");
        Assert.Equal("Step value", step.Label);
        Assert.Equal("Rewards", step.Group);
        Assert.Equal("coins/step", step.Unit);
        Assert.Equal(1, step.Min);
        Assert.Equal(15, step.Max);
        Assert.Equal(0.5, step.Step);
        Assert.Equal(KnobLayer.Economy, step.Layer);
        Assert.Equal(KnobApply.Set, step.Apply);
        Assert.Equal(5, step.Baseline);
        Assert.False(step.Integer);
        Assert.True(step.Editable);
        Assert.Equal("EconomyData", step.Source!.DataType);
        Assert.Equal("StepValue", step.Source.Property);
        Assert.Null(step.Source.Row);
        Assert.Equal("Economy.yaml", step.Source.File);
    }

    [Fact]
    public async Task EachRow_expands_into_one_knob_per_row_in_file_order()
    {
        using var lab = new DescentFixture();

        var schema = await lab.Engine.GetSchemaAsync();

        var fares = schema.Knobs.Where(k => k.Source?.Property == "Fare").ToList();
        Assert.Equal(new[] { "B1", "B2", "B3", "B4", "B5" }, fares.Select(k => k.Source!.Row));
        Assert.Equal("FloorData[B3].Fare", fares[2].Id);
        Assert.Equal("Fare · B3", fares[2].Label);
        Assert.Equal(2000, fares[2].Baseline);
        Assert.True(fares[2].Integer);
        // No range on the attribute: each row gets one around its own value.
        Assert.Equal(0, fares[2].Min);
        Assert.Equal(4000, fares[2].Max);
        Assert.Equal(64000, fares[4].Max);
    }

    [Fact]
    public async Task Row_knob_addresses_a_single_row_and_the_attribute_can_repeat()
    {
        using var lab = new DescentFixture();

        var schema = await lab.Engine.GetSchemaAsync();

        var carpet = schema.Knobs.Single(k => k.Id == "UpgradeData[carpet].Amount");
        Assert.Equal("Carpet value per level", carpet.Label);
        Assert.Equal(0.25, carpet.Baseline, 6);
        Assert.Equal("carpet", carpet.Source!.Row);

        var push = schema.Knobs.Single(k => k.Id == "UpgradeData[push].Amount");
        Assert.Equal(0.35, push.Baseline, 6);
    }

    [Fact]
    public async Task Scale_knob_over_all_rows_rests_at_one()
    {
        using var lab = new DescentFixture();

        var schema = await lab.Engine.GetSchemaAsync();

        var prices = schema.Knobs.Single(k => k.Id == "UpgradeData.BaseCost");
        Assert.Equal(KnobApply.Scale, prices.Apply);
        Assert.Equal(1, prices.Baseline);
        Assert.Null(prices.Source!.Row);
    }

    [Fact]
    public async Task Physics_knobs_are_listed_but_not_editable()
    {
        using var lab = new DescentFixture();

        var schema = await lab.Engine.GetSchemaAsync();

        var glass = schema.Knobs.Single(k => k.Id == "EconomyData.GlassToughness");
        Assert.Equal(KnobLayer.Physics, glass.Layer);
        Assert.False(glass.Editable);

        var error = await Assert.ThrowsAsync<ScenarioException>(() => lab.Engine.RunAsync(
            new Scenario { Changes = { ["EconomyData.GlassToughness"] = 1.5 } }));
        Assert.Contains("physics", error.Message);
    }

    [Fact]
    public async Task Declared_knob_reads_its_baseline_from_the_data()
    {
        using var lab = new DescentFixture();

        var schema = await lab.Engine.GetSchemaAsync();

        var growth = schema.Knobs.Single(k => k.Id == "Floors.ValueGrowth");
        Assert.Equal("Depth value growth", growth.Label);
        Assert.Null(growth.Source);
        Assert.Equal(Math.Pow(18, 0.25), growth.Baseline, 6);
        // Declared knobs come before attribute knobs.
        Assert.Equal(0, schema.Knobs.IndexOf(growth));
    }

    [Fact]
    public async Task Schema_carries_policies_parameters_checks_and_defaults()
    {
        using var lab = new DescentFixture();

        var schema = await lab.Engine.GetSchemaAsync();

        Assert.Equal(new[] { "efficient", "cheap", "random" }, schema.Policies.Select(p => p.Id));
        Assert.Equal("efficient", schema.Defaults.Policy);
        Assert.Equal(40, schema.Defaults.Bots);
        Assert.Equal(400, schema.Defaults.MaxBots);
        var shop = Assert.Single(schema.PolicyParams);
        Assert.Equal("shopSeconds", shop.Id);
        Assert.Equal(8, shop.Default);
        Assert.Equal(new[] { "First purchase", "Longest run" }, schema.Checks.Select(c => c.Name));
        Assert.True(schema.Snapshots);
        Assert.Equal("toy-1", schema.OutcomeVersion);
        Assert.Equal(64, schema.BaselineHash.Length);
    }

    [Fact]
    public async Task Unknown_knob_in_a_scenario_is_rejected_by_name()
    {
        using var lab = new DescentFixture();

        var error = await Assert.ThrowsAsync<ScenarioException>(() => lab.Engine.RunAsync(
            new Scenario { Changes = { ["EconomyData.Nope"] = 1 } }));

        Assert.Contains("EconomyData.Nope", error.Message);
    }
}
