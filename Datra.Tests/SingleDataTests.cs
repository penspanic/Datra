using System.IO;
using System.Linq;
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Generated;
using Datra.SampleData.Models;
using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
    public class SingleDataTests
    {
        private readonly GameDataContext _context;
        
        public SingleDataTests()
        {
            _context = TestDataHelper.CreateGameDataContext();
            _context.LoadAllAsync().Wait();
        }
        
        [Fact]
        public void Should_LoadSingleData_FromJson()
        {
            // Act
            var gameConfig = _context.GameConfig.Get();
            
            // Assert
            Assert.NotNull(gameConfig);
            Assert.Equal("Epic Adventure", gameConfig.GameName);
            Assert.Equal(100, gameConfig.MaxLevel);
            Assert.Equal(1.5f, gameConfig.ExpMultiplier);
        }
        
        [Fact]
        public void Should_ParseEnum_InSingleData()
        {
            // Act
            var gameConfig = _context.GameConfig.Get();
            
            // Assert
            Assert.Equal(GameMode.Normal, gameConfig.DefaultMode);
        }
        
        [Fact]
        public void Should_ParseEnumArray_InSingleData()
        {
            // Act
            var gameConfig = _context.GameConfig.Get();
            
            // Assert
            Assert.NotNull(gameConfig.AvailableModes);
            Assert.Equal(4, gameConfig.AvailableModes.Length);
            Assert.Equal(GameMode.Easy, gameConfig.AvailableModes[0]);
            Assert.Equal(GameMode.Normal, gameConfig.AvailableModes[1]);
            Assert.Equal(GameMode.Hard, gameConfig.AvailableModes[2]);
            Assert.Equal(GameMode.Expert, gameConfig.AvailableModes[3]);
            
            Assert.NotNull(gameConfig.EnabledRewards);
            Assert.Equal(3, gameConfig.EnabledRewards.Length);
            Assert.Contains(RewardType.Gold, gameConfig.EnabledRewards);
            Assert.Contains(RewardType.Experience, gameConfig.EnabledRewards);
            Assert.Contains(RewardType.Item, gameConfig.EnabledRewards);
        }
        
        [Fact]
        public void Should_ParseDataRef_InSingleData()
        {
            // Act
            var gameConfig = _context.GameConfig.Get();
            
            // Assert
            // DataRef is a value type, no need for null check
            Assert.Equal("hero_001", gameConfig.DefaultCharacter.Value);
            // DataRef is a value type, no need for null check
            Assert.Equal(1001, gameConfig.StartingItem.Value);
        }
        
        [Fact]
        public void Should_ParseDataRefArray_InSingleData()
        {
            // Act
            var gameConfig = _context.GameConfig.Get();
            
            // Assert
            Assert.NotNull(gameConfig.UnlockableCharacters);
            Assert.Equal(3, gameConfig.UnlockableCharacters.Length);
            Assert.Equal("hero_002", gameConfig.UnlockableCharacters[0].Value);
            Assert.Equal("hero_003", gameConfig.UnlockableCharacters[1].Value);
            Assert.Equal("hero_004", gameConfig.UnlockableCharacters[2].Value);
            
            Assert.NotNull(gameConfig.StartingItems);
            Assert.Equal(3, gameConfig.StartingItems.Length);
            Assert.Equal(1001, gameConfig.StartingItems[0].Value);
            Assert.Equal(1002, gameConfig.StartingItems[1].Value);
            Assert.Equal(2001, gameConfig.StartingItems[2].Value);
        }
        
        [Fact]
        public void Should_SerializeSingleData_ToJson()
        {
            // Arrange
            var newConfig = new GameConfigData(
                "Test Game",
                50,
                2.0f,
                GameMode.Hard,
                new[] { GameMode.Normal, GameMode.Hard },
                new[] { RewardType.Gold, RewardType.Skill, RewardType.Achievement },
                new StringDataRef<CharacterData> { Value = "hero_005" },
                new IntDataRef<ItemData> { Value = 3001 },
                new[]
                {
                    new StringDataRef<CharacterData> { Value = "hero_001" },
                    new StringDataRef<CharacterData> { Value = "hero_002" }
                },
                new[]
                {
                    new IntDataRef<ItemData> { Value = 3001 },
                    new IntDataRef<ItemData> { Value = 3002 },
                    new IntDataRef<ItemData> { Value = 3003 }
                }
            );
            
            // Act
            string json = GameConfigDataSerializer.SerializeSingle(newConfig, new JsonDataSerializer());
            
            // Assert
            Assert.Contains("\"GameName\": \"Test Game\"", json);
            Assert.Contains("\"MaxLevel\": 50", json);
            Assert.Contains("\"ExpMultiplier\": 2.0", json);
            Assert.Contains("\"DefaultMode\": \"Hard\"", json);
            Assert.Contains("\"Gold\"", json);
            Assert.Contains("\"Skill\"", json);
            Assert.Contains("\"Achievement\"", json);
            Assert.Contains("\"hero_005\"", json);
            Assert.Contains("3001", json);
        }
        
        [Fact]
        public void Should_RoundTrip_SingleData()
        {
            // Arrange
            var original = _context.GameConfig.Get();
            
            // Act
            string serialized = GameConfigDataSerializer.SerializeSingle(original, new JsonDataSerializer());
            var deserialized = GameConfigDataSerializer.DeserializeSingle(serialized, new JsonDataSerializer());
            
            // Assert
            Assert.Equal(original.GameName, deserialized.GameName);
            Assert.Equal(original.MaxLevel, deserialized.MaxLevel);
            Assert.Equal(original.ExpMultiplier, deserialized.ExpMultiplier);
            Assert.Equal(original.DefaultMode, deserialized.DefaultMode);
            Assert.Equal(original.AvailableModes.Length, deserialized.AvailableModes.Length);
            Assert.Equal(original.EnabledRewards.Length, deserialized.EnabledRewards.Length);
            Assert.Equal(original.DefaultCharacter.Value, deserialized.DefaultCharacter.Value);
            Assert.Equal(original.StartingItem.Value, deserialized.StartingItem.Value);
            Assert.Equal(original.UnlockableCharacters.Length, deserialized.UnlockableCharacters.Length);
            Assert.Equal(original.StartingItems.Length, deserialized.StartingItems.Length);
        }
        
        [Fact]
        public void Should_HandleEnumAsNumber_InJson()
        {
            // Arrange
            var json = @"{
                ""GameName"": ""Numeric Enum Test"",
                ""MaxLevel"": 10,
                ""ExpMultiplier"": 1.0,
                ""DefaultMode"": 2,
                ""AvailableModes"": [0, 1, 2],
                ""EnabledRewards"": [0, 2, 4],
                ""DefaultCharacter"": ""hero_001"",
                ""StartingItem"": 1001,
                ""UnlockableCharacters"": [],
                ""StartingItems"": []
            }";
            
            // Act
            var gameConfig = GameConfigDataSerializer.DeserializeSingle(json, new JsonDataSerializer());
            
            // Assert
            Assert.Equal(GameMode.Hard, gameConfig.DefaultMode);
            Assert.Equal(3, gameConfig.AvailableModes.Length);
            Assert.Equal(GameMode.Easy, gameConfig.AvailableModes[0]);
            Assert.Equal(GameMode.Normal, gameConfig.AvailableModes[1]);
            Assert.Equal(GameMode.Hard, gameConfig.AvailableModes[2]);
            Assert.Contains(RewardType.Gold, gameConfig.EnabledRewards);
            Assert.Contains(RewardType.Item, gameConfig.EnabledRewards);
            Assert.Contains(RewardType.Achievement, gameConfig.EnabledRewards);
        }
        
        [Fact]
        public void Should_HandleEmptyArrays_InSingleData()
        {
            // Arrange
            var json = @"{
                ""GameName"": ""Empty Arrays Test"",
                ""MaxLevel"": 1,
                ""ExpMultiplier"": 1.0,
                ""DefaultMode"": ""Easy"",
                ""AvailableModes"": [],
                ""EnabledRewards"": [],
                ""DefaultCharacter"": ""hero_001"",
                ""StartingItem"": 1001,
                ""UnlockableCharacters"": [],
                ""StartingItems"": []
            }";
            
            // Act
            var gameConfig = GameConfigDataSerializer.DeserializeSingle(json, new JsonDataSerializer());
            
            // Assert
            Assert.NotNull(gameConfig.AvailableModes);
            Assert.Empty(gameConfig.AvailableModes);
            Assert.NotNull(gameConfig.EnabledRewards);
            Assert.Empty(gameConfig.EnabledRewards);
            Assert.NotNull(gameConfig.UnlockableCharacters);
            Assert.Empty(gameConfig.UnlockableCharacters);
            Assert.NotNull(gameConfig.StartingItems);
            Assert.Empty(gameConfig.StartingItems);
        }
    }
}