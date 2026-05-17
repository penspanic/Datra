#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Interfaces;

namespace Datra.WebEditor.Sample;

/// <summary>
/// Reads / writes Datra resource files relative to a temp scratch directory. Mirrors the test
/// harness's <c>TestFileRawDataProvider</c> so the demo and tests share semantics.
/// </summary>
public sealed class TempFolderRawDataProvider : IRawDataProvider
{
    private readonly string _root;

    public TempFolderRawDataProvider(SampleScratchPath scratch) => _root = scratch.Path;

    public async Task<string> LoadTextAsync(string path)
    {
        var full = Path.Combine(_root, path);
        if (!File.Exists(full)) throw new FileNotFoundException(full);
        return await File.ReadAllTextAsync(full).ConfigureAwait(false);
    }

    public async Task SaveTextAsync(string path, string content)
    {
        var full = Path.Combine(_root, path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(full, content).ConfigureAwait(false);
    }

    public bool Exists(string path) => File.Exists(Path.Combine(_root, path));

    public string ResolveFilePath(string path) => Path.GetFullPath(Path.Combine(_root, path));

    public async Task<Dictionary<string, string>> LoadMultipleTextAsync(string folderPathOrLabel, string pattern = "*.json")
    {
        var folder = Path.Combine(_root, folderPathOrLabel);
        var result = new Dictionary<string, string>();
        if (!Directory.Exists(folder)) return result;

        foreach (var path in Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly))
        {
            result[Path.GetFileName(path)] = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        }
        return result;
    }

    public Task<IReadOnlyList<string>> ListFilesAsync(string folderPathOrLabel, string pattern = "*.json")
    {
        var folder = Path.Combine(_root, folderPathOrLabel);
        IReadOnlyList<string> files = Directory.Exists(folder)
            ? Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).Where(n => n is not null).Select(n => n!).ToList()
            : new List<string>();
        return Task.FromResult(files);
    }

    public Task<bool> DeleteAsync(string path)
    {
        var full = Path.Combine(_root, path);
        if (!File.Exists(full)) return Task.FromResult(false);
        File.Delete(full);
        return Task.FromResult(true);
    }
}
