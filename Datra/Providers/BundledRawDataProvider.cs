using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datra.Bundles;
using Datra.Interfaces;

namespace Datra.Providers
{
    /// <summary>
    /// Read-only Datra raw provider backed by a single in-memory bundle.
    /// </summary>
    public sealed class BundledRawDataProvider : IRawDataProvider
    {
        private readonly DatraRawBundle _bundle;
        private readonly Dictionary<string, string> _files;

        public BundledRawDataProvider(DatraRawBundle bundle)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            if (_bundle.SchemaVersion != DatraRawBundle.CurrentSchemaVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported Datra raw bundle schema version {_bundle.SchemaVersion}. " +
                    $"Expected {DatraRawBundle.CurrentSchemaVersion}.");
            }

            _files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kvp in _bundle.Files ?? new Dictionary<string, string>())
                _files[DatraBundleBuilder.NormalizePath(kvp.Key)] = kvp.Value;
        }

        public DatraRawBundle Bundle => _bundle;

        public Task<string> LoadTextAsync(string path)
        {
            var normalized = DatraBundleBuilder.NormalizePath(path);
            if (!_files.TryGetValue(normalized, out var content))
                throw new System.IO.FileNotFoundException($"File not found in Datra bundle: {normalized}", normalized);
            return Task.FromResult(content);
        }

        public Task SaveTextAsync(string path, string content)
            => throw new NotSupportedException("BundledRawDataProvider is read-only.");

        public bool Exists(string path)
        {
            var normalized = DatraBundleBuilder.NormalizePath(path);
            return _files.ContainsKey(normalized);
        }

        public string ResolveFilePath(string path)
            => $"bundle://{DatraBundleBuilder.NormalizePath(path)}";

        public Task<Dictionary<string, string>> LoadMultipleTextAsync(string folderPathOrLabel, string pattern = "*.json")
        {
            var folder = NormalizeFolder(folderPathOrLabel);
            var matcher = GlobToRegex(pattern);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var kvp in _files.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!kvp.Key.StartsWith(folder, StringComparison.Ordinal)) continue;
                var name = kvp.Key.Substring(folder.Length);
                if (name.Length == 0 || name.Contains("/", StringComparison.Ordinal)) continue;
                if (!matcher.IsMatch(name)) continue;
                result[name] = kvp.Value;
            }

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<string>> ListFilesAsync(string folderPathOrLabel, string pattern = "*.json")
        {
            var folder = NormalizeFolder(folderPathOrLabel);
            var matcher = GlobToRegex(pattern);
            var result = _files.Keys
                .Where(path => path.StartsWith(folder, StringComparison.Ordinal))
                .Select(path => path.Substring(folder.Length))
                .Where(name => name.Length > 0 && !name.Contains("/", StringComparison.Ordinal))
                .Where(name => matcher.IsMatch(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(result);
        }

        public Task<bool> DeleteAsync(string path)
            => throw new NotSupportedException("BundledRawDataProvider is read-only.");

        private static string NormalizeFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return string.Empty;
            return DatraBundleBuilder.NormalizePath(folderPath).TrimEnd('/') + "/";
        }

        private static Regex GlobToRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) pattern = "*";
            var escaped = Regex.Escape(pattern)
                .Replace("\\*", "[^/]*", StringComparison.Ordinal)
                .Replace("\\?", "[^/]", StringComparison.Ordinal);
            return new Regex("^" + escaped + "$", RegexOptions.CultureInvariant);
        }
    }
}
