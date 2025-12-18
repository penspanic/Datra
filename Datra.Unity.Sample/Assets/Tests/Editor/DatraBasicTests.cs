using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Datra.SampleData.Generated;
using Datra.SampleData.Models;
using Datra.Serializers;
using Datra.Unity.Editor.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Basic tests for Datra integration in Unity.
    /// These tests verify that the generated code compiles and works correctly.
    /// </summary>
    public class DatraBasicTests
    {
        private const string SampleDataBasePath = "Packages/com.penspanic.datra.sampledata/Resources";

        [Test]
        public void DataContext_CanBeCreated()
        {
            // Arrange & Act
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Assert
            Assert.IsNotNull(context);
            Assert.IsNotNull(context.Character);
            Assert.IsNotNull(context.Item);
        }

        [Test]
        public void CharacterData_TypeIsGenerated()
        {
            // Verify that the CharacterData type exists and has expected properties
            var type = typeof(CharacterData);

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetProperty("Id"));
            Assert.IsNotNull(type.GetProperty("Level"));
            Assert.IsNotNull(type.GetProperty("Health"));
        }

        [Test]
        public void CharacterData_HasNestedTypeProperty()
        {
            // Verify nested type support (PooledPrefab)
            var type = typeof(CharacterData);
            var testPooledPrefabProp = type.GetProperty("TestPooledPrefab");

            Assert.IsNotNull(testPooledPrefabProp, "TestPooledPrefab property should exist");
            Assert.AreEqual(typeof(PooledPrefab), testPooledPrefabProp.PropertyType);
        }

        [Test]
        public void PooledPrefab_HasExpectedFields()
        {
            // Verify PooledPrefab struct has expected fields
            var type = typeof(PooledPrefab);

            Assert.IsNotNull(type.GetField("Path"), "Path field should exist");
            Assert.IsNotNull(type.GetField("InitialCount"), "InitialCount field should exist");
            Assert.IsNotNull(type.GetField("MaxCount"), "MaxCount field should exist");
        }

        [UnityTest]
        public IEnumerator DataContext_CanLoadData()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            // Assert - check if data was loaded
            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.Message}");
            }

            Assert.Greater(context.Character.Count, 0, "Should have loaded at least one character");
        }

        [UnityTest]
        public IEnumerator CharacterData_NestedType_IsDeserialized()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.Message}");
            }

            // Assert - verify nested type is properly deserialized
            Assert.Greater(context.Character.Count, 0);

            var enumerator = context.Character.Values.GetEnumerator();
            enumerator.MoveNext();
            var character = enumerator.Current;

            // TestPooledPrefab should be initialized (not null/default for struct)
            Assert.IsNotNull(character.TestPooledPrefab.Path,
                "Nested type Path should be deserialized");
        }

        [Test]
        public void CsvSerializer_IsGenerated()
        {
            // Verify that the serializer class was generated
            var serializerType = typeof(CharacterDataSerializer);

            Assert.IsNotNull(serializerType);

            // Check for expected methods
            var deserializeMethod = serializerType.GetMethod("DeserializeCsv");
            var serializeMethod = serializerType.GetMethod("SerializeCsv");

            Assert.IsNotNull(deserializeMethod, "DeserializeCsv method should exist");
            Assert.IsNotNull(serializeMethod, "SerializeCsv method should exist");
        }

        #region Polymorphic JSON Tests

        [Test]
        public void QuestData_TypeIsGenerated()
        {
            // Verify that QuestData and QuestObjective types exist
            var questType = typeof(QuestData);
            var objectiveType = typeof(QuestObjective);

            Assert.IsNotNull(questType);
            Assert.IsNotNull(objectiveType);
            Assert.IsTrue(objectiveType.IsAbstract, "QuestObjective should be abstract");
        }

        [Test]
        public void QuestObjective_DerivedTypesExist()
        {
            // Verify all derived types exist
            Assert.IsNotNull(typeof(KillObjective));
            Assert.IsNotNull(typeof(CollectObjective));
            Assert.IsNotNull(typeof(TalkObjective));
            Assert.IsNotNull(typeof(LocationObjective));

            // Verify inheritance
            Assert.IsTrue(typeof(QuestObjective).IsAssignableFrom(typeof(KillObjective)));
            Assert.IsTrue(typeof(QuestObjective).IsAssignableFrom(typeof(CollectObjective)));
            Assert.IsTrue(typeof(QuestObjective).IsAssignableFrom(typeof(TalkObjective)));
            Assert.IsTrue(typeof(QuestObjective).IsAssignableFrom(typeof(LocationObjective)));
        }

        [UnityTest]
        public IEnumerator QuestData_CanLoadPolymorphicObjectives()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            // Assert
            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message ?? loadTask.Exception?.Message}");
            }

            Assert.Greater(context.Quest.Count, 0, "Should have loaded at least one quest");
            Debug.Log($"Loaded {context.Quest.Count} quests");
        }

        [UnityTest]
        public IEnumerator QuestData_PolymorphicObjectives_AreCorrectTypes()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message ?? loadTask.Exception?.Message}");
            }

            // Assert - check quest_main_001 has correct objective types
            Assert.IsTrue(context.Quest.TryGetValue("quest_main_001", out var quest),
                "Should find quest_main_001");

            Assert.IsNotNull(quest.Objectives, "Objectives should not be null");
            Assert.AreEqual(2, quest.Objectives.Count, "Should have 2 objectives");

            // First objective should be TalkObjective
            Assert.IsInstanceOf<TalkObjective>(quest.Objectives[0],
                $"First objective should be TalkObjective, got {quest.Objectives[0]?.GetType().Name}");

            // Second objective should be KillObjective
            Assert.IsInstanceOf<KillObjective>(quest.Objectives[1],
                $"Second objective should be KillObjective, got {quest.Objectives[1]?.GetType().Name}");

            var talkObj = (TalkObjective)quest.Objectives[0];
            Assert.AreEqual("npc_elder_001", talkObj.TargetNpcId);

            var killObj = (KillObjective)quest.Objectives[1];
            Assert.AreEqual("enemy_slime", killObj.TargetEnemyId);
            Assert.AreEqual(5, killObj.RequiredCount);

            Debug.Log("Polymorphic objectives deserialized correctly!");
        }

        [UnityTest]
        public IEnumerator QuestData_AllObjectiveTypes_CanBeLoaded()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message ?? loadTask.Exception?.Message}");
            }

            // Assert - verify all objective types are present across quests
            bool hasKillObjective = false;
            bool hasCollectObjective = false;
            bool hasTalkObjective = false;
            bool hasLocationObjective = false;

            foreach (var quest in context.Quest.Values)
            {
                foreach (var obj in quest.Objectives)
                {
                    if (obj is KillObjective) hasKillObjective = true;
                    if (obj is CollectObjective) hasCollectObjective = true;
                    if (obj is TalkObjective) hasTalkObjective = true;
                    if (obj is LocationObjective) hasLocationObjective = true;
                }
            }

            Assert.IsTrue(hasKillObjective, "Should have at least one KillObjective");
            Assert.IsTrue(hasCollectObjective, "Should have at least one CollectObjective");
            Assert.IsTrue(hasTalkObjective, "Should have at least one TalkObjective");
            Assert.IsTrue(hasLocationObjective, "Should have at least one LocationObjective");

            Debug.Log("All polymorphic objective types loaded successfully!");
        }

        [Test]
        public void JsonDataSerializer_SupportsPolymorphicTypes()
        {
            // Arrange
            var serializer = new JsonDataSerializer();
            var testData = new TestPolymorphicData
            {
                Items = new System.Collections.Generic.List<QuestObjective>
                {
                    new KillObjective { Id = "k1", TargetEnemyId = "goblin", RequiredCount = 5 },
                    new TalkObjective { Id = "t1", TargetNpcId = "merchant" }
                }
            };

            // Act
            var json = serializer.SerializeSingle(testData);
            var deserialized = serializer.DeserializeSingle<TestPolymorphicData>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(2, deserialized.Items.Count);
            Assert.IsInstanceOf<KillObjective>(deserialized.Items[0]);
            Assert.IsInstanceOf<TalkObjective>(deserialized.Items[1]);

            var killObj = (KillObjective)deserialized.Items[0];
            Assert.AreEqual("goblin", killObj.TargetEnemyId);

            Debug.Log($"Serialized JSON:\n{json}");
        }

        // Helper class for polymorphic serialization test
        private class TestPolymorphicData
        {
            public System.Collections.Generic.List<QuestObjective> Items { get; set; }
        }

        #endregion
    }
}
