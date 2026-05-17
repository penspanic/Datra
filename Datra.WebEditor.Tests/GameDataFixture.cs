#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Datra.SampleData.Generated;
using Datra.Serializers;

namespace Datra.WebEditor.Tests;

/// <summary>
/// Fixture for <see cref="GameDataContext"/> — Datra.SampleData's full multi-format,
/// multi-kind (Table / Single / Asset) context. Copies the bundled Resources into a fresh
/// temp directory per test so save round-trips don't pollute the repo.
/// </summary>
internal sealed class GameDataFixture : IDisposable
{
    public string ScratchPath { get; }
    public GameDataContext Context { get; }
    public TestFileRawDataProvider Provider { get; }

    public GameDataFixture()
    {
        ScratchPath = Path.Combine(Path.GetTempPath(), "datra-webeditor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ScratchPath);
        CopyDirectory(FindResources(), ScratchPath);

        Provider = new TestFileRawDataProvider(ScratchPath);
        Context = new GameDataContext(Provider, new DataSerializerFactory());
    }

    public string ReadFile(string relative) =>
        File.ReadAllText(Path.Combine(ScratchPath, relative));

    public void Dispose()
    {
        try { Directory.Delete(ScratchPath, recursive: true); } catch { /* best-effort */ }
    }

    private static string FindResources()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "Datra.SampleData", "Resources");
            if (Directory.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate Datra.SampleData/Resources.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var sub in Directory.GetDirectories(source))
        {
            CopyDirectory(sub, Path.Combine(destination, Path.GetFileName(sub)));
        }
    }
}
