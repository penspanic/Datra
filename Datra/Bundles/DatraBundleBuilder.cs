using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Datra.Attributes;
using Datra.Utilities;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
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
                    // YAML → JSON via the YamlStream representation model so we can
                    // see each scalar's ScalarStyle: plain scalars are type-inferred
                    // per the YAML 1.2 core schema, but quoted ones (`"0"`, `'true'`)
                    // stay as JSON strings — matching the author's intent.
                    using var reader = new StringReader(content);
                    var yamlStream = new YamlStream();
                    yamlStream.Load(reader);
                    JsonNode? node = null;
                    if (yamlStream.Documents.Count > 0)
                    {
                        node = ConvertYamlNodeToJsonNode(yamlStream.Documents[0].RootNode);
                    }
                    var jsonContent = node?.ToJsonString(jsonOptions) ?? "null";
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

        /// <summary>
        /// Convert a <see cref="YamlNode"/> tree to a <see cref="JsonNode"/> tree.
        /// Scalar typing follows YAML 1.2 core schema for plain (unquoted) scalars;
        /// single/double-quoted scalars are always emitted as JSON strings so
        /// authored quoting (e.g. <c>AttachWhenEquals: "0"</c>) survives round-trip.
        /// </summary>
        private static JsonNode? ConvertYamlNodeToJsonNode(YamlNode yaml)
        {
            switch (yaml)
            {
                case YamlMappingNode map:
                {
                    var obj = new JsonObject();
                    foreach (var entry in map.Children)
                    {
                        var key = (entry.Key as YamlScalarNode)?.Value ?? entry.Key.ToString();
                        obj[key ?? string.Empty] = ConvertYamlNodeToJsonNode(entry.Value);
                    }
                    return obj;
                }
                case YamlSequenceNode seq:
                {
                    var arr = new JsonArray();
                    foreach (var child in seq.Children) arr.Add(ConvertYamlNodeToJsonNode(child));
                    return arr;
                }
                case YamlScalarNode scalar:
                    return ConvertYamlScalar(scalar);
                default:
                    return null;
            }
        }

        private static JsonNode? ConvertYamlScalar(YamlScalarNode scalar)
        {
            var s = scalar.Value;
            // Quoted scalars are always strings (preserves authored intent like `"0"`).
            if (scalar.Style == ScalarStyle.SingleQuoted ||
                scalar.Style == ScalarStyle.DoubleQuoted ||
                scalar.Style == ScalarStyle.Literal ||
                scalar.Style == ScalarStyle.Folded)
            {
                return JsonValue.Create(s ?? string.Empty);
            }
            // Plain scalars: apply YAML 1.2 core schema inference.
            if (s is null) return null;
            if (s.Length == 0 || s == "~" || s == "null" || s == "Null" || s == "NULL")
                return null;
            if (s == "true" || s == "True" || s == "TRUE") return JsonValue.Create(true);
            if (s == "false" || s == "False" || s == "FALSE") return JsonValue.Create(false);
            if (LooksLikeInteger(s) && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                return JsonValue.Create(i);
            if (LooksLikeFloat(s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return JsonValue.Create(d);
            return JsonValue.Create(s);
        }

        private static bool LooksLikeInteger(string s)
        {
            if (s.Length == 0) return false;
            int start = (s[0] == '+' || s[0] == '-') ? 1 : 0;
            if (start == s.Length) return false;
            // Reject leading zeros for multi-digit integers (YAML core schema treats
            // "010" as a string, not an int).
            if (s.Length - start > 1 && s[start] == '0') return false;
            for (int i = start; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        private static bool LooksLikeFloat(string s)
        {
            if (s.Length == 0) return false;
            bool seenDigit = false, seenDotOrExp = false;
            int i = (s[0] == '+' || s[0] == '-') ? 1 : 0;
            for (; i < s.Length; i++)
            {
                var c = s[i];
                if (c >= '0' && c <= '9') seenDigit = true;
                else if (c == '.' || c == 'e' || c == 'E') seenDotOrExp = true;
                else if (c == '+' || c == '-') { /* exponent sign — allow */ }
                else return false;
            }
            return seenDigit && seenDotOrExp;
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
