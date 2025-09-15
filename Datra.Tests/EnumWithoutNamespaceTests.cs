using System.Collections.Generic;
using System.Linq;
using Datra.SampleData.Models;
using Xunit;

namespace Datra.Tests
{
    public class EnumWithoutNamespaceTests
    {
        [Fact]
        public void Should_Parse_Enum_Without_Namespace()
        {
            // Arrange
            var csvData = @"Id,Name,Quality,AllowedQualities,StatType,StatTypes
1,Test Item 1,Common,Common|Rare,Attack,Attack|Defense
2,Test Item 2,Legendary,Epic|Legendary,Defense,HealthRegen|Speed";

            // Act
            var result = EnumTestDataSerializer.DeserializeCsv(csvData);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            // Test first item
            var item1 = result[1];
            Assert.Equal(1, item1.Id);
            Assert.Equal("Test Item 1", item1.Name);
            Assert.Equal(QualityType.Common, item1.Quality);
            Assert.Equal(2, item1.AllowedQualities.Length);
            Assert.Equal(QualityType.Common, item1.AllowedQualities[0]);
            Assert.Equal(QualityType.Rare, item1.AllowedQualities[1]);
            Assert.Equal(StatType.Attack, item1.StatType);
            Assert.Equal(2, item1.StatTypes.Length);
            Assert.Equal(StatType.Attack, item1.StatTypes[0]);
            Assert.Equal(StatType.Defense, item1.StatTypes[1]);

            // Test second item
            var item2 = result[2];
            Assert.Equal(2, item2.Id);
            Assert.Equal("Test Item 2", item2.Name);
            Assert.Equal(QualityType.Legendary, item2.Quality);
            Assert.Equal(2, item2.AllowedQualities.Length);
            Assert.Equal(QualityType.Epic, item2.AllowedQualities[0]);
            Assert.Equal(QualityType.Legendary, item2.AllowedQualities[1]);
            Assert.Equal(StatType.Defense, item2.StatType);
            Assert.Equal(2, item2.StatTypes.Length);
            Assert.Equal(StatType.HealthRegen, item2.StatTypes[0]);
            Assert.Equal(StatType.Speed, item2.StatTypes[1]);
        }

        [Fact]
        public void Should_Serialize_Enum_Without_Namespace()
        {
            // Arrange
            var data = new Dictionary<int, EnumTestData>
            {
                {
                    1,
                    new EnumTestData(
                        id: 1,
                        name: "Test Item",
                        quality: QualityType.Epic,
                        allowedQualities: new[] { QualityType.Common, QualityType.Rare, QualityType.Epic },
                        statType: StatType.Attack,
                        statTypes: new[] { StatType.Attack, StatType.Defense }
                    )
                }
            };

            // Act
            var csvResult = EnumTestDataSerializer.SerializeCsv(data);

            // Assert
            Assert.NotNull(csvResult);
            var lines = csvResult.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.Equal(2, lines.Length); // Header + 1 data row

            // Check header
            Assert.Contains("Quality", lines[0]);
            Assert.Contains("AllowedQualities", lines[0]);

            // Check data
            Assert.Contains("Epic", lines[1]);
            Assert.Contains("Common|Rare|Epic", lines[1]);
            Assert.Contains("Attack", lines[1]);
            Assert.Contains("Attack|Defense", lines[1]);
        }

        [Fact]
        public void Should_Handle_Empty_Enum_Arrays()
        {
            // Arrange
            var csvData = @"Id,Name,Quality,AllowedQualities,StatType,StatTypes
1,Test Item,Common,,Attack,";

            // Act
            var result = EnumTestDataSerializer.DeserializeCsv(csvData);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);

            var item = result[1];
            Assert.Equal(QualityType.Common, item.Quality);
            Assert.Empty(item.AllowedQualities);
            Assert.Equal(StatType.Attack, item.StatType);
            Assert.Empty(item.StatTypes);
        }
    }
}