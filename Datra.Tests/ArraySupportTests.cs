using System;
using System.IO;
using System.Linq;
using Datra.DataTypes;
using Datra.Tests.Models;
using Xunit;

namespace Datra.Tests
{
    public class ArraySupportTests
    {
        private readonly GameDataContext _context;
        public ArraySupportTests()
        {
            _context = TestDataHelper.CreateGameDataContext();
            _context.LoadAllAsync().Wait();
        }
            
        [Fact]
        public void Should_ParseIntArray_FromCsv()
        {
            // Act
            var character = _context.Character.GetById("hero_001");
            
            // Assert
            Assert.NotNull(character);
            Assert.NotNull(character.UpgradeCosts);
            Assert.Equal(5, character.UpgradeCosts.Length);
            Assert.Equal(100, character.UpgradeCosts[0]);
            Assert.Equal(200, character.UpgradeCosts[1]);
            Assert.Equal(400, character.UpgradeCosts[2]);
            Assert.Equal(800, character.UpgradeCosts[3]);
            Assert.Equal(1600, character.UpgradeCosts[4]);
        }
        
        [Fact]
        public void Should_ParseIntDataRefArray_FromCsv()
        {
            // Act
            var refData = _context.RefTest.GetById("test_01");
            
            // Assert
            Assert.NotNull(refData);
            Assert.NotNull(refData.ItemRefs);
            Assert.Equal(3, refData.ItemRefs.Length);
            Assert.Equal(1001, refData.ItemRefs[0].Value);
            Assert.Equal(1002, refData.ItemRefs[1].Value);
            Assert.Equal(1003, refData.ItemRefs[2].Value);
        }
        
        [Fact]
        public void Should_HandleEmptyArray_FromCsv()
        {
            // Act
            var refData = _context.RefTest.GetById("test_05");
            
            // Assert
            Assert.NotNull(refData);
            Assert.NotNull(refData.ItemRefs);
            Assert.Equal(0, refData.ItemRefs.Length);
        }
        
        [Fact]
        public void Should_SerializeArrays_ToCsv()
        {
            // Arrange
            var newCharacter = new CharacterData(
                "hero_006",
                "TestHero",
                5,
                400,
                200,
                15,
                10,
                12,
                "Warrior",
                new[] { 50, 100, 200, 400 }
            );
            
            // Act
            string csvContent = CharacterDataSerializer.SerializeCsv(new Dictionary<string, CharacterData>()
            {
                { "hero_006", newCharacter }
            });

            // Assert
            Assert.Contains("50|100|200|400", csvContent);
        }
        
        [Fact]
        public void Should_SerializeDataRefArrays_ToCsv()
        {
            // Arrange
            var newRefData = new RefTestData(
                "test_06",
                new StringDataRef<CharacterData> { Value = "hero_001" },
                new IntDataRef<ItemData> { Value = 1001 },
                new[]
                {
                    new IntDataRef<ItemData> { Value = 2001 },
                    new IntDataRef<ItemData> { Value = 2002 },
                    new IntDataRef<ItemData> { Value = 2003 }
                }
            );

            // Act
            string csvContent = RefTestDataSerializer.SerializeCsv(new Dictionary<string, RefTestData>
            {
                { "test_06", newRefData }
            });
            
            // Assert
            Assert.Contains("2001|2002|2003", csvContent);
        }
    }
}