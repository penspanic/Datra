using Datra.Interfaces;
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

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapDatraEditor();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
