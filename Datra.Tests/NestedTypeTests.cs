using System.Collections.Generic;
using Datra.SampleData.Models;
using Xunit;

namespace Datra.Tests
{
    public class NestedTypeTests
    {
        private readonly Generated.GameDataContext _context;

        public NestedTypeTests()
        {
            _context = TestDataHelper.CreateGameDataContext();
            _context.LoadAllAsync().Wait();
        }

        [Fact]
        public void Should_DeserializeNestedType_FromCsv()
        {
            // Arrange & Act - data loaded in constructor
            var hero1 = _context.Character["hero_001"];

            // Assert - hero_001 has TestPooledPrefab data
            Assert.Equal("Assets/Prefabs/Skills/Slash.prefab", hero1.TestPooledPrefab.Path);
            Assert.Equal(3, hero1.TestPooledPrefab.InitialCount);
            Assert.Equal(10, hero1.TestPooledPrefab.MaxCount);
        }

        [Fact]
        public void Should_DeserializeNestedType_WithDifferentValues()
        {
            // Arrange & Act
            var hero2 = _context.Character["hero_002"];
            var hero3 = _context.Character["hero_003"];
            var hero4 = _context.Character["hero_004"];
            var hero5 = _context.Character["hero_005"];

            // Assert hero_002
            Assert.Equal("Assets/Prefabs/Skills/Fireball.prefab", hero2.TestPooledPrefab.Path);
            Assert.Equal(5, hero2.TestPooledPrefab.InitialCount);
            Assert.Equal(20, hero2.TestPooledPrefab.MaxCount);

            // Assert hero_003
            Assert.Equal("Assets/Prefabs/Skills/Arrow.prefab", hero3.TestPooledPrefab.Path);
            Assert.Equal(10, hero3.TestPooledPrefab.InitialCount);
            Assert.Equal(50, hero3.TestPooledPrefab.MaxCount);

            // Assert hero_004
            Assert.Equal("Assets/Prefabs/Skills/Whirlwind.prefab", hero4.TestPooledPrefab.Path);
            Assert.Equal(2, hero4.TestPooledPrefab.InitialCount);
            Assert.Equal(5, hero4.TestPooledPrefab.MaxCount);

            // Assert hero_005
            Assert.Equal("Assets/Prefabs/Skills/IceBolt.prefab", hero5.TestPooledPrefab.Path);
            Assert.Equal(8, hero5.TestPooledPrefab.InitialCount);
            Assert.Equal(30, hero5.TestPooledPrefab.MaxCount);
        }

        [Fact]
        public void Should_DeserializeNestedType_WithEmptyValues()
        {
            // Arrange & Act - hero_006 has no TestPooledPrefab data
            var hero6 = _context.Character["hero_006"];

            // Assert - empty/default values
            Assert.Equal(string.Empty, hero6.TestPooledPrefab.Path ?? string.Empty);
            Assert.Equal(0, hero6.TestPooledPrefab.InitialCount);
            Assert.Equal(0, hero6.TestPooledPrefab.MaxCount);
        }

        [Fact]
        public void Should_SerializeNestedType_ToCsv()
        {
            // Arrange
            var testPrefab = new PooledPrefab
            {
                Path = "Assets/Prefabs/Test.prefab",
                InitialCount = 15,
                MaxCount = 100
            };

            var newCharacter = new CharacterData(
                "test_hero",
                1,
                100,
                50,
                10,
                10,
                10,
                "Test",
                CharacterGrade.Common,
                new[] { StatType.Attack },
                new[] { 10 },
                testPrefab,
                null,
                null,
                null,
                null
            );

            var table = new Dictionary<string, CharacterData>
            {
                { "test_hero", newCharacter }
            };

            // Act
            string csv = CharacterDataSerializer.SerializeCsv(table);

            // Assert - CSV should contain the nested type fields with dot notation
            Assert.Contains("TestPooledPrefab.Path", csv);
            Assert.Contains("TestPooledPrefab.InitialCount", csv);
            Assert.Contains("TestPooledPrefab.MaxCount", csv);
            Assert.Contains("Assets/Prefabs/Test.prefab", csv);
            Assert.Contains("15", csv);
            Assert.Contains("100", csv);
        }

        [Fact]
        public void Should_RoundTrip_NestedType()
        {
            // Arrange
            var testPrefab = new PooledPrefab
            {
                Path = "Assets/Prefabs/RoundTrip.prefab",
                InitialCount = 7,
                MaxCount = 42
            };

            var original = new CharacterData(
                "roundtrip_hero",
                5,
                500,
                250,
                15,
                15,
                15,
                "RoundTrip",
                CharacterGrade.Rare,
                new[] { StatType.Attack, StatType.Defense },
                new[] { 100, 200 },
                testPrefab,
                null,
                null,
                null,
                null
            );

            var table = new Dictionary<string, CharacterData>
            {
                { "roundtrip_hero", original }
            };

            // Act - Serialize then Deserialize
            string csv = CharacterDataSerializer.SerializeCsv(table);
            var deserialized = CharacterDataSerializer.DeserializeCsv(csv);

            // Assert
            Assert.True(deserialized.ContainsKey("roundtrip_hero"));
            var result = deserialized["roundtrip_hero"];

            Assert.Equal(original.TestPooledPrefab.Path, result.TestPooledPrefab.Path);
            Assert.Equal(original.TestPooledPrefab.InitialCount, result.TestPooledPrefab.InitialCount);
            Assert.Equal(original.TestPooledPrefab.MaxCount, result.TestPooledPrefab.MaxCount);
        }

        [Fact]
        public void Should_PreserveOtherFields_WithNestedType()
        {
            // Arrange & Act
            var hero1 = _context.Character["hero_001"];

            // Assert - Other fields should still work
            Assert.Equal("hero_001", hero1.Id);
            Assert.Equal(10, hero1.Level);
            Assert.Equal(1000, hero1.Health);
            Assert.Equal("Warrior", hero1.ClassName);
            Assert.Equal(CharacterGrade.Common, hero1.Grade);
            Assert.Contains(StatType.Attack, hero1.Stats);
            Assert.Contains(StatType.Defense, hero1.Stats);
        }
    }
}
