#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Datra.Lab
{
    /// <summary>
    /// Read side of the physics cache: for a <see cref="PhysicsKey"/>, the pool of outcomes a
    /// physics batch produced earlier. The economy layer draws from the pool instead of running
    /// physics. Implementations are read from several threads at once.
    /// </summary>
    /// <remarks>
    /// P0 only reads. Noticing a missing key, queueing a batch and filling the cache are not
    /// part of this contract yet.
    /// </remarks>
    public interface IOutcomeReader
    {
        /// <summary>Engine pin + recipe hash: what the stored outcomes were computed with.</summary>
        string Version { get; }

        /// <summary>The stored outcomes for <paramref name="key"/>, or null when none are cached.</summary>
        IReadOnlyList<T>? Pool<T>(PhysicsKey key);
    }

    /// <summary>Thrown by <see cref="SimRun.Outcome{T}"/> when the cache has nothing for a key.</summary>
    public sealed class MissingOutcomeException : Exception
    {
        public MissingOutcomeException(PhysicsKey key)
            : base($"No stored physics outcomes for '{key.Text}'.")
        {
            Key = key;
        }

        public PhysicsKey Key { get; }
    }

    /// <summary>Outcome pools held in memory. For tests, samples and small hand-made tables.</summary>
    public sealed class InMemoryOutcomes : IOutcomeReader
    {
        private readonly ConcurrentDictionary<string, object> _pools =
            new ConcurrentDictionary<string, object>(StringComparer.Ordinal);

        public InMemoryOutcomes(string version = "memory")
        {
            Version = version;
        }

        public string Version { get; }

        public int Count => _pools.Count;

        public InMemoryOutcomes Add<T>(PhysicsKey key, IEnumerable<T> samples)
        {
            _pools[key.Text] = samples.ToList();
            return this;
        }

        public IReadOnlyList<T>? Pool<T>(PhysicsKey key) =>
            _pools.TryGetValue(key.Text, out var pool) ? pool as IReadOnlyList<T> : null;
    }

    /// <summary>
    /// Reads outcome pools from a directory of JSON Lines files: one file per key, named
    /// <c>{PhysicsKey.Hash(version)}.jsonl</c>, one outcome per line. Files are parsed on first
    /// use and kept.
    /// </summary>
    public sealed class JsonlOutcomeReader : IOutcomeReader
    {
        private readonly string _directory;
        private readonly JsonSerializerOptions _json;
        private readonly ConcurrentDictionary<string, object?> _pools =
            new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);

        public JsonlOutcomeReader(string directory, string version, JsonSerializerOptions? json = null)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            Version = version ?? string.Empty;
            _json = json ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public string Version { get; }

        /// <summary>The file a key's outcomes are read from.</summary>
        public string PathFor(PhysicsKey key) => Path.Combine(_directory, key.Hash(Version) + ".jsonl");

        public IReadOnlyList<T>? Pool<T>(PhysicsKey key)
        {
            var cacheKey = typeof(T).FullName + "|" + key.Text;
            return _pools.GetOrAdd(cacheKey, _ => Load<T>(key)) as IReadOnlyList<T>;
        }

        private object? Load<T>(PhysicsKey key)
        {
            var path = PathFor(key);
            if (!File.Exists(path)) return null;

            var pool = new List<T>();
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var sample = JsonSerializer.Deserialize<T>(line, _json);
                if (sample != null) pool.Add(sample);
            }
            return pool.Count == 0 ? null : pool;
        }
    }
}
