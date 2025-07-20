using System;
using Newtonsoft.Json;
using Datra.Data.DataTypes;
using Datra.Data.Interfaces;

namespace Datra.Data.Converters
{
    /// <summary>
    /// JSON converter for StringDataRef types
    /// </summary>
    public class StringDataRefJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsGenericType && 
                   objectType.GetGenericTypeDefinition() == typeof(StringDataRef<>);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Activator.CreateInstance(objectType);

            if (reader.TokenType != JsonToken.String)
                throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing StringDataRef.");

            var value = (string)reader.Value;
            var instance = Activator.CreateInstance(objectType);
            
            // Set the Value property
            var valueProperty = objectType.GetProperty("Value");
            valueProperty.SetValue(instance, value);
            
            return instance;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var valueProperty = value.GetType().GetProperty("Value");
            var stringValue = valueProperty.GetValue(value) as string;
            
            writer.WriteValue(stringValue ?? string.Empty);
        }
    }
}