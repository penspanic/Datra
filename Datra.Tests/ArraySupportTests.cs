using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datra.DataTypes;
using Datra.Generated;
using Datra.SampleData.Models;
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
            if (!_context.Character.TryGetValue("hero_001", out var character))
            {
                Assert.Fail("Character data not found");
                return;
            }
            
            
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
            if (!_context.RefTest.TryGetValue("test_01", out var refData))
            {
                Assert.Fail("RefTest data not found");
                return;
            }
            
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
            if (!_context.RefTest.TryGetValue("test_05", out var refData))
            {
                Assert.Fail("RefTest data not found");
                return;
            }
            
            // Assert
            Assert.NotNull(refData);
            Assert.NotNull(refData.ItemRefs);
            Assert.Empty(refData.ItemRefs);
        }
        
        [Fact]
        public void Should_SerializeArrays_ToCsv()
        {
            // Arrange
            var newCharacter = new CharacterData(
                "hero_006",
                5,
                400,
                200,
                15,
                10,
                12,
                "Warrior",
                CharacterGrade.Epic,
                new[] { StatType.Attack, StatType.Defense },
                new[] { 50, 100, 200, 400 },
                new PooledPrefab(),  // TestPooledPrefab
                null,  // ModelPrefabPath
                null,  // PortraitPath
                null,  // IconPath
                null   // AttackSoundPath
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
        
        [Fact]
        public void Should_ParseEnumValue_FromCsv()
        {
            // Act
            if (!_context.Character.TryGetValue("hero_001", out var character))
            {
                Assert.Fail("Character data not found");
                return;
            }
            
            // Assert
            Assert.NotNull(character);
            Assert.Equal(CharacterGrade.Common, character.Grade);
        }
        
        [Fact]
        public void Should_SerializeEnumValue_ToCsv()
        {
            // Arrange
            var newCharacter = new CharacterData(
                "hero_007",
                10,
                1000,
                500,
                30,
                25,
                20,
                "Paladin",
                CharacterGrade.Legendary,
                new[] { StatType.Attack, StatType.Defense, StatType.HealthRegen },
                new[] { 100, 200, 400, 800, 1600 },
                new PooledPrefab(),  // TestPooledPrefab
                null,  // ModelPrefabPath
                null,  // PortraitPath
                null,  // IconPath
                null   // AttackSoundPath
            );
            
            // Act
            string csvContent = CharacterDataSerializer.SerializeCsv(new Dictionary<string, CharacterData>()
            {
                { "hero_007", newCharacter }
            });

            // Assert
            Assert.Contains("Legendary", csvContent);
            Assert.Contains("hero_007", csvContent);
        }
        
        [Fact]
        public void Should_ParseDifferentEnumValues_FromCsv()
        {
            // Create test CSV with different enum values
            var csvData = @"Id,Name,Level,Health,Mana,Strength,Intelligence,Agility,ClassName,Grade,Stats,UpgradeCosts
test_common,CommonHero,1,100,50,10,5,8,Warrior,Common,Attack|Defense,10|20|30
test_rare,RareHero,5,200,100,15,10,12,Mage,Rare,Attack|ManaRegen,50|100|200
test_epic,EpicHero,10,400,200,20,15,18,Rogue,Epic,Speed|CriticalRate,100|200|400
test_legendary,LegendaryHero,15,800,400,30,25,25,Paladin,Legendary,Attack|Defense|HealthRegen,200|400|800";
            
            // Act
            var characters = CharacterDataSerializer.DeserializeCsv(csvData);
            
            // Assert
            Assert.Equal(CharacterGrade.Common, characters["test_common"].Grade);
            Assert.Equal(CharacterGrade.Rare, characters["test_rare"].Grade);
            Assert.Equal(CharacterGrade.Epic, characters["test_epic"].Grade);
            Assert.Equal(CharacterGrade.Legendary, characters["test_legendary"].Grade);
        }
        
        [Fact]
        public void Should_ParseEnumArray_FromCsv()
        {
            // Act
            if (!_context.Character.TryGetValue("hero_001", out var character))
            {
                Assert.Fail("Character data not found");
                return;
            }
            
            // Assert
            Assert.NotNull(character);
            Assert.NotNull(character.Stats);
            Assert.Equal(3, character.Stats.Length);
            Assert.Equal(StatType.Attack, character.Stats[0]);
            Assert.Equal(StatType.Defense, character.Stats[1]);
            Assert.Equal(StatType.HealthRegen, character.Stats[2]);
        }
        
        [Fact]
        public void Should_ParseDifferentEnumArrays_FromCsv()
        {
            // Act
            
            if (!_context.Character.TryGetValue("hero_001", out var arthur))
            {
                Assert.Fail("Character data not found for hero_001");
                return;
            }

            if (!_context.Character.TryGetValue("hero_003", out var lena))
            {
                Assert.Fail("Character data not found for hero_003");
                return;
            }

            if (!_context.Character.TryGetValue("hero_004", out var thor))
            {
                Assert.Fail("Character data not found for hero_004");
                return;
            }
            
            // Assert
            Assert.NotNull(arthur.Stats);
            Assert.Equal(3, arthur.Stats.Length);
            Assert.Contains(StatType.Attack, arthur.Stats);
            Assert.Contains(StatType.Defense, arthur.Stats);
            
            Assert.NotNull(lena.Stats);
            Assert.Equal(4, lena.Stats.Length);
            Assert.Contains(StatType.Speed, lena.Stats);
            Assert.Contains(StatType.CriticalRate, lena.Stats);
            Assert.Contains(StatType.Accuracy, lena.Stats);
            
            Assert.NotNull(thor.Stats);
            Assert.Equal(5, thor.Stats.Length);
            Assert.Contains(StatType.Attack, thor.Stats);
            Assert.Contains(StatType.Defense, thor.Stats);
            Assert.Contains(StatType.CriticalDamage, thor.Stats);
            Assert.Contains(StatType.HealthRegen, thor.Stats);
            Assert.Contains(StatType.Speed, thor.Stats);
        }
        
        [Fact]
        public void Should_SerializeEnumArray_ToCsv()
        {
            // Arrange
            var newCharacter = new CharacterData(
                "hero_008",
                20,
                2000,
                1000,
                40,
                30,
                35,
                "Warrior",
                CharacterGrade.Epic,
                new[] { StatType.Attack, StatType.Defense, StatType.CriticalRate, StatType.Evasion },
                new[] { 500, 1000, 2000, 4000 },
                new PooledPrefab(),  // TestPooledPrefab
                null,  // ModelPrefabPath
                null,  // PortraitPath
                null,  // IconPath
                null   // AttackSoundPath
            );
            
            // Act
            string csvContent = CharacterDataSerializer.SerializeCsv(new Dictionary<string, CharacterData>()
            {
                { "hero_008", newCharacter }
            });

            // Assert
            Assert.Contains("Attack|Defense|CriticalRate|Evasion", csvContent);
            Assert.Contains("Epic", csvContent);
        }
        
        [Fact]
        public void Should_HandleEmptyEnumArray_FromCsv()
        {
            // Create test CSV with empty enum array
            var csvData = @"Id,Name,Level,Health,Mana,Strength,Intelligence,Agility,ClassName,Grade,Stats,UpgradeCosts
test_empty,EmptyStatsHero,1,100,50,10,5,8,Warrior,Common,,10|20|30";
            
            // Act
            var characters = CharacterDataSerializer.DeserializeCsv(csvData);
            
            // Assert
            Assert.NotNull(characters["test_empty"]);
            Assert.NotNull(characters["test_empty"].Stats);
            Assert.Empty(characters["test_empty"].Stats);
        }
    }
}