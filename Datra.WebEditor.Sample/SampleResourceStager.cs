#nullable enable
using System;
using System.IO;

namespace Datra.WebEditor.Sample;

/// <summary>
/// Strongly-typed wrapper so DI can disambiguate the scratch directory from other strings.
/// </summary>
public sealed record SampleScratchPath(string Path)
{
    public static implicit operator string(SampleScratchPath p) => p.Path;
}

/// <summary>
/// Copies <c>Datra.SampleData/Resources</c> into a fresh temp directory on app start so the
/// demo can mutate files without disturbing the source tree. Subsequent launches get a new
/// scratch directory; the previous one is best-effort deleted.
/// </summary>
public static class SampleResourceStager
{
    public static SampleScratchPath Stage()
    {
        var sourceRoot = FindSourceResources();
        var dest = Path.Combine(Path.GetTempPath(), "datra-webeditor-sample",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(dest);
        CopyDirectory(sourceRoot, dest);
        return new SampleScratchPath(dest);
    }

    private static string FindSourceResources()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "Datra.SampleData", "Resources");
            if (Directory.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        throw new DirectoryNotFoundException(
            "Could not locate Datra.SampleData/Resources. Run the sample from the Datra repository root.");
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
