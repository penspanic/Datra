using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
