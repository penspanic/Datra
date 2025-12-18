using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.SampleData.Models;
using Datra.Serializers;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Datra.Tests
{
    /// <summary>
    /// Test-only POCO class for serialization roundtrip tests (not a Datra data class)
    /// </summary>
    public class TestQuestData
    {
        public string Id { get; set; }
        public QuestType Type { get; set; }
        public int RequiredLevel { get; set; }
        public int RewardGold { get; set; }
        public int RewardExp { get; set; }
        public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
    }

    public class PolymorphicJsonTests
    {
        private readonly ITestOutputHelper _output;
        private readonly JsonDataSerializer _serializer;

        public PolymorphicJsonTests(ITestOutputHelper output)
        {
            _output = output;
            _serializer = new JsonDataSerializer();
        }

        [Fact]
        public async Task LoadQuestData_WithPolymorphicObjectives_ShouldDeserializeCorrectTypes()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();

            // Act
            var allQuests = context.Quest.Values.ToList();

            // Assert
            Assert.NotEmpty(allQuests);

            var mainQuest = allQuests.FirstOrDefault(q => q.Id == "quest_main_001");
            Assert.NotNull(mainQuest);
            Assert.Equal(QuestType.Main, mainQuest.Type);
            Assert.Equal(2, mainQuest.Objectives.Count);

            // First objective should be TalkObjective
            var talkObj = mainQuest.Objectives[0] as TalkObjective;
            Assert.NotNull(talkObj);
            Assert.Equal("npc_elder_001", talkObj.TargetNpcId);
            Assert.Equal(2, talkObj.DialogueKeys.Length);

            // Second objective should be KillObjective
            var killObj = mainQuest.Objectives[1] as KillObjective;
            Assert.NotNull(killObj);
            Assert.Equal("enemy_slime", killObj.TargetEnemyId);
            Assert.Equal(5, killObj.RequiredCount);

            _output.WriteLine($"Quest: {mainQuest.Id}");
            _output.WriteLine($"  Type: {mainQuest.Type}");
            _output.WriteLine($"  Objectives: {mainQuest.Objectives.Count}");
            foreach (var obj in mainQuest.Objectives)
            {
                _output.WriteLine($"    - [{obj.GetType().Name}] {obj.Description}");
            }
        }

        [Fact]
        public async Task LoadQuestData_WithCollectAndLocationObjectives_ShouldDeserializeCorrectly()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();

            // Act
            context.Quest.TryGetValue("quest_main_002", out var quest);

            // Assert
            Assert.NotNull(quest);
            Assert.Equal(2, quest.Objectives.Count);

            // First objective should be CollectObjective
            var collectObj = quest.Objectives[0] as CollectObjective;
            Assert.NotNull(collectObj);
            Assert.Equal(2001, collectObj.TargetItemId);
            Assert.Equal(10, collectObj.RequiredAmount);

            // Second objective should be LocationObjective
            var locationObj = quest.Objectives[1] as LocationObjective;
            Assert.NotNull(locationObj);
            Assert.Equal("location_forest_shrine", locationObj.LocationId);
            Assert.Equal(5.0f, locationObj.Radius);

            _output.WriteLine($"Quest: {quest.Id}");
            _output.WriteLine($"  CollectObjective: Item {collectObj.TargetItemId} x {collectObj.RequiredAmount}");
            _output.WriteLine($"  LocationObjective: {locationObj.LocationId} (radius: {locationObj.Radius})");
        }

        [Fact]
        public async Task LoadQuestData_WithRewardItems_ShouldDeserializeDictionary()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();

            // Act
            context.Quest.TryGetValue("quest_main_002", out var quest);

            // Assert
            Assert.NotNull(quest);
            Assert.NotNull(quest.RewardItems);
            Assert.Equal(2, quest.RewardItems.Count);
            Assert.True(quest.RewardItems.ContainsKey(1002));
            Assert.True(quest.RewardItems.ContainsKey(2001));
            Assert.Equal(1, quest.RewardItems[1002]);
            Assert.Equal(5, quest.RewardItems[2001]);

            _output.WriteLine($"Quest: {quest.Id}");
            _output.WriteLine($"  Reward Items:");
            foreach (var kvp in quest.RewardItems)
            {
                _output.WriteLine($"    - Item {kvp.Key}: x{kvp.Value}");
            }
        }

        [Fact]
        public void SerializeTestData_WithPolymorphicObjectives_ShouldIncludeTypeInfo()
        {
            // Arrange - using test POCO class (not Datra data class)
            var quest = new TestQuestData
            {
                Id = "test_quest",
                Type = QuestType.Side,
                RequiredLevel = 10,
                RewardGold = 500,
                RewardExp = 250,
                Objectives = new List<QuestObjective>
                {
                    new KillObjective
                    {
                        Id = "kill_01",
                        Description = "Kill dragons",
                        TargetEnemyId = "enemy_dragon",
                        RequiredCount = 3
                    },
                    new CollectObjective
                    {
                        Id = "collect_01",
                        Description = "Collect dragon scales",
                        TargetItemId = 5001,
                        RequiredAmount = 5
                    }
                }
            };

            // Act
            var json = _serializer.SerializeSingle(quest);
            _output.WriteLine("Serialized JSON:");
            _output.WriteLine(json);

            // Assert - should contain $type for polymorphic objectives
            Assert.Contains("$type", json);
            Assert.Contains("KillObjective", json);
            Assert.Contains("CollectObjective", json);
        }

        [Fact]
        public void DeserializeTestData_FromSerializedJson_ShouldPreserveTypes()
        {
            // Arrange - using test POCO class (not Datra data class)
            var originalQuest = new TestQuestData
            {
                Id = "roundtrip_test",
                Type = QuestType.Event,
                RequiredLevel = 1,
                RewardGold = 100,
                RewardExp = 50,
                Objectives = new List<QuestObjective>
                {
                    new TalkObjective
                    {
                        Id = "talk_01",
                        Description = "Talk to event NPC",
                        TargetNpcId = "npc_event",
                        DialogueKeys = new[] { "event_start", "event_end" }
                    },
                    new LocationObjective
                    {
                        Id = "location_01",
                        Description = "Go to event area",
                        LocationId = "event_zone",
                        Radius = 10.0f
                    }
                }
            };

            // Act - serialize and deserialize
            var json = _serializer.SerializeSingle(originalQuest);
            var deserializedQuest = _serializer.DeserializeSingle<TestQuestData>(json);

            // Assert
            Assert.NotNull(deserializedQuest);
            Assert.Equal(originalQuest.Id, deserializedQuest.Id);
            Assert.Equal(originalQuest.Type, deserializedQuest.Type);
            Assert.Equal(originalQuest.Objectives.Count, deserializedQuest.Objectives.Count);

            // Check type preservation
            Assert.IsType<TalkObjective>(deserializedQuest.Objectives[0]);
            Assert.IsType<LocationObjective>(deserializedQuest.Objectives[1]);

            var talkObj = (TalkObjective)deserializedQuest.Objectives[0];
            Assert.Equal("npc_event", talkObj.TargetNpcId);
            Assert.Equal(2, talkObj.DialogueKeys.Length);

            var locationObj = (LocationObjective)deserializedQuest.Objectives[1];
            Assert.Equal("event_zone", locationObj.LocationId);
            Assert.Equal(10.0f, locationObj.Radius);

            _output.WriteLine("Round-trip test passed!");
            _output.WriteLine($"Original: {originalQuest.Id} with {originalQuest.Objectives.Count} objectives");
            _output.WriteLine($"Deserialized: {deserializedQuest.Id} with {deserializedQuest.Objectives.Count} objectives");
        }

        [Fact]
        public async Task AllQuestTypes_ShouldLoadCorrectly()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();

            // Act
            var allQuests = context.Quest.Values.ToList();

            // Assert
            Assert.Equal(4, allQuests.Count); // 4 quests in sample data

            var mainQuests = allQuests.Where(q => q.Type == QuestType.Main).ToList();
            var sideQuests = allQuests.Where(q => q.Type == QuestType.Side).ToList();
            var dailyQuests = allQuests.Where(q => q.Type == QuestType.Daily).ToList();

            Assert.Equal(2, mainQuests.Count);
            Assert.Single(sideQuests);
            Assert.Single(dailyQuests);

            _output.WriteLine($"Total quests: {allQuests.Count}");
            _output.WriteLine($"  Main: {mainQuests.Count}");
            _output.WriteLine($"  Side: {sideQuests.Count}");
            _output.WriteLine($"  Daily: {dailyQuests.Count}");

            // Verify all objective types are present across all quests
            var allObjectives = allQuests.SelectMany(q => q.Objectives).ToList();
            Assert.Contains(allObjectives, o => o is KillObjective);
            Assert.Contains(allObjectives, o => o is CollectObjective);
            Assert.Contains(allObjectives, o => o is TalkObjective);
            Assert.Contains(allObjectives, o => o is LocationObjective);

            _output.WriteLine($"Total objectives: {allObjectives.Count}");
            _output.WriteLine($"  KillObjective: {allObjectives.Count(o => o is KillObjective)}");
            _output.WriteLine($"  CollectObjective: {allObjectives.Count(o => o is CollectObjective)}");
            _output.WriteLine($"  TalkObjective: {allObjectives.Count(o => o is TalkObjective)}");
            _output.WriteLine($"  LocationObjective: {allObjectives.Count(o => o is LocationObjective)}");
        }
    }
}
