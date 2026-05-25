using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Datra.Attributes;
using Datra.Utilities;
using YamlDotNet.Serialization;

namespace Datra.Bundles
{
    /// <summary>
    /// Builds deterministic Datra raw-data bundles from a directory tree.
    /// </summary>
    public static class DatraBundleBuilder
    {
        private static readonly string[] DefaultPatterns = { "*.json", "*.yaml", "*.yml", "*.csv" };

        public static DatraRawBundle FromDirectory(
            string basePath,
            IEnumerable<string>? patterns = null,
            Func<string, bool>? includePath = null)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                throw new ArgumentException("Base path is required.", nameof(basePath));
            if (!Directory.Exists(basePath))
                throw new DirectoryNotFoundException($"Datra bundle base directory not found: {basePath}");

            var root = Path.GetFullPath(basePath);
            var files = EnumerateFiles(root, patterns ?? DefaultPatterns, includePath)
                .ToDictionary(
                    p => NormalizeRelativePath(root, p),
                    p => File.ReadAllText(p, Encoding.UTF8),
                    StringComparer.Ordinal);

            var bundle = new DatraRawBundle
            {
                Files = files,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };
            bundle.ContentHash = ComputeContentHash(files);
            return bundle;
        }

        /// <summary>
        /// Returns a new bundle whose YAML and CSV files have been converted to JSON
        /// in-place. The file path keys are preserved (so generated <c>[TableData]</c>
        /// lookups still find them) — a <see cref="DatraRawBundle.FormatOverrides"/>
        /// entry per converted file tells the runtime to dispatch to the JSON
        /// serializer regardless of the path extension.
        ///
        /// CSV is left untouched (its row-oriented serializer is source-generated and
        /// already trim/AOT-safe). Files already keyed as <c>*.json</c> are passed
        /// through verbatim.
        ///
        /// Use case: the Tidemark wasm client receives a JSON-only bundle so
        /// YamlDotNet is no longer reachable in the client trim graph.
        /// </summary>
        public static DatraRawBundle NormalizeToJson(DatraRawBundle source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Files == null) throw new ArgumentException("Source bundle has no Files.", nameof(source));

            var yamlDeserializer = new DeserializerBuilder().Build();
            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };

            var outFiles = new Dictionary<string, string>(source.Files.Count, StringComparer.Ordinal);
            var overrides = new Dictionary<string, DataFormat>(StringComparer.Ordinal);

            foreach (var kvp in source.Files)
            {
                var path = kvp.Key;
                var content = kvp.Value ?? string.Empty;
                var fmt = DataFormatHelper.DetectFormat(path);

                if (fmt == DataFormat.Yaml)
                {
                    // YAML → opaque object graph → JSON via STJ. The runtime serializer
                    // re-parses this content with its proper [TableData] type info,
                    // so we only need a structurally faithful conversion here.
                    using var reader = new StringReader(content);
                    var graph = yamlDeserializer.Deserialize(reader);
                    var jsonContent = JsonSerializer.Serialize(graph, jsonOptions);
                    outFiles[path] = jsonContent;
                    overrides[path] = DataFormat.Json;
                }
                else
                {
                    outFiles[path] = content;
                }
            }

            var bundle = new DatraRawBundle
            {
                Files = outFiles,
                FormatOverrides = overrides,
                CreatedAtUtc = source.CreatedAtUtc,
            };
            bundle.ContentHash = ComputeContentHash(outFiles);
            return bundle;
        }

        public static string ComputeContentHash(IReadOnlyDictionary<string, string> files)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));

            using (var sha = SHA256.Create())
            {
                foreach (var kvp in files.OrderBy(k => NormalizePath(k.Key), StringComparer.Ordinal))
                {
                    var path = NormalizePath(kvp.Key);
                    var pathBytes = Encoding.UTF8.GetBytes(path);
                    var contentBytes = Encoding.UTF8.GetBytes(kvp.Value ?? string.Empty);

                    AddLengthPrefixed(sha, pathBytes);
                    AddLengthPrefixed(sha, contentBytes);
                }

                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToHex(sha.Hash!);
            }
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));

            var normalized = path.Replace('\\', '/').TrimStart('/');
            while (normalized.Contains("//", StringComparison.Ordinal))
                normalized = normalized.Replace("//", "/", StringComparison.Ordinal);

            if (Path.IsPathRooted(normalized) ||
                normalized == "." ||
                normalized == ".." ||
                normalized.StartsWith("../", StringComparison.Ordinal) ||
                normalized.Contains("/../", StringComparison.Ordinal) ||
                normalized.EndsWith("/..", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Datra bundle paths must be relative and safe: {path}", nameof(path));
            }

            return normalized;
        }

        private static IEnumerable<string> EnumerateFiles(
            string root,
            IEnumerable<string> patterns,
            Func<string, bool>? includePath)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pattern in patterns)
            {
                foreach (var file in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
                {
                    var relative = NormalizeRelativePath(root, file);
                    if (includePath != null && !includePath(relative)) continue;
                    if (seen.Add(file)) yield return file;
                }
            }
        }

        private static string NormalizeRelativePath(string root, string file)
        {
            var relative = Path.GetRelativePath(root, file);
            return NormalizePath(relative);
        }

        private static void AddLengthPrefixed(HashAlgorithm hash, byte[] bytes)
        {
            var length = BitConverter.GetBytes(bytes.Length);
            if (!BitConverter.IsLittleEndian) Array.Reverse(length);

            hash.TransformBlock(length, 0, length.Length, null, 0);
            if (bytes.Length > 0)
                hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
