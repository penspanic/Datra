#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.SampleData2.Generated;
using Datra.Serializers;

namespace Datra.WebEditor.Tests;

/// <summary>
/// Per-test fixture that gives every test its own scratch directory copied from the canonical
/// <c>Datra.SampleData2/Resources</c> bundle. Lets us mutate CSV files freely without polluting
/// the repo or stepping on parallel test runs.
/// </summary>
internal sealed class ShopFixture : IDisposable
{
    public string ScratchPath { get; }
    public ShopContext Context { get; }
    public TestFileRawDataProvider Provider { get; }

    public ShopFixture()
    {
        ScratchPath = Path.Combine(Path.GetTempPath(), "datra-webeditor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ScratchPath);
        CopyDirectory(FindSampleResources(), ScratchPath);

        Provider = new TestFileRawDataProvider(ScratchPath);
        Context = new ShopContext(Provider, new DataSerializerFactory());
    }

    public Task LoadAsync() => Context.LoadAllAsync();

    public string ReadFile(string relative) =>
        File.ReadAllText(Path.Combine(ScratchPath, relative));

    public void Dispose()
    {
        try { Directory.Delete(ScratchPath, recursive: true); } catch { /* best-effort */ }
    }

    private static string FindSampleResources()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "Datra.SampleData2", "Resources");
            if (Directory.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate Datra.SampleData2/Resources.");
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

internal sealed class TestFileRawDataProvider : IRawDataProvider
{
    private readonly string _basePath;

    public TestFileRawDataProvider(string basePath) => _basePath = basePath;

    public async Task<string> LoadTextAsync(string path)
    {
        var full = Path.Combine(_basePath, path);
        if (!File.Exists(full)) throw new FileNotFoundException(full);
        return await File.ReadAllTextAsync(full).ConfigureAwait(false);
    }

    public async Task SaveTextAsync(string path, string content)
    {
        var full = Path.Combine(_basePath, path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(full, content).ConfigureAwait(false);
    }

    public bool Exists(string path) => File.Exists(Path.Combine(_basePath, path));

    public string ResolveFilePath(string path) => Path.GetFullPath(Path.Combine(_basePath, path));
}
