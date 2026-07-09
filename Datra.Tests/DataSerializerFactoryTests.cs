using System.Reflection;
using Datra.Attributes;
using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
    public class DataSerializerFactoryTests
    {
        [Fact]
        public void JsonContextFactory_DoesNotCreateYamlSerializerForJsonFormat()
        {
            var factory = new DataSerializerFactory(TestJsonContext.Default);

            var serializer = factory.GetSerializer("Units.yaml", DataFormat.Json);

            Assert.IsType<SystemTextJsonDataSerializer>(serializer);
            Assert.Null(ReadYamlSerializer(factory));
        }

        [Fact]
        public void JsonContextFactory_CreatesYamlSerializerOnlyForYamlFormat()
        {
            var factory = new DataSerializerFactory(TestJsonContext.Default);

            var serializer = factory.GetSerializer("Units.yaml", DataFormat.Yaml);

            Assert.IsType<YamlDataSerializer>(serializer);
            Assert.Same(serializer, ReadYamlSerializer(factory));
        }

        private static object? ReadYamlSerializer(DataSerializerFactory factory)
        {
            var field = typeof(DataSerializerFactory).GetField(
                "_yamlSerializer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return field!.GetValue(factory);
        }
    }
}
