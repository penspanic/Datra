#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Datra.DataTypes;
using Datra.SampleData.Generated;
using Datra.SampleData.Models;
using Datra.SampleData.OtherNamespace;
using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
    /// <summary>
    /// Behavioural parity for non-polymorphic Datra data on the trim/AOT-safe STJ path.
    /// Mirrors the JsonDataSerializer-targeted tests in SingleDataTests.
    /// </summary>
    public class SystemTextJsonDataSerializerTests
    {
        private static IDataSerializer NewSerializer() =>
            new SystemTextJsonDataSerializer(TestJsonContext.Default);

        [Fact]
        public void Single_RoundTrip_PreservesPrimitives()
        {
            var serializer = NewSerializer();
            var original = new GameConfigData(
                "STJ Game", 75, 1.25f,
                GameMode.Normal,
                new[] { GameMode.Easy, GameMode.Hard },
                new[] { RewardType.Gold, RewardType.Item },
                LanguageCode.English,
                new StringDataRef<CharacterData> { Value = "hero_010" },
                new IntDataRef<ItemData> { Value = 4001 },
                new[] { new StringDataRef<CharacterData> { Value = "hero_a" } },
                new[] { new IntDataRef<ItemData> { Value = 4002 }, new IntDataRef<ItemData> { Value = 4003 } });

            var json = GameConfigDataSerializer.SerializeSingle(original, serializer);
            var restored = GameConfigDataSerializer.DeserializeSingle(json, serializer);

            Assert.Equal(original.GameName, restored.GameName);
            Assert.Equal(original.MaxLevel, restored.MaxLevel);
            Assert.Equal(original.ExpMultiplier, restored.ExpMultiplier);
            Assert.Equal(original.DefaultMode, restored.DefaultMode);
            Assert.Equal(original.AvailableModes, restored.AvailableModes);
            Assert.Equal(original.EnabledRewards, restored.EnabledRewards);
            Assert.Equal(original.DefaultCharacter.Value, restored.DefaultCharacter.Value);
            Assert.Equal(original.StartingItem.Value, restored.StartingItem.Value);
            Assert.Equal(original.UnlockableCharacters.Length, restored.UnlockableCharacters.Length);
            Assert.Equal(original.StartingItems.Length, restored.StartingItems.Length);
        }

        [Fact]
        public void Single_SerializesEnumsAsStrings()
        {
            var serializer = NewSerializer();
            var data = new GameConfigData(
                "STJ", 1, 1.0f, GameMode.Hard,
                new[] { GameMode.Hard }, new[] { RewardType.Gold },
                LanguageCode.English,
                new StringDataRef<CharacterData> { Value = "hero_001" },
                new IntDataRef<ItemData> { Value = 1 },
                System.Array.Empty<StringDataRef<CharacterData>>(),
                System.Array.Empty<IntDataRef<ItemData>>());

            var json = GameConfigDataSerializer.SerializeSingle(data, serializer);

            Assert.Contains("\"DefaultMode\": \"Hard\"", json);
            Assert.Contains("\"Hard\"", json);
            Assert.Contains("\"Gold\"", json);
        }

        [Fact]
        public void Single_DataRef_SerializesAsRawKey()
        {
            var serializer = NewSerializer();
            var data = new GameConfigData(
                "STJ", 1, 1.0f, GameMode.Hard,
                new[] { GameMode.Hard }, new[] { RewardType.Gold },
                LanguageCode.English,
                new StringDataRef<CharacterData> { Value = "hero_x" },
                new IntDataRef<ItemData> { Value = 7777 },
                System.Array.Empty<StringDataRef<CharacterData>>(),
                System.Array.Empty<IntDataRef<ItemData>>());

            var json = GameConfigDataSerializer.SerializeSingle(data, serializer);

            Assert.Contains("\"DefaultCharacter\": \"hero_x\"", json);
            Assert.Contains("\"StartingItem\": 7777", json);
        }

        [Fact]
        public void Single_DeserializesNumericEnums()
        {
            var serializer = NewSerializer();
            var json = @"{
                ""GameName"": ""Numeric Enum"",
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

            var data = GameConfigDataSerializer.DeserializeSingle(json, serializer);

            Assert.Equal(GameMode.Hard, data.DefaultMode);
            Assert.Equal(3, data.AvailableModes.Length);
            Assert.Equal(GameMode.Easy, data.AvailableModes[0]);
            Assert.Equal(GameMode.Hard, data.AvailableModes[2]);
        }

        [Fact]
        public void Table_RoundTrip_PreservesAllItems()
        {
            var serializer = NewSerializer();
            var a = new ItemData(1, 100, ItemType.Weapon, 10, 0, "icon/a", "drop/a", "snd/a", "fx/a");
            var b = new ItemData(2, 200, ItemType.Armor,  0, 15, "icon/b", "drop/b", "snd/b", "fx/b");
            var table = new Dictionary<int, ItemData> { [a.Id] = a, [b.Id] = b };

            var json = ItemDataSerializer.SerializeTable(table, serializer);
            var restored = ItemDataSerializer.DeserializeTable(json, serializer);

            Assert.Equal(2, restored.Count);
            Assert.Equal(100, restored[1].Price);
            Assert.Equal(ItemType.Armor, restored[2].Type);
            Assert.Equal("drop/a", restored[1].DroppedItemPrefabPath);
        }
    }

    /// <summary>
    /// The consumer-authored STJ source-gen context. STJ's own source generator runs in
    /// this test assembly (net9.0) and produces the reflection-free overrides. The
    /// canonical pattern Tidemark.Client will follow for trim/AOT safety.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        UseStringEnumConverter = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString)]
    [JsonSerializable(typeof(GameConfigData))]
    [JsonSerializable(typeof(ItemData))]
    [JsonSerializable(typeof(List<ItemData>))]
    [JsonSerializable(typeof(Dictionary<int, ItemData>))]
    internal partial class TestJsonContext : JsonSerializerContext
    {
    }
}
