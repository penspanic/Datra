#nullable enable
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Datra.Lab
{
    /// <summary>
    /// The one JSON shape the lab speaks: camelCase properties, camelCase enum names, nulls
    /// left out. Endpoints, snapshot files and (later) exported pages all use it, so the view
    /// modules read the same document wherever it came from.
    /// </summary>
    public static class LabJson
    {
        public static JsonSerializerOptions Options { get; } = Create(indented: false);

        private static readonly JsonSerializerOptions Indented = Create(indented: true);

        public static string Serialize<T>(T value, bool indented = false) =>
            JsonSerializer.Serialize(value, indented ? Indented : Options);

        public static T? Deserialize<T>(string json) where T : class =>
            JsonSerializer.Deserialize<T>(json, Options);

        private static JsonSerializerOptions Create(bool indented)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = indented,
                // Snapshot files are read by people: keep non-ASCII labels as they were typed.
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return options;
        }
    }
}
