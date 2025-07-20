using System;
using Newtonsoft.Json;
using Datra.Data.DataTypes;

namespace Datra.Data.Converters
{
    /// <summary>
    /// Generic JSON converter for DataRef types
    /// </summary>
    public class DataRefJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (!objectType.IsGenericType)
                return false;
                
            var genericTypeDef = objectType.GetGenericTypeDefinition();
            return genericTypeDef == typeof(StringDataRef<>) || 
                   genericTypeDef == typeof(IntDataRef<>);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Activator.CreateInstance(objectType);

            var instance = Activator.CreateInstance(objectType);
            var valueProperty = objectType.GetProperty("Value");
            
            if (valueProperty == null)
                throw new JsonSerializationException($"Type {objectType} does not have a Value property.");
            
            // Handle different value types
            if (valueProperty.PropertyType == typeof(string))
            {
                if (reader.TokenType != JsonToken.String)
                    throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing string DataRef.");
                valueProperty.SetValue(instance, (string)reader.Value);
            }
            else if (valueProperty.PropertyType == typeof(int))
            {
                if (reader.TokenType != JsonToken.Integer)
                    throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing int DataRef.");
                valueProperty.SetValue(instance, Convert.ToInt32(reader.Value));
            }
            else
            {
                // For other types, use the serializer to deserialize
                var value = serializer.Deserialize(reader, valueProperty.PropertyType);
                valueProperty.SetValue(instance, value);
            }
            
            return instance;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var dataRef = (IDataRef)value;
            var keyValue = dataRef.GetKeyValue();
            
            if (keyValue == null)
                writer.WriteNull();
            else
                writer.WriteValue(keyValue);
        }
    }
}