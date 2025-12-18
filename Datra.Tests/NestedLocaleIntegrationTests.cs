using System.Linq;
using System.Threading.Tasks;
using Datra.DataTypes;
using Datra.Localization;
using Datra.SampleData.Models;
using Xunit;
using Xunit.Abstractions;

namespace Datra.Tests
{
    public class NestedLocaleIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public NestedLocaleIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task QuestData_FixedLocaleKeys_GenerateCorrectKeys()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();

            // Act
            var quest = context.Quest.Values.First(q => q.Id == "quest_main_001");

            // Assert - Fixed locale keys (note: property names are lowercase)
            Assert.Equal("QuestData.quest_main_001.Name", quest.Name.Key);
            Assert.Equal("QuestData.quest_main_001.Description", quest.Description.Key);

            _output.WriteLine($"Quest: {quest.Id}");
            _output.WriteLine($"  Name Key: {quest.Name.Key}");
            _output.WriteLine($"  Description Key: {quest.Description.Key}");
        }

        [Fact]
        public async Task QuestObjective_DescriptionLocale_GeneratesCorrectNestedKey()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();

            var quest = context.Quest.Values.First(q => q.Id == "quest_main_001");

            // Act - Get locale keys for each objective using ILocaleEvaluator
            var obj0Key = quest.GetObjectiveDescription(quest.Objectives[0]);
            var obj1Key = quest.GetObjectiveDescription(quest.Objectives[1]);

            // Assert - Nested locale keys with indices
            Assert.Equal("QuestData.quest_main_001.Objectives#0.Description", obj0Key.Key);
            Assert.Equal("QuestData.quest_main_001.Objectives#1.Description", obj1Key.Key);

            _output.WriteLine($"Quest: {quest.Id}");
            _output.WriteLine($"  Objective[0] Key: {obj0Key.Key}");
            _output.WriteLine($"  Objective[1] Key: {obj1Key.Key}");
        }

        [Fact]
        public async Task AllQuests_NestedLocaleKeys_AreConsistent()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();

            // Act & Assert - Verify all quests have valid nested locale keys
            foreach (var quest in context.Quest.Values)
            {
                _output.WriteLine($"\nQuest: {quest.Id}");
                _output.WriteLine($"  Name Key: {quest.Name.Key}");
                _output.WriteLine($"  Description Key: {quest.Description.Key}");

                // Verify fixed key format (note: property names are lowercase)
                Assert.Equal($"QuestData.{quest.Id}.Name", quest.Name.Key);
                Assert.Equal($"QuestData.{quest.Id}.Description", quest.Description.Key);

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    var objective = quest.Objectives[i];
                    var descKey = quest.GetObjectiveDescription(objective);

                    // Verify nested key format
                    Assert.Equal($"QuestData.{quest.Id}.Objectives#{i}.Description", descKey.Key);
                    _output.WriteLine($"  Objective[{i}] Key: {descKey.Key}");
                }
            }
        }

        [Fact]
        public async Task QuestData_ILocaleEvaluator_EvaluatesCorrectly()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();

            var quest = context.Quest.Values.First(q => q.Id == "quest_main_002");
            var objective = quest.Objectives[1]; // Second objective

            // Act - Use ILocaleEvaluator interface directly
            var nestedRef = objective.DescriptionLocale;
            var evaluatedKey = nestedRef.Evaluate(quest, quest.Id, objective);

            // Assert
            Assert.Equal("QuestData.quest_main_002.Objectives#1.Description", evaluatedKey.Key);

            _output.WriteLine($"NestedLocaleRef: {nestedRef.PathTemplate}");
            _output.WriteLine($"Evaluated Key: {evaluatedKey.Key}");
        }

        [Fact]
        public async Task QuestData_MultipleQuestTypes_HaveCorrectKeys()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();

            // Act & Assert - Check different quest types
            var mainQuest = context.Quest.Values.First(q => q.Type == QuestType.Main);
            var sideQuest = context.Quest.Values.First(q => q.Type == QuestType.Side);
            var dailyQuest = context.Quest.Values.First(q => q.Type == QuestType.Daily);

            _output.WriteLine($"Main Quest: {mainQuest.Id}");
            Assert.StartsWith("QuestData.quest_main_", mainQuest.Name.Key);

            _output.WriteLine($"Side Quest: {sideQuest.Id}");
            Assert.StartsWith("QuestData.quest_side_", sideQuest.Name.Key);

            _output.WriteLine($"Daily Quest: {dailyQuest.Id}");
            Assert.StartsWith("QuestData.quest_daily_", dailyQuest.Name.Key);
        }

        [Fact]
        public void NestedLocaleRef_CachingWorks_ForRepeatedAccess()
        {
            // Arrange
            NestedLocaleRef.ClearCache();
            var descriptionLocale = NestedLocaleRef.Create("Objectives", "Description");

            // Act - Simulate repeated access (like in a game loop)
            var results = new LocaleRef[100];
            for (int i = 0; i < 100; i++)
            {
                results[i] = descriptionLocale.Evaluate("QuestData.quest_001", "Objectives", 0);
            }

            // Assert - All should return the same cached string instance
            for (int i = 1; i < 100; i++)
            {
                Assert.Same(results[0].Key, results[i].Key);
            }

            _output.WriteLine("Caching test passed: 100 evaluations returned same string instance");
        }

        [Fact]
        public void NestedLocaleRef_DifferentObjectives_HaveDifferentKeys()
        {
            // Arrange
            var descriptionLocale = NestedLocaleRef.Create("Objectives", "Description");
            var prefix = "QuestData.quest_main_001";

            // Act
            var keys = new LocaleRef[5];
            for (int i = 0; i < 5; i++)
            {
                keys[i] = descriptionLocale.Evaluate(prefix, "Objectives", i);
            }

            // Assert - Each objective should have a unique key
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal($"QuestData.quest_main_001.Objectives#{i}.Description", keys[i].Key);
                _output.WriteLine($"Objective[{i}]: {keys[i].Key}");
            }

            // Verify all keys are different
            var uniqueKeys = keys.Select(k => k.Key).Distinct().Count();
            Assert.Equal(5, uniqueKeys);
        }
    }
}
