using Datra.Lab.Sample;
using Datra.Lab.Sample.Generated;
using Datra.Providers;

namespace Datra.Lab.Tests;

/// <summary>
/// The toy game's data staged into a scratch directory, with a lab engine over it. Every test
/// gets its own copy, so the "nothing was written" checks cannot see another test's files.
/// </summary>
internal sealed class DescentFixture : IDisposable
{
    public string DataPath { get; }
    public string SnapshotPath { get; }
    public FileSystemRawDataProvider Provider { get; }
    public LabEngine<DescentContext> Engine { get; }

    public DescentFixture(Action<LabOptions<DescentContext>>? configure = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "datra-lab-tests", Guid.NewGuid().ToString("N"));
        DataPath = DescentData.WriteTo(Path.Combine(root, "data"));
        SnapshotPath = Path.Combine(root, "snapshots");
        Provider = new FileSystemRawDataProvider(DataPath);

        var options = new LabOptions<DescentContext>();
        DescentLab.Configure(options);
        options.SnapshotDirectory(SnapshotPath);
        options.Outcomes = DescentPhysics.Bake(LoadSaved());
        configure?.Invoke(options);

        Engine = new LabEngine<DescentContext>(Provider, new DescentSim(), options);
    }

    /// <summary>A context read straight from the staged files: what is actually saved.</summary>
    public DescentContext LoadSaved()
    {
        var context = new DescentContext(new FileSystemRawDataProvider(DataPath));
        context.LoadAllAsync().GetAwaiter().GetResult();
        return context;
    }

    /// <summary>File name → bytes of every staged data file.</summary>
    public Dictionary<string, string> ReadFiles() =>
        Directory.GetFiles(DataPath).ToDictionary(f => Path.GetFileName(f)!, File.ReadAllText);

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(DataPath)!, recursive: true); } catch { /* best-effort */ }
    }
}
