#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Datra.Lab
{
    /// <summary>
    /// A named, frozen scenario together with the result it gave at the time. The result is
    /// kept so "the picture we looked at" survives later changes to the data or the simulator.
    /// </summary>
    public sealed class LabSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }

        /// <summary>Hash of the baseline data the result was computed from.</summary>
        public string BaselineHash { get; set; } = string.Empty;

        public Scenario Scenario { get; set; } = new Scenario();
        public LabResult Result { get; set; } = new LabResult();
    }

    /// <summary>The listing entry for a snapshot: enough to pick one without loading its result.</summary>
    public sealed class SnapshotInfo
    {
        public string Name { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string BaselineHash { get; set; } = string.Empty;
        public int ChangedKnobs { get; set; }
    }

    public interface ISnapshotStore
    {
        Task<IReadOnlyList<SnapshotInfo>> ListAsync();
        Task<LabSnapshot?> LoadAsync(string name);
        Task SaveAsync(LabSnapshot snapshot);
    }

    /// <summary>
    /// Snapshots as <c>{name}.json</c> files in one directory. Plain indented JSON, meant to be
    /// committed next to the game's data.
    /// </summary>
    public sealed class FileSnapshotStore : ISnapshotStore
    {
        // Letters, digits, space and a few separators. No path characters, no leading dot.
        private static readonly Regex ValidName = new Regex(@"^[\p{L}\p{N}][\p{L}\p{N} ._+\-]{0,79}$", RegexOptions.CultureInvariant);

        private readonly string _directory;

        public FileSnapshotStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A directory is required.", nameof(directory));
            _directory = Path.GetFullPath(directory);
        }

        public string Directory => _directory;

        public static bool IsValidName(string? name) =>
            !string.IsNullOrEmpty(name) && ValidName.IsMatch(name) && !name!.Contains("..") && !name.EndsWith(".", StringComparison.Ordinal);

        public Task<IReadOnlyList<SnapshotInfo>> ListAsync()
        {
            var list = new List<SnapshotInfo>();
            if (System.IO.Directory.Exists(_directory))
            {
                foreach (var file in System.IO.Directory.GetFiles(_directory, "*.json"))
                {
                    try
                    {
                        var snapshot = LabJson.Deserialize<LabSnapshot>(File.ReadAllText(file));
                        if (snapshot is null) continue;
                        list.Add(new SnapshotInfo
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            CreatedAtUtc = snapshot.CreatedAtUtc,
                            BaselineHash = snapshot.BaselineHash,
                            ChangedKnobs = snapshot.Scenario?.Changes?.Count ?? 0,
                        });
                    }
                    catch (JsonException)
                    {
                        // Not a snapshot (or a hand-edit gone wrong): leave it out of the list.
                    }
                }
            }

            IReadOnlyList<SnapshotInfo> ordered = list
                .OrderByDescending(s => s.CreatedAtUtc)
                .ThenBy(s => s.Name, StringComparer.Ordinal)
                .ToList();
            return Task.FromResult(ordered);
        }

        public Task<LabSnapshot?> LoadAsync(string name)
        {
            var path = PathFor(name);
            if (!File.Exists(path)) return Task.FromResult<LabSnapshot?>(null);
            var snapshot = LabJson.Deserialize<LabSnapshot>(File.ReadAllText(path));
            if (snapshot != null) snapshot.Name = name;
            return Task.FromResult(snapshot);
        }

        public Task SaveAsync(LabSnapshot snapshot)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            var path = PathFor(snapshot.Name);
            System.IO.Directory.CreateDirectory(_directory);
            File.WriteAllText(path, LabJson.Serialize(snapshot, indented: true));
            return Task.CompletedTask;
        }

        private string PathFor(string name)
        {
            if (!IsValidName(name))
            {
                throw new ArgumentException(
                    "A snapshot name is 1-80 letters, digits, spaces or . _ + - and starts with a letter or digit.",
                    nameof(name));
            }
            return Path.Combine(_directory, name + ".json");
        }
    }
}
