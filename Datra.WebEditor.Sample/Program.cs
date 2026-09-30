using Datra.Interfaces;
using Datra.Lab.Sample;
using Datra.Lab.Sample.Generated;
using Datra.SampleData.Generated;
using Datra.Serializers;
using Datra.WebEditor.Extensions;
using Datra.WebEditor.Sample;
using Datra.WebEditor.Sample.Components;
using Datra.WebEditor.Server;

var builder = WebApplication.CreateBuilder(args);

// Stage the bundled Datra.SampleData/Resources into a per-launch temp directory so the
// editor can write to disk without dirtying the source tree.
var scratchPath = SampleResourceStager.Stage();
Console.WriteLine($"[Datra.WebEditor.Sample] staged sample data → {scratchPath}");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton(scratchPath);
builder.Services.AddSingleton<DataSerializerFactory>();
builder.Services.AddSingleton<IRawDataProvider>(sp =>
    new TempFolderRawDataProvider(sp.GetRequiredService<SampleScratchPath>()));
builder.Services.AddSingleton(sp => new GameDataContext(
    sp.GetRequiredService<IRawDataProvider>(),
    sp.GetRequiredService<DataSerializerFactory>()));

builder.Services.AddDatraWebEditor(opt => opt.DataContextType = typeof(GameDataContext));

// Balance lab (/lab): a toy incremental game tuned through knobs. Its three YAML files are
// staged next to the editor's sample data, so the one IRawDataProvider above serves both.
DescentData.WriteTo(scratchPath.Path);
builder.Services.AddDatraLab<DescentContext, DescentSim>((services, lab) =>
{
    DescentLab.Configure(lab);
    lab.SnapshotDirectory(Path.Combine(scratchPath.Path, "lab-snapshots"));

    // Stand-in for a game's overnight physics batch: roll the outcome pools once, from the saved data.
    var saved = new DescentContext(services.GetRequiredService<IRawDataProvider>());
    saved.LoadAllAsync().GetAwaiter().GetResult();
    lab.Outcomes = DescentPhysics.Bake(saved);
});

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapDatraEditor();
app.MapDatraLab();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
