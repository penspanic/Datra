#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Lab;
using Datra.Lab.Sample;
using Datra.Lab.Sample.Generated;
using Datra.Providers;
using Datra.WebEditor.Server;
using Datra.WebEditor.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datra.WebEditor.Tests;

/// <summary>
/// The lab over HTTP, the way the JavaScript views use it: schema out, scenario in, result out,
/// snapshots saved and read back.
/// </summary>
public class LabEndpointTests
{
    /// <summary>A real ASP.NET Core pipeline on an in-memory server, over the toy game staged in a scratch directory.</summary>
    private sealed class LabHost : IAsyncDisposable
    {
        private readonly string _root;
        public string DataPath { get; }
        public WebApplication App { get; }
        public HttpClient Client { get; }

        private LabHost(string root, string dataPath, WebApplication app)
        {
            _root = root;
            DataPath = dataPath;
            App = app;
            Client = app.GetTestClient();
        }

        public static async Task<LabHost> StartAsync(bool snapshots = true)
        {
            var root = Path.Combine(Path.GetTempPath(), "datra-webeditor-tests", Guid.NewGuid().ToString("N"));
            var dataPath = DescentData.WriteTo(Path.Combine(root, "data"));

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<IRawDataProvider>(new FileSystemRawDataProvider(dataPath));
            builder.Services.AddSingleton<IDataChangedNotifier, DataChangedNotifier>();
            builder.Services.AddDatraLab<DescentContext, DescentSim>((services, lab) =>
            {
                DescentLab.Configure(lab);
                if (snapshots) lab.SnapshotDirectory(Path.Combine(root, "snapshots"));

                var saved = new DescentContext(services.GetRequiredService<IRawDataProvider>());
                saved.LoadAllAsync().GetAwaiter().GetResult();
                lab.Outcomes = DescentPhysics.Bake(saved);
            });

            var app = builder.Build();
            app.MapDatraLab();
            await app.StartAsync();
            return new LabHost(root, dataPath, app);
        }

        public async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(string path)
        {
            using var response = await Client.GetAsync("/api/datra/lab" + path);
            return (response.StatusCode, await ReadAsync(response));
        }

        public async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(string path, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync("/api/datra/lab" + path, content);
            return (response.StatusCode, await ReadAsync(response));
        }

        public Dictionary<string, string> ReadFiles() =>
            Directory.GetFiles(DataPath).ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);

        private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        {
            var text = await response.Content.ReadAsStringAsync();
            return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
        }
    }

    /// <summary>The result without its wall-clock timing, for comparing two runs.</summary>
    private static string WithoutTiming(JsonElement result) =>
        string.Join(",", result.EnumerateObject().Where(p => p.Name != "timing").Select(p => p.Name + ":" + p.Value.GetRawText()));

    [Fact]
    public async Task Schema_describes_the_knobs_in_the_labs_own_json_shape()
    {
        await using var host = await LabHost.StartAsync();

        var (status, schema) = await host.GetAsync("/schema");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Descent · balance lab", schema.GetProperty("title").GetString());
        Assert.Equal("DescentContext", schema.GetProperty("context").GetString());

        var knobs = schema.GetProperty("knobs").EnumerateArray().ToList();
        var step = knobs.Single(k => k.GetProperty("id").GetString() == "EconomyData.StepValue");
        Assert.Equal("Step value", step.GetProperty("label").GetString());
        Assert.Equal("coins/step", step.GetProperty("unit").GetString());
        Assert.Equal(5, step.GetProperty("baseline").GetDouble());
        // Enums travel as camelCase names, not numbers.
        Assert.Equal("economy", step.GetProperty("layer").GetString());
        Assert.Equal("set", step.GetProperty("apply").GetString());
        Assert.Equal("Economy.yaml", step.GetProperty("source").GetProperty("file").GetString());

        var glass = knobs.Single(k => k.GetProperty("id").GetString() == "EconomyData.GlassToughness");
        Assert.Equal("physics", glass.GetProperty("layer").GetString());
        Assert.False(glass.GetProperty("editable").GetBoolean());

        Assert.Equal(3, schema.GetProperty("policies").GetArrayLength());
        Assert.Equal("efficient", schema.GetProperty("defaults").GetProperty("policy").GetString());
        Assert.True(schema.GetProperty("snapshots").GetBoolean());
    }

    [Fact]
    public async Task Run_round_trips_a_scenario_into_a_result()
    {
        await using var host = await LabHost.StartAsync();
        const string scenario = """{"changes":{"FloorData[B3].Fare":900,"EconomyData.StepValue":7},"bots":16,"seed":3,"policy":"cheap","policyParams":{"shopSeconds":5}}""";

        var (status, result) = await host.PostAsync("/run", scenario);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(16, result.GetProperty("bots").GetInt32());
        Assert.Equal(16, result.GetProperty("finished").GetInt32());
        Assert.True(result.GetProperty("total").GetProperty("median").GetDouble() > 0);

        var echoed = result.GetProperty("scenario");
        Assert.Equal(900, echoed.GetProperty("changes").GetProperty("FloorData[B3].Fare").GetDouble());
        Assert.Equal("cheap", echoed.GetProperty("policy").GetString());
        Assert.Equal(5, echoed.GetProperty("policyParams").GetProperty("shopSeconds").GetDouble());

        Assert.Equal(new[] { "B1", "B2", "B3", "B4", "B5" },
            result.GetProperty("stages").EnumerateArray().Select(s => s.GetProperty("id").GetString()));
        Assert.Equal(360, result.GetProperty("stages")[2].GetProperty("targetSeconds").GetDouble());
        Assert.Equal(121, result.GetProperty("progression").GetProperty("points").GetArrayLength());
        Assert.True(result.GetProperty("purchases").GetProperty("items").GetArrayLength() > 0);
        Assert.Equal(2, result.GetProperty("checks").GetArrayLength());
        Assert.Equal("toy-1", result.GetProperty("outcomeVersion").GetString());
    }

    [Fact]
    public async Task The_same_request_gives_the_same_result_and_leaves_the_data_files_alone()
    {
        await using var host = await LabHost.StartAsync();
        var before = host.ReadFiles();
        const string scenario = """{"changes":{"UpgradeData.BaseCost":1.8,"Floors.ValueGrowth":2.4}}""";

        var (_, first) = await host.PostAsync("/run", scenario);
        var (_, second) = await host.PostAsync("/run", scenario);
        var (_, baseline) = await host.PostAsync("/run", "{}");

        Assert.Equal(WithoutTiming(first), WithoutTiming(second));
        Assert.NotEqual(
            first.GetProperty("total").GetProperty("median").GetDouble(),
            baseline.GetProperty("total").GetProperty("median").GetDouble());
        Assert.Equal(before, host.ReadFiles());
    }

    [Theory]
    [InlineData("""{"changes":{"EconomyData.Nope":1}}""", "EconomyData.Nope")]
    [InlineData("""{"changes":{"EconomyData.GlassToughness":1.5}}""", "physics")]
    [InlineData("""{"policy":"reckless"}""", "reckless")]
    [InlineData("""{"bots":0}""", "Bots")]
    [InlineData("""{"changes":""", "JSON")]
    public async Task A_scenario_that_cannot_run_is_a_400_that_says_why(string body, string expected)
    {
        await using var host = await LabHost.StartAsync();

        var (status, error) = await host.PostAsync("/run", body);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(expected, error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Snapshot_is_saved_listed_and_read_back_with_its_result()
    {
        await using var host = await LabHost.StartAsync();
        const string body = """{"name":"cheap B3","scenario":{"changes":{"FloorData[B3].Fare":900},"bots":12}}""";

        var (saveStatus, saved) = await host.PostAsync("/snapshots", body);
        var (_, list) = await host.GetAsync("/snapshots");
        var (loadStatus, loaded) = await host.GetAsync("/snapshots/cheap%20B3");

        Assert.Equal(HttpStatusCode.OK, saveStatus);
        var entry = Assert.Single(list.EnumerateArray());
        Assert.Equal("cheap B3", entry.GetProperty("name").GetString());
        Assert.Equal(1, entry.GetProperty("changedKnobs").GetInt32());

        Assert.Equal(HttpStatusCode.OK, loadStatus);
        Assert.Equal(900, loaded.GetProperty("scenario").GetProperty("changes").GetProperty("FloorData[B3].Fare").GetDouble());
        Assert.Equal(12, loaded.GetProperty("scenario").GetProperty("bots").GetInt32());
        Assert.Equal(saved.GetProperty("result").GetRawText(), loaded.GetProperty("result").GetRawText());
        Assert.Equal(saved.GetProperty("baselineHash").GetString(), loaded.GetProperty("baselineHash").GetString());

        // Running the stored scenario again reproduces the stored picture.
        var (_, again) = await host.PostAsync("/run", loaded.GetProperty("scenario").GetRawText());
        Assert.Equal(
            loaded.GetProperty("result").GetProperty("stages").GetRawText(),
            again.GetProperty("stages").GetRawText());
    }

    [Fact]
    public async Task Snapshot_lookups_fail_cleanly()
    {
        await using var host = await LabHost.StartAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await host.GetAsync("/snapshots/none")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.GetAsync("/snapshots/..%2Fescape")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.PostAsync("/snapshots", """{"name":"a/b","scenario":{}}""")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.PostAsync("/snapshots", """{"name":"ok","scenario":{"changes":{"Nope":1}}}""")).Status);
        Assert.Empty((await host.GetAsync("/snapshots")).Body.EnumerateArray());
    }

    [Fact]
    public async Task Without_a_snapshot_store_the_schema_says_so_and_the_routes_are_404()
    {
        await using var host = await LabHost.StartAsync(snapshots: false);

        var (_, schema) = await host.GetAsync("/schema");

        Assert.False(schema.GetProperty("snapshots").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await host.GetAsync("/snapshots")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await host.PostAsync("/snapshots", """{"name":"x","scenario":{}}""")).Status);
    }

    [Fact]
    public async Task An_editor_save_refreshes_the_labs_baseline()
    {
        await using var host = await LabHost.StartAsync();
        double StepValue(JsonElement schema) => schema.GetProperty("knobs").EnumerateArray()
            .Single(k => k.GetProperty("id").GetString() == "EconomyData.StepValue").GetProperty("baseline").GetDouble();

        var (_, before) = await host.GetAsync("/schema");
        File.WriteAllText(Path.Combine(host.DataPath, "Economy.yaml"), "StepValue: 9\nIntactBonus: 40\nBottlesPerRun: 3\nGlassToughness: 1\n");
        await host.App.Services.GetRequiredService<IDataChangedNotifier>()
            .NotifyAsync(new DataChangedEvent(typeof(Datra.Lab.Sample.Models.EconomyData), DataChangeKind.Saved));
        var (_, after) = await host.GetAsync("/schema");

        Assert.Equal(5, StepValue(before));
        Assert.Equal(9, StepValue(after));
        Assert.NotEqual(before.GetProperty("baselineHash").GetString(), after.GetProperty("baselineHash").GetString());
    }

    [Fact]
    public void AddDatraLab_without_a_raw_data_provider_says_what_is_missing()
    {
        var services = new ServiceCollection();
        services.AddDatraLab<DescentContext, DescentSim>();
        using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ILabEngine>());

        Assert.Contains("IRawDataProvider", error.Message);
    }
}
