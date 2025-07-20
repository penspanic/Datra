using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Datra.DataTypes;

namespace Datra.Converters
{
    /// <summary>
    /// Generic YAML converter for DataRef types
    /// </summary>
    public class DataRefYamlConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            if (!type.IsGenericType)
                return false;
                
            var genericTypeDef = type.GetGenericTypeDefinition();
            return genericTypeDef == typeof(StringDataRef<>) || 
                   genericTypeDef == typeof(IntDataRef<>);
        }

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            if (parser.TryConsume<Scalar>(out var scalar))
            {
                var instance = Activator.CreateInstance(type);
                var valueProperty = type.GetProperty("Value");
                
                if (valueProperty == null)
                    throw new YamlException($"Type {type} does not have a Value property.");
                
                // Handle different value types
                if (valueProperty.PropertyType == typeof(string))
                {
                    valueProperty.SetValue(instance, scalar.Value);
                }
                else if (valueProperty.PropertyType == typeof(int))
                {
                    if (int.TryParse(scalar.Value, out var intValue))
                        valueProperty.SetValue(instance, intValue);
                    else
                        throw new YamlException($"Cannot parse '{scalar.Value}' as integer for DataRef.");
                }
                else
                {
                    // For other types, try to convert
                    var convertedValue = Convert.ChangeType(scalar.Value, valueProperty.PropertyType);
                    valueProperty.SetValue(instance, convertedValue);
                }
                
                return instance;
            }
            
            throw new YamlException($"Expected scalar value for DataRef type {type}.");
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            if (value == null)
            {
                emitter.Emit(new Scalar(null, null, string.Empty, ScalarStyle.Plain, false, false));
                return;
            }

            var dataRef = (IDataRef)value;
            var keyValue = dataRef.GetKeyValue();
            
            if (keyValue == null)
                emitter.Emit(new Scalar(null, null, string.Empty, ScalarStyle.Plain, false, false));
            else
                emitter.Emit(new Scalar(null, null, keyValue.ToString() ?? string.Empty, ScalarStyle.Plain, false, false));
        }
    }
}