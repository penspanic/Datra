#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datra.DataTypes;

namespace Datra.Converters
{
    /// <summary>
    /// Factory that produces <see cref="JsonConverter{T}"/> instances for
    /// <see cref="IntDataRef{T}"/> and <see cref="StringDataRef{T}"/>.
    /// JSON form: the raw key value (int or string), never an object.
    /// </summary>
    public sealed class DataRefSystemTextJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType) return false;
            var def = typeToConvert.GetGenericTypeDefinition();
            return def == typeof(IntDataRef<>) || def == typeof(StringDataRef<>);
        }

#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2055",
            Justification = "Closed generic argument is sourced from the property type, which the trimmer keeps when DataRef is reachable.")]
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "Same as IL2055; AOT keeps closed DataRef<T> via reachable property metadata.")]
#endif
        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var def = typeToConvert.GetGenericTypeDefinition();
            var dataType = typeToConvert.GetGenericArguments()[0];
            var converterType = def == typeof(IntDataRef<>)
                ? typeof(IntDataRefConverter<>).MakeGenericType(dataType)
                : typeof(StringDataRefConverter<>).MakeGenericType(dataType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }

    internal sealed class IntDataRefConverter<T> : JsonConverter<IntDataRef<T>>
        where T : class, Datra.Interfaces.ITableData<int>
    {
        public override IntDataRef<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return default;
            if (reader.TokenType == JsonTokenType.Number) return new IntDataRef<T>(reader.GetInt32());
            if (reader.TokenType == JsonTokenType.String)
            {
                // Permissive: some pipelines serialize numbers as strings.
                var s = reader.GetString();
                return string.IsNullOrEmpty(s) ? default : new IntDataRef<T>(int.Parse(s));
            }
            throw new JsonException($"Unexpected token {reader.TokenType} while reading IntDataRef<{typeof(T).Name}>.");
        }

        public override void Write(Utf8JsonWriter writer, IntDataRef<T> value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.Value);
        }
    }

    internal sealed class StringDataRefConverter<T> : JsonConverter<StringDataRef<T>>
        where T : class, Datra.Interfaces.ITableData<string>
    {
        public override StringDataRef<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return default;
            if (reader.TokenType == JsonTokenType.String) return new StringDataRef<T>(reader.GetString() ?? string.Empty);
            throw new JsonException($"Unexpected token {reader.TokenType} while reading StringDataRef<{typeof(T).Name}>.");
        }

        public override void Write(Utf8JsonWriter writer, StringDataRef<T> value, JsonSerializerOptions options)
        {
            if (value.Value == null) writer.WriteNullValue();
            else writer.WriteStringValue(value.Value);
        }
    }
}
