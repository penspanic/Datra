#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.Lab
{
    /// <summary>
    /// A read-through cache of the raw files behind the baseline data. Every fork reads from
    /// here, so a scenario run parses text from memory instead of going back to the source
    /// provider. Nothing is ever written to the source.
    /// </summary>
    public sealed class RawDataSnapshot
    {
        private readonly IRawDataProvider _source;
        private readonly ConcurrentDictionary<string, string?> _texts =
            new ConcurrentDictionary<string, string?>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _folders =
            new ConcurrentDictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        public RawDataSnapshot(IRawDataProvider source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>The text at <paramref name="path"/>, or null when the source has no such file.</summary>
        public async Task<string?> LoadTextAsync(string path)
        {
            if (_texts.TryGetValue(path, out var cached)) return cached;

            string? text;
            try
            {
                text = await _source.LoadTextAsync(path).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                text = null;
            }

            return _texts.GetOrAdd(path, text);
        }

        public bool Exists(string path) =>
            _texts.TryGetValue(path, out var cached) ? cached != null : _source.Exists(path);

        public async Task<Dictionary<string, string>> LoadFolderAsync(string folderPathOrLabel, string pattern)
        {
            var cacheKey = folderPathOrLabel + "\n" + pattern;
            if (_folders.TryGetValue(cacheKey, out var cached)) return cached;

            var files = await _source.LoadMultipleTextAsync(folderPathOrLabel, pattern).ConfigureAwait(false);
            return _folders.GetOrAdd(cacheKey, files);
        }

        public DataFormat? GetFormat(string path) =>
            _source is IFormatAwareRawDataProvider aware ? aware.GetFormat(path) : null;

        /// <summary>Forget everything read so far. The next fork re-reads the source.</summary>
        public void Clear()
        {
            _texts.Clear();
            _folders.Clear();
        }

        /// <summary>
        /// SHA-256 over every file read so far (path and content, ordered by path). Identifies
        /// the baseline a result was computed from.
        /// </summary>
        public string ComputeHash()
        {
            var builder = new StringBuilder();
            foreach (var pair in _texts.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (pair.Value is null) continue;
                builder.Append(pair.Key).Append('\n').Append(pair.Value.Length).Append('\n').Append(pair.Value).Append('\n');
            }
            foreach (var folder in _folders.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                foreach (var file in folder.Value.OrderBy(p => p.Key, StringComparer.Ordinal))
                    builder.Append(folder.Key).Append('/').Append(file.Key).Append('\n').Append(file.Value).Append('\n');
            }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }
    }
}
