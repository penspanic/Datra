using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Datra.Data.DataTypes;

namespace Datra.Data.Converters
{
    /// <summary>
    /// YAML type converter for StringDataRef types
    /// </summary>
    public class StringDataRefYamlConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return type.IsGenericType && 
                   type.GetGenericTypeDefinition() == typeof(StringDataRef<>);
        }

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            var value = parser.Consume<Scalar>().Value;
            var instance = Activator.CreateInstance(type);
            
            // Set the Value property
            var valueProperty = type.GetProperty("Value");
            valueProperty.SetValue(instance, value);
            
            return instance;
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            if (value == null)
            {
                emitter.Emit(new Scalar(null, null, string.Empty, ScalarStyle.Plain, true, false));
                return;
            }

            var valueProperty = value.GetType().GetProperty("Value");
            var stringValue = valueProperty.GetValue(value) as string ?? string.Empty;
            
            emitter.Emit(new Scalar(null, null, stringValue, ScalarStyle.Plain, true, false));
        }
    }
}