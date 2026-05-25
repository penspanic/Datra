using System;
using System.Collections.Generic;
using Datra.Attributes;

namespace Datra.Bundles
{
    /// <summary>
    /// Portable text-data bundle for Datra runtime loading.
    /// The bundle keeps Datra's file-oriented model intact while allowing
    /// web clients to fetch one payload and serve all files from memory.
    /// </summary>
    public sealed class DatraRawBundle
    {
        /// <summary>
        /// Schema version 2 added <see cref="FormatOverrides"/> for in-flight
        /// YAML→JSON normalization (Datra v2 wasm trim path). v1 bundles are
        /// still accepted — provider treats them as having no format overrides.
        /// </summary>
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>
        /// SHA-256 hash over normalized file paths and contents.
        /// Does not include <see cref="CreatedAtUtc"/>.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public Dictionary<string, string> Files { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Per-file format override. When present, the runtime uses the override
        /// instead of inferring from the file extension. Used by
        /// <see cref="DatraBundleBuilder.NormalizeToJson"/> to mark YAML files
        /// whose content has been converted to JSON without changing the path.
        /// </summary>
        public Dictionary<string, DataFormat> FormatOverrides { get; set; } =
            new Dictionary<string, DataFormat>(StringComparer.Ordinal);
    }
}
