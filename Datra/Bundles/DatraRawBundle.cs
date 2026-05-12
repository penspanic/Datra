using System;
using System.Collections.Generic;

namespace Datra.Bundles
{
    /// <summary>
    /// Portable text-data bundle for Datra runtime loading.
    /// The bundle keeps Datra's file-oriented model intact while allowing
    /// web clients to fetch one payload and serve all files from memory.
    /// </summary>
    public sealed class DatraRawBundle
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>
        /// SHA-256 hash over normalized file paths and contents.
        /// Does not include <see cref="CreatedAtUtc"/>.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public Dictionary<string, string> Files { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
