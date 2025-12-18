#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datra.DataTypes;

namespace Datra.Localization
{
    /// <summary>
    /// Represents a nested locale path template that can be evaluated with runtime indices.
    /// Used for hierarchical data structures like Graph/Node/Choice where the final key
    /// depends on array indices determined at runtime.
    ///
    /// Example:
    ///   Template: "Nodes.Choices.Name"
    ///   Evaluated: "Graph.file001.Nodes#3.Choices#1.Name"
    /// </summary>
    public readonly struct NestedLocaleRef : IEquatable<NestedLocaleRef>
    {
        // Cache for evaluated keys to avoid repeated string allocations
        [ThreadStatic]
        private static Dictionary<int, string>? _cache;

        private static Dictionary<int, string> Cache => _cache ??= new Dictionary<int, string>();

        /// <summary>
        /// The path template with dot-separated segments (e.g., "Nodes.Choices.Name")
        /// </summary>
        public string PathTemplate { get; }

        /// <summary>
        /// The individual path segments
        /// </summary>
        public string[] Segments { get; }

        /// <summary>
        /// Indicates whether this reference has a valid path
        /// </summary>
        public bool HasValue => !string.IsNullOrEmpty(PathTemplate);

        private NestedLocaleRef(string pathTemplate, string[] segments)
        {
            PathTemplate = pathTemplate;
            Segments = segments;
        }

        /// <summary>
        /// Creates a nested locale reference from path segments.
        /// </summary>
        /// <param name="segments">The path segments (e.g., "Nodes", "Choices", "Name")</param>
        /// <returns>A new NestedLocaleRef</returns>
        public static NestedLocaleRef Create(params string[] segments)
        {
            if (segments == null || segments.Length == 0)
                return new NestedLocaleRef(string.Empty, Array.Empty<string>());

            var pathTemplate = string.Join(".", segments);
            return new NestedLocaleRef(pathTemplate, segments);
        }

        /// <summary>
        /// Clears the evaluation cache. Call this when memory pressure is high
        /// or when locale data is reloaded.
        /// </summary>
        public static void ClearCache()
        {
            _cache?.Clear();
        }

        /// <summary>
        /// Evaluates the nested locale to a concrete LocaleRef by inserting indices.
        /// Uses caching to avoid repeated string allocations for the same key.
        /// </summary>
        /// <param name="prefix">The prefix (e.g., "Graph.file001")</param>
        /// <param name="indices">Array of (segmentName, index) pairs for segments that need indices</param>
        /// <returns>A LocaleRef with the fully resolved key</returns>
        /// <example>
        /// var nested = NestedLocaleRef.Create("Nodes", "Choices", "Name");
        /// var localeRef = nested.Evaluate("Graph.file001", ("Nodes", 3), ("Choices", 1));
        /// // Result: LocaleRef with key "Graph.file001.Nodes#3.Choices#1.Name"
        /// </example>
        public LocaleRef Evaluate(string prefix, params (string segmentName, int index)[] indices)
        {
            if (!HasValue)
                return new LocaleRef { Key = prefix };

            // Compute cache key
            var cacheKey = ComputeCacheKey(prefix, indices);

            if (Cache.TryGetValue(cacheKey, out var cachedKey))
                return new LocaleRef { Key = cachedKey };

            // Build the key using optimized method
            var key = BuildKey(prefix, indices);
            Cache[cacheKey] = key;

            return new LocaleRef { Key = key };
        }

        /// <summary>
        /// Evaluates without caching. Use this for one-off evaluations
        /// where caching overhead is not desired.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LocaleRef EvaluateNoCache(string prefix, params (string segmentName, int index)[] indices)
        {
            if (!HasValue)
                return new LocaleRef { Key = prefix };

            return new LocaleRef { Key = BuildKey(prefix, indices) };
        }

        /// <summary>
        /// Fast evaluation with a single index. Avoids array allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LocaleRef Evaluate(string prefix, string segmentName, int index)
        {
            if (!HasValue)
                return new LocaleRef { Key = prefix };

            // Use simpler cache key for single index case
            var cacheKey = HashCode.Combine(
                PathTemplate?.GetHashCode() ?? 0,
                prefix?.GetHashCode() ?? 0,
                segmentName.GetHashCode(),
                index
            );

            if (Cache.TryGetValue(cacheKey, out var cachedKey))
                return new LocaleRef { Key = cachedKey };

            var key = BuildKeySingle(prefix ?? string.Empty, segmentName, index);
            Cache[cacheKey] = key;

            return new LocaleRef { Key = key };
        }

        /// <summary>
        /// Fast evaluation with two indices. Avoids array allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LocaleRef Evaluate(string prefix, string segment1, int index1, string segment2, int index2)
        {
            if (!HasValue)
                return new LocaleRef { Key = prefix };

            var cacheKey = HashCode.Combine(
                PathTemplate?.GetHashCode() ?? 0,
                prefix?.GetHashCode() ?? 0,
                segment1.GetHashCode(),
                index1,
                segment2.GetHashCode(),
                index2
            );

            if (Cache.TryGetValue(cacheKey, out var cachedKey))
                return new LocaleRef { Key = cachedKey };

            var key = BuildKeyDouble(prefix ?? string.Empty, segment1, index1, segment2, index2);
            Cache[cacheKey] = key;

            return new LocaleRef { Key = key };
        }

        private string BuildKey(string prefix, (string segmentName, int index)[] indices)
        {
            // Calculate total length needed
            int totalLength = 0;

            if (!string.IsNullOrEmpty(prefix))
                totalLength = prefix.Length + 1; // +1 for dot

            for (int i = 0; i < Segments.Length; i++)
            {
                if (i > 0)
                    totalLength++; // dot separator

                var segment = Segments[i];
                totalLength += segment.Length;

                // Check if this segment needs an index
                foreach (var (segmentName, index) in indices)
                {
                    if (string.Equals(segment, segmentName, StringComparison.Ordinal))
                    {
                        totalLength++; // # character
                        totalLength += CountDigits(index);
                        break;
                    }
                }
            }

            // Use string.Create for efficient allocation
            return string.Create(totalLength, (prefix, Segments, indices), static (span, state) =>
            {
                var (prefix, segments, indices) = state;
                int pos = 0;

                if (!string.IsNullOrEmpty(prefix))
                {
                    prefix.AsSpan().CopyTo(span.Slice(pos));
                    pos += prefix.Length;
                    span[pos++] = '.';
                }

                for (int i = 0; i < segments.Length; i++)
                {
                    if (i > 0)
                        span[pos++] = '.';

                    var segment = segments[i];
                    segment.AsSpan().CopyTo(span.Slice(pos));
                    pos += segment.Length;

                    foreach (var (segmentName, index) in indices)
                    {
                        if (string.Equals(segment, segmentName, StringComparison.Ordinal))
                        {
                            span[pos++] = '#';
                            pos += WriteInt(span.Slice(pos), index);
                            break;
                        }
                    }
                }
            });
        }

        private string BuildKeySingle(string prefix, string segmentName, int index)
        {
            // Calculate total length
            int totalLength = 0;

            if (!string.IsNullOrEmpty(prefix))
                totalLength = prefix.Length + 1;

            for (int i = 0; i < Segments.Length; i++)
            {
                if (i > 0) totalLength++;
                var segment = Segments[i];
                totalLength += segment.Length;

                if (string.Equals(segment, segmentName, StringComparison.Ordinal))
                {
                    totalLength++;
                    totalLength += CountDigits(index);
                }
            }

            return string.Create(totalLength, (prefix, Segments, segmentName, index), static (span, state) =>
            {
                var (prefix, segments, segmentName, index) = state;
                int pos = 0;

                if (!string.IsNullOrEmpty(prefix))
                {
                    prefix.AsSpan().CopyTo(span.Slice(pos));
                    pos += prefix.Length;
                    span[pos++] = '.';
                }

                for (int i = 0; i < segments.Length; i++)
                {
                    if (i > 0) span[pos++] = '.';

                    var segment = segments[i];
                    segment.AsSpan().CopyTo(span.Slice(pos));
                    pos += segment.Length;

                    if (string.Equals(segment, segmentName, StringComparison.Ordinal))
                    {
                        span[pos++] = '#';
                        pos += WriteInt(span.Slice(pos), index);
                    }
                }
            });
        }

        private string BuildKeyDouble(string prefix, string seg1, int idx1, string seg2, int idx2)
        {
            int totalLength = 0;

            if (!string.IsNullOrEmpty(prefix))
                totalLength = prefix.Length + 1;

            for (int i = 0; i < Segments.Length; i++)
            {
                if (i > 0) totalLength++;
                var segment = Segments[i];
                totalLength += segment.Length;

                if (string.Equals(segment, seg1, StringComparison.Ordinal))
                {
                    totalLength++;
                    totalLength += CountDigits(idx1);
                }
                else if (string.Equals(segment, seg2, StringComparison.Ordinal))
                {
                    totalLength++;
                    totalLength += CountDigits(idx2);
                }
            }

            return string.Create(totalLength, (prefix, Segments, seg1, idx1, seg2, idx2), static (span, state) =>
            {
                var (prefix, segments, seg1, idx1, seg2, idx2) = state;
                int pos = 0;

                if (!string.IsNullOrEmpty(prefix))
                {
                    prefix.AsSpan().CopyTo(span.Slice(pos));
                    pos += prefix.Length;
                    span[pos++] = '.';
                }

                for (int i = 0; i < segments.Length; i++)
                {
                    if (i > 0) span[pos++] = '.';

                    var segment = segments[i];
                    segment.AsSpan().CopyTo(span.Slice(pos));
                    pos += segment.Length;

                    if (string.Equals(segment, seg1, StringComparison.Ordinal))
                    {
                        span[pos++] = '#';
                        pos += WriteInt(span.Slice(pos), idx1);
                    }
                    else if (string.Equals(segment, seg2, StringComparison.Ordinal))
                    {
                        span[pos++] = '#';
                        pos += WriteInt(span.Slice(pos), idx2);
                    }
                }
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountDigits(int value)
        {
            if (value < 0) value = -value;
            if (value < 10) return 1;
            if (value < 100) return 2;
            if (value < 1000) return 3;
            if (value < 10000) return 4;
            if (value < 100000) return 5;
            if (value < 1000000) return 6;
            if (value < 10000000) return 7;
            if (value < 100000000) return 8;
            if (value < 1000000000) return 9;
            return 10;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteInt(Span<char> span, int value)
        {
            // Fast path for common small indices
            if (value >= 0 && value < 10)
            {
                span[0] = (char)('0' + value);
                return 1;
            }

            return value.TryFormat(span, out int charsWritten) ? charsWritten : 0;
        }

        private int ComputeCacheKey(string prefix, (string segmentName, int index)[] indices)
        {
            var hash = new HashCode();
            hash.Add(PathTemplate);
            hash.Add(prefix);

            foreach (var (segmentName, index) in indices)
            {
                hash.Add(segmentName);
                hash.Add(index);
            }

            return hash.ToHashCode();
        }

        /// <summary>
        /// Evaluates using an ILocaleEvaluator for complex resolution logic.
        /// </summary>
        /// <param name="evaluator">The evaluator that knows how to resolve indices</param>
        /// <param name="rootId">The root identifier (e.g., FileId)</param>
        /// <param name="context">Context objects used to determine indices</param>
        /// <returns>A LocaleRef with the fully resolved key</returns>
        public LocaleRef Evaluate(ILocaleEvaluator evaluator, object rootId, params object[] context)
        {
            if (evaluator == null)
                throw new ArgumentNullException(nameof(evaluator));

            return evaluator.EvaluateNestedLocale(rootId, this, context);
        }

        /// <summary>
        /// Gets the final segment (typically the property name).
        /// </summary>
        public string PropertyName => Segments.Length > 0 ? Segments[Segments.Length - 1] : string.Empty;

        /// <summary>
        /// Gets the number of segments in the path.
        /// </summary>
        public int Depth => Segments.Length;

        public override string ToString() => PathTemplate ?? string.Empty;

        public override bool Equals(object? obj) => obj is NestedLocaleRef other && Equals(other);

        public bool Equals(NestedLocaleRef other) =>
            string.Equals(PathTemplate, other.PathTemplate, StringComparison.Ordinal);

        public override int GetHashCode() => PathTemplate?.GetHashCode() ?? 0;

        public static bool operator ==(NestedLocaleRef left, NestedLocaleRef right) => left.Equals(right);

        public static bool operator !=(NestedLocaleRef left, NestedLocaleRef right) => !left.Equals(right);
    }
}
