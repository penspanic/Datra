#nullable enable
using System;
using Newtonsoft.Json;

namespace Datra.DataTypes
{
    /// <summary>
    /// Stable identifier for asset data.
    /// Uses GUID internally for stability across file renames/moves.
    /// </summary>
    [JsonConverter(typeof(AssetIdJsonConverter))]
    public readonly struct AssetId : IEquatable<AssetId>
    {
        /// <summary>
        /// The underlying GUID value
        /// </summary>
        public Guid Value { get; }

        /// <summary>
        /// Whether this AssetId has a valid (non-empty) value
        /// </summary>
        [JsonIgnore]
        public bool IsValid => Value != Guid.Empty;

        public AssetId(Guid value)
        {
            Value = value;
        }

        public AssetId(string guidString)
        {
            Value = Guid.TryParse(guidString, out var guid) ? guid : Guid.Empty;
        }

        /// <summary>
        /// Creates a new unique AssetId
        /// </summary>
        public static AssetId NewId() => new AssetId(Guid.NewGuid());

        /// <summary>
        /// Empty/invalid AssetId
        /// </summary>
        public static AssetId Empty => new AssetId(Guid.Empty);

        /// <summary>
        /// Parse from string (GUID format)
        /// </summary>
        public static AssetId Parse(string value) => new AssetId(value);

        /// <summary>
        /// Try to parse from string
        /// </summary>
        public static bool TryParse(string? value, out AssetId result)
        {
            if (string.IsNullOrEmpty(value))
            {
                result = Empty;
                return false;
            }

            if (Guid.TryParse(value, out var guid))
            {
                result = new AssetId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public override string ToString() => Value.ToString("N"); // 32 hex digits, no dashes

        public override int GetHashCode() => Value.GetHashCode();

        public override bool Equals(object? obj) => obj is AssetId other && Equals(other);

        public bool Equals(AssetId other) => Value.Equals(other.Value);

        public static bool operator ==(AssetId left, AssetId right) => left.Equals(right);

        public static bool operator !=(AssetId left, AssetId right) => !left.Equals(right);

        public static implicit operator string(AssetId id) => id.ToString();
    }

    /// <summary>
    /// JSON converter for AssetId (serializes as string)
    /// </summary>
    public class AssetIdJsonConverter : JsonConverter<AssetId>
    {
        public override AssetId ReadJson(JsonReader reader, Type objectType, AssetId existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var value = reader.Value?.ToString();
            return AssetId.TryParse(value, out var id) ? id : AssetId.Empty;
        }

        public override void WriteJson(JsonWriter writer, AssetId value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }
}
