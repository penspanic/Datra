using System.Collections.Generic;
using System.Linq;
using Xunit;
using Datra.Generators.Models;

namespace Datra.Tests
{
    public class DataModelInfoTests
    {
        [Fact]
        public void GetConstructorProperties_ExcludesFixedLocale()
        {
            // Arrange
            var model = new DataModelInfo
            {
                Properties = new List<PropertyInfo>
                {
                    new PropertyInfo { Name = "Id", Type = "int" },
                    new PropertyInfo { Name = "Name", Type = "string" },
                    new PropertyInfo { Name = "LocalizedName", Type = "LocaleRef", IsFixedLocale = true }
                }
            };

            // Act
            var constructorProps = model.GetConstructorProperties().ToList();

            // Assert
            Assert.Equal(2, constructorProps.Count);
            Assert.Contains(constructorProps, p => p.Name == "Id");
            Assert.Contains(constructorProps, p => p.Name == "Name");
            Assert.DoesNotContain(constructorProps, p => p.Name == "LocalizedName");
        }

        [Fact]
        public void GetConstructorProperties_ExcludesRefProperty()
        {
            // Arrange - simulates scenario where physical file's Ref property is detected
            var model = new DataModelInfo
            {
                Properties = new List<PropertyInfo>
                {
                    new PropertyInfo { Name = "Id", Type = "int" },
                    new PropertyInfo { Name = "Name", Type = "string" },
                    new PropertyInfo { Name = "Ref", Type = "IntDataRef<TestData>", IsDataRef = true }
                }
            };

            // Act
            var constructorProps = model.GetConstructorProperties().ToList();

            // Assert
            Assert.Equal(2, constructorProps.Count);
            Assert.Contains(constructorProps, p => p.Name == "Id");
            Assert.Contains(constructorProps, p => p.Name == "Name");
            Assert.DoesNotContain(constructorProps, p => p.Name == "Ref");
        }

        [Fact]
        public void GetConstructorProperties_IncludesRegularDataRef()
        {
            // Arrange - regular DataRef properties (not named "Ref") should be included
            var model = new DataModelInfo
            {
                Properties = new List<PropertyInfo>
                {
                    new PropertyInfo { Name = "Id", Type = "int" },
                    new PropertyInfo { Name = "ItemRef", Type = "IntDataRef<ItemData>", IsDataRef = true },
                    new PropertyInfo { Name = "CharacterRef", Type = "StringDataRef<CharacterData>", IsDataRef = true }
                }
            };

            // Act
            var constructorProps = model.GetConstructorProperties().ToList();

            // Assert
            Assert.Equal(3, constructorProps.Count);
            Assert.Contains(constructorProps, p => p.Name == "Id");
            Assert.Contains(constructorProps, p => p.Name == "ItemRef");
            Assert.Contains(constructorProps, p => p.Name == "CharacterRef");
        }

        [Fact]
        public void GetConstructorProperties_ExcludesBothRefAndFixedLocale()
        {
            // Arrange
            var model = new DataModelInfo
            {
                Properties = new List<PropertyInfo>
                {
                    new PropertyInfo { Name = "Id", Type = "int" },
                    new PropertyInfo { Name = "Name", Type = "string" },
                    new PropertyInfo { Name = "Ref", Type = "IntDataRef<TestData>", IsDataRef = true },
                    new PropertyInfo { Name = "LocalizedName", Type = "LocaleRef", IsFixedLocale = true }
                }
            };

            // Act
            var constructorProps = model.GetConstructorProperties().ToList();

            // Assert
            Assert.Equal(2, constructorProps.Count);
            Assert.Contains(constructorProps, p => p.Name == "Id");
            Assert.Contains(constructorProps, p => p.Name == "Name");
        }

        [Fact]
        public void GetSerializableProperties_IncludesRefProperty()
        {
            // Arrange - GetSerializableProperties should still include Ref for serialization purposes
            var model = new DataModelInfo
            {
                Properties = new List<PropertyInfo>
                {
                    new PropertyInfo { Name = "Id", Type = "int" },
                    new PropertyInfo { Name = "Ref", Type = "IntDataRef<TestData>", IsDataRef = true }
                }
            };

            // Act
            var serializableProps = model.GetSerializableProperties().ToList();

            // Assert - Ref is excluded from serialization too (FixedLocale check, but Ref is not serialized)
            // Actually, GetSerializableProperties excludes FixedLocale, not Ref
            // Ref should be included in serializable but excluded from constructor
            Assert.Equal(2, serializableProps.Count);
        }
    }
}
