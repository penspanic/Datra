#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.Lab
{
    /// <summary>
    /// The raw data provider a forked context runs on. Reads fall through to the shared
    /// baseline snapshot; writes and deletes stay in this overlay and never reach the source,
    /// so nothing a simulator (or a stray <c>SaveAllAsync</c>) does can change saved data.
    /// </summary>
    public sealed class OverlayRawDataProvider : IFormatAwareRawDataProvider
    {
        private readonly RawDataSnapshot _baseline;
        // A null value is a tombstone: the file was deleted in this overlay.
        private readonly ConcurrentDictionary<string, string?> _writes =
            new ConcurrentDictionary<string, string?>(StringComparer.Ordinal);

        public OverlayRawDataProvider(RawDataSnapshot baseline)
        {
            _baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
        }

        /// <summary>Paths written in this overlay (deleted ones excluded).</summary>
        public IReadOnlyCollection<string> WrittenPaths =>
            _writes.Where(p => p.Value != null).Select(p => p.Key).ToList();

        public async Task<string> LoadTextAsync(string path)
        {
            var text = _writes.TryGetValue(path, out var written)
                ? written
                : await _baseline.LoadTextAsync(path).ConfigureAwait(false);
            return text ?? throw new FileNotFoundException($"File not found: {path}", path);
        }

        public Task SaveTextAsync(string path, string content)
        {
            _writes[path] = content;
            return Task.CompletedTask;
        }

        public bool Exists(string path) =>
            _writes.TryGetValue(path, out var written) ? written != null : _baseline.Exists(path);

        public string ResolveFilePath(string path) => "overlay://" + path.Replace('\\', '/');

        public async Task<Dictionary<string, string>> LoadMultipleTextAsync(string folderPathOrLabel, string pattern = "*.json")
        {
            var files = await _baseline.LoadFolderAsync(folderPathOrLabel, pattern).ConfigureAwait(false);
            // Copy: the caller owns the dictionary it gets, the snapshot keeps its own.
            return new Dictionary<string, string>(files, StringComparer.Ordinal);
        }

        public async Task<IReadOnlyList<string>> ListFilesAsync(string folderPathOrLabel, string pattern = "*.json")
        {
            var files = await _baseline.LoadFolderAsync(folderPathOrLabel, pattern).ConfigureAwait(false);
            return files.Keys.ToList();
        }

        public Task<bool> DeleteAsync(string path)
        {
            var existed = Exists(path);
            _writes[path] = null;
            return Task.FromResult(existed);
        }

        public DataFormat? GetFormat(string path) => _baseline.GetFormat(path);
    }
}
