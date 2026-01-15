using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datra.Converters;
using Datra.SampleData.Models;
using Datra.Serializers;
using Xunit;
using Xunit.Abstractions;

namespace Datra.Tests
{
    /// <summary>
    /// Test-only POCO class for YAML polymorphic serialization roundtrip tests
    /// </summary>
    public class TestQuestDataYaml
    {
        public string Id { get; set; } = string.Empty;
        public QuestType Type { get; set; }
        public int RequiredLevel { get; set; }
        public int RewardGold { get; set; }
        public int RewardExp { get; set; }
        public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();
    }

    #region Dictionary Polymorphism Test Classes

    /// <summary>
    /// Test class with Dictionary containing polymorphic values
    /// </summary>
    public class TestNodeWithEmbeddedObjectives
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, EmbeddedObjective> EmbeddedObjectives { get; set; } = new Dictionary<string, EmbeddedObjective>();
    }

    /// <summary>
    /// Container class with polymorphic property (similar to EmbeddedCondition)
    /// </summary>
    public class EmbeddedObjective
    {
        public EmbeddedObjective()
        {
            Id = string.Empty;
            Objective = null!;
        }

        public EmbeddedObjective(string id, QuestObjective objective)
        {
            Id = id;
            Objective = objective;
        }

        public string Id { get; set; }
        public QuestObjective Objective { get; set; }
    }

    #endregion

    public class PolymorphicYamlTests
    {
        private readonly ITestOutputHelper _output;
        private readonly YamlDataSerializer _serializer;

        public PolymorphicYamlTests(ITestOutputHelper output)
        {
            _output = output;
            // Register QuestObjective as polymorphic base type
            _serializer = new YamlDataSerializer(new[] { typeof(QuestObjective) });
        }

        // Note: LoadQuestsYaml tests removed - QuestsYaml.yaml replaced by Skills.yaml
        // See SkillDataYamlTests.cs for comprehensive YAML polymorphism tests

        [Fact]
        public void SerializeTestData_WithPolymorphicObjectives_ShouldIncludeTypeInfo()
        {
            // Arrange
            var quest = new TestQuestDataYaml
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
            var yaml = _serializer.SerializeSingle(quest);
            _output.WriteLine("Serialized YAML:");
            _output.WriteLine(yaml);

            // Assert - should contain $type for polymorphic objectives
            Assert.Contains("$type", yaml);
            Assert.Contains("KillObjective", yaml);
            Assert.Contains("CollectObjective", yaml);
        }

        [Fact]
        public void RoundTrip_WithPolymorphicObjectives_ShouldPreserveTypes()
        {
            // Arrange
            var originalQuest = new TestQuestDataYaml
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
            var yaml = _serializer.SerializeSingle(originalQuest);
            _output.WriteLine("Serialized YAML:");
            _output.WriteLine(yaml);

            var deserializedQuest = _serializer.DeserializeSingle<TestQuestDataYaml>(yaml);

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
        public void AllObjectiveTypes_ShouldRoundTripCorrectly()
        {
            // Arrange - test with all 4 objective types
            var quest = new TestQuestDataYaml
            {
                Id = "all_types_test",
                Type = QuestType.Main,
                RequiredLevel = 5,
                RewardGold = 1000,
                RewardExp = 500,
                Objectives = new List<QuestObjective>
                {
                    new KillObjective
                    {
                        Id = "kill_01",
                        Description = "Defeat enemies",
                        TargetEnemyId = "enemy_001",
                        RequiredCount = 10,
                        CurrentCount = 3
                    },
                    new CollectObjective
                    {
                        Id = "collect_01",
                        Description = "Gather items",
                        TargetItemId = 1001,
                        RequiredAmount = 5,
                        CurrentAmount = 2
                    },
                    new TalkObjective
                    {
                        Id = "talk_01",
                        Description = "Speak with NPC",
                        TargetNpcId = "npc_001",
                        DialogueKeys = new[] { "key1", "key2", "key3" }
                    },
                    new LocationObjective
                    {
                        Id = "location_01",
                        Description = "Reach destination",
                        LocationId = "loc_001",
                        Radius = 15.5f
                    }
                }
            };

            // Act
            var yaml = _serializer.SerializeSingle(quest);
            _output.WriteLine("Serialized YAML with all objective types:");
            _output.WriteLine(yaml);

            var deserialized = _serializer.DeserializeSingle<TestQuestDataYaml>(yaml);

            // Assert all types are preserved
            Assert.Equal(4, deserialized.Objectives.Count);
            Assert.IsType<KillObjective>(deserialized.Objectives[0]);
            Assert.IsType<CollectObjective>(deserialized.Objectives[1]);
            Assert.IsType<TalkObjective>(deserialized.Objectives[2]);
            Assert.IsType<LocationObjective>(deserialized.Objectives[3]);

            // Verify specific properties
            var killObj = (KillObjective)deserialized.Objectives[0];
            Assert.Equal(10, killObj.RequiredCount);
            Assert.Equal(3, killObj.CurrentCount);

            var collectObj = (CollectObjective)deserialized.Objectives[1];
            Assert.Equal(1001, collectObj.TargetItemId);
            Assert.Equal(5, collectObj.RequiredAmount);

            var talkObj = (TalkObjective)deserialized.Objectives[2];
            Assert.Equal("npc_001", talkObj.TargetNpcId);
            Assert.Equal(3, talkObj.DialogueKeys.Length);

            var locationObj = (LocationObjective)deserialized.Objectives[3];
            Assert.Equal("loc_001", locationObj.LocationId);
            Assert.Equal(15.5f, locationObj.Radius);

            _output.WriteLine("All objective types round-tripped successfully!");
        }

        [Fact]
        public void PortableTypeResolver_ShouldResolveTypesCorrectly()
        {
            // Arrange
            var resolver = new PortableTypeResolver();

            // Act & Assert
            var killType = resolver.ResolveType("Datra.SampleData.Models.KillObjective");
            Assert.NotNull(killType);
            Assert.Equal(typeof(KillObjective), killType);

            var collectType = resolver.ResolveType("Datra.SampleData.Models.CollectObjective");
            Assert.NotNull(collectType);
            Assert.Equal(typeof(CollectObjective), collectType);

            var talkType = resolver.ResolveType("Datra.SampleData.Models.TalkObjective");
            Assert.NotNull(talkType);
            Assert.Equal(typeof(TalkObjective), talkType);

            var locationAType = resolver.ResolveType("Datra.SampleData.Models.LocationObjective");
            Assert.NotNull(locationAType);
            Assert.Equal(typeof(LocationObjective), locationAType);

            _output.WriteLine("All types resolved correctly!");
        }

        [Fact]
        public void PortableTypeResolver_ShouldGetTypeNameCorrectly()
        {
            // Arrange
            var resolver = new PortableTypeResolver();

            // Act & Assert
            Assert.Equal("Datra.SampleData.Models.KillObjective", resolver.GetTypeName(typeof(KillObjective)));
            Assert.Equal("Datra.SampleData.Models.CollectObjective", resolver.GetTypeName(typeof(CollectObjective)));
            Assert.Equal("Datra.SampleData.Models.TalkObjective", resolver.GetTypeName(typeof(TalkObjective)));
            Assert.Equal("Datra.SampleData.Models.LocationObjective", resolver.GetTypeName(typeof(LocationObjective)));

            _output.WriteLine("All type names generated correctly!");
        }

        [Fact]
        public void PolymorphicConverter_Accepts_ShouldReturnTrueForCorrectTypes()
        {
            // Arrange
            var resolver = new PortableTypeResolver();
            var polymorphicTypes = new System.Collections.Generic.HashSet<System.Type> { typeof(QuestObjective) };
            var converter = new PolymorphicYamlTypeConverter(resolver, polymorphicTypes);

            // Act & Assert - Direct polymorphic types
            Assert.True(converter.Accepts(typeof(QuestObjective)), "Should accept QuestObjective (registered base type)");
            Assert.True(converter.Accepts(typeof(KillObjective)), "Should accept KillObjective (derived from QuestObjective)");
            Assert.True(converter.Accepts(typeof(CollectObjective)), "Should accept CollectObjective (derived from QuestObjective)");

            // Act & Assert - Collections of polymorphic types
            Assert.True(converter.Accepts(typeof(List<QuestObjective>)), "Should accept List<QuestObjective>");
            Assert.True(converter.Accepts(typeof(QuestObjective[])), "Should accept QuestObjective[]");

            // Act & Assert - Classes with polymorphic properties
            Assert.True(converter.Accepts(typeof(TestQuestDataYaml)), "Should accept TestQuestDataYaml (has List<QuestObjective> property)");

            _output.WriteLine("All Accepts checks passed!");
        }

        [Fact]
        public void SerializeSinglePolymorphicObject_DirectlyWithDeclaredType_ShouldWork()
        {
            // Note: YamlDotNet doesn't invoke IYamlTypeConverter for root objects when the
            // actual type matches the serialization call. This is a limitation of YamlDotNet.
            // Polymorphic serialization works correctly when objects are within collections
            // or as properties of other objects.

            // Arrange - serialize a single polymorphic object directly
            var killObj = new KillObjective
            {
                Id = "kill_test",
                Description = "Test kill",
                TargetEnemyId = "enemy_test",
                RequiredCount = 5
            };

            // Act
            var resolver = new PortableTypeResolver();
            var polymorphicTypes = new System.Collections.Generic.HashSet<System.Type> { typeof(QuestObjective) };

            var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)
                .WithTypeConverter(new PolymorphicYamlTypeConverter(resolver, polymorphicTypes))
                .Build();

            var yaml = serializer.Serialize(killObj);
            _output.WriteLine("Serialized single KillObjective:");
            _output.WriteLine(yaml);

            // Assert - properties are serialized correctly (even without $type for single root objects)
            Assert.Contains("TargetEnemyId: enemy_test", yaml);
            Assert.Contains("RequiredCount: 5", yaml);
            Assert.Contains("Id: kill_test", yaml);
        }

        [Fact]
        public void SerializeListOfPolymorphicObjects_ShouldIncludeTypeInfo()
        {
            // Arrange
            var objectives = new List<QuestObjective>
            {
                new KillObjective { Id = "kill_01", Description = "Kill", TargetEnemyId = "enemy", RequiredCount = 5 },
                new CollectObjective { Id = "collect_01", Description = "Collect", TargetItemId = 1001, RequiredAmount = 3 }
            };

            // Act
            var resolver = new PortableTypeResolver();
            var polymorphicTypes = new System.Collections.Generic.HashSet<System.Type> { typeof(QuestObjective) };

            var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)
                .WithTypeConverter(new PolymorphicYamlTypeConverter(resolver, polymorphicTypes))
                .Build();

            var yaml = serializer.Serialize(objectives);
            _output.WriteLine("Serialized List<QuestObjective>:");
            _output.WriteLine(yaml);

            // Assert
            Assert.Contains("$type", yaml);
            Assert.Contains("KillObjective", yaml);
            Assert.Contains("CollectObjective", yaml);
        }

        // Note: JsonAndYaml_ShouldProduceSameDeserializationResult test removed - QuestsYaml.yaml replaced by Skills.yaml
        // See SkillDataYamlTests.cs for comprehensive YAML polymorphism tests

        /// <summary>
        /// Helper method to deserialize quest list from YAML.
        /// </summary>
        private List<TestQuestDataYaml> DeserializeQuestList(string yamlContent)
        {
            // Use a temporary deserializer that can handle List<TestQuestDataYaml>
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .WithTypeConverter(new PolymorphicYamlTypeConverter(
                    new PortableTypeResolver(),
                    new System.Collections.Generic.HashSet<System.Type> { typeof(QuestObjective) }))
                .Build();

            using var reader = new StringReader(yamlContent);
            return deserializer.Deserialize<List<TestQuestDataYaml>>(reader)
                   ?? new List<TestQuestDataYaml>();
        }

        #region Dictionary Polymorphism Tests

        [Fact]
        public void Dictionary_WithPolymorphicValues_ShouldSerializeWithTypeInfo()
        {
            // Arrange
            var node = new TestNodeWithEmbeddedObjectives
            {
                Id = "node_gate",
                Name = "Gate Node",
                EmbeddedObjectives = new Dictionary<string, EmbeddedObjective>
                {
                    ["obj_kill"] = new EmbeddedObjective("obj_kill", new KillObjective
                    {
                        Id = "kill_01",
                        Description = "Kill enemies",
                        TargetEnemyId = "enemy_dragon",
                        RequiredCount = 3
                    }),
                    ["obj_collect"] = new EmbeddedObjective("obj_collect", new CollectObjective
                    {
                        Id = "collect_01",
                        Description = "Collect items",
                        TargetItemId = 1001,
                        RequiredAmount = 5
                    })
                }
            };

            // Act
            var yaml = _serializer.SerializeSingle(node);
            _output.WriteLine("Serialized YAML with Dictionary:");
            _output.WriteLine(yaml);

            // Assert
            Assert.Contains("obj_kill", yaml);
            Assert.Contains("obj_collect", yaml);
            Assert.Contains("$type", yaml);
            Assert.Contains("KillObjective", yaml);
            Assert.Contains("CollectObjective", yaml);
        }

        [Fact]
        public void Dictionary_WithPolymorphicValues_ShouldRoundTripCorrectly()
        {
            // Arrange
            var originalNode = new TestNodeWithEmbeddedObjectives
            {
                Id = "node_test",
                Name = "Test Node",
                EmbeddedObjectives = new Dictionary<string, EmbeddedObjective>
                {
                    ["talk_obj"] = new EmbeddedObjective("talk_obj", new TalkObjective
                    {
                        Id = "talk_01",
                        Description = "Talk to NPC",
                        TargetNpcId = "npc_elder",
                        DialogueKeys = new[] { "greet", "farewell" }
                    }),
                    ["location_obj"] = new EmbeddedObjective("location_obj", new LocationObjective
                    {
                        Id = "location_01",
                        Description = "Go to location",
                        LocationId = "castle_entrance",
                        Radius = 15.5f
                    })
                }
            };

            // Act
            var yaml = _serializer.SerializeSingle(originalNode);
            _output.WriteLine("Serialized YAML:");
            _output.WriteLine(yaml);

            var deserialized = _serializer.DeserializeSingle<TestNodeWithEmbeddedObjectives>(yaml);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(originalNode.Id, deserialized.Id);
            Assert.Equal(originalNode.Name, deserialized.Name);
            Assert.Equal(2, deserialized.EmbeddedObjectives.Count);

            // Check Dictionary keys
            Assert.True(deserialized.EmbeddedObjectives.ContainsKey("talk_obj"));
            Assert.True(deserialized.EmbeddedObjectives.ContainsKey("location_obj"));

            // Check polymorphic types are preserved
            var talkObj = deserialized.EmbeddedObjectives["talk_obj"];
            Assert.Equal("talk_obj", talkObj.Id);
            Assert.IsType<TalkObjective>(talkObj.Objective);
            var talk = (TalkObjective)talkObj.Objective;
            Assert.Equal("npc_elder", talk.TargetNpcId);
            Assert.Equal(2, talk.DialogueKeys.Length);

            var locationObj = deserialized.EmbeddedObjectives["location_obj"];
            Assert.Equal("location_obj", locationObj.Id);
            Assert.IsType<LocationObjective>(locationObj.Objective);
            var location = (LocationObjective)locationObj.Objective;
            Assert.Equal("castle_entrance", location.LocationId);
            Assert.Equal(15.5f, location.Radius);

            _output.WriteLine("Dictionary round-trip test passed!");
        }

        [Fact]
        public void Dictionary_Empty_ShouldRoundTripCorrectly()
        {
            // Arrange
            var node = new TestNodeWithEmbeddedObjectives
            {
                Id = "empty_node",
                Name = "Empty Node",
                EmbeddedObjectives = new Dictionary<string, EmbeddedObjective>()
            };

            // Act
            var yaml = _serializer.SerializeSingle(node);
            _output.WriteLine("Serialized YAML with empty dictionary:");
            _output.WriteLine(yaml);

            var deserialized = _serializer.DeserializeSingle<TestNodeWithEmbeddedObjectives>(yaml);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(node.Id, deserialized.Id);
            Assert.Empty(deserialized.EmbeddedObjectives);

            _output.WriteLine("Empty dictionary round-trip test passed!");
        }

        [Fact]
        public void PolymorphicConverter_Accepts_ShouldReturnTrueForDictionary()
        {
            // Arrange
            var resolver = new PortableTypeResolver();
            var polymorphicTypes = new System.Collections.Generic.HashSet<System.Type> { typeof(QuestObjective) };
            var converter = new PolymorphicYamlTypeConverter(resolver, polymorphicTypes);

            // Act & Assert
            // Dictionary with value type that has polymorphic properties
            Assert.True(converter.Accepts(typeof(Dictionary<string, EmbeddedObjective>)),
                "Should accept Dictionary<string, EmbeddedObjective> (value has polymorphic properties)");

            // Dictionary with direct polymorphic value type
            Assert.True(converter.Accepts(typeof(Dictionary<string, QuestObjective>)),
                "Should accept Dictionary<string, QuestObjective> (value is polymorphic type)");

            // Class containing Dictionary with polymorphic values
            Assert.True(converter.Accepts(typeof(TestNodeWithEmbeddedObjectives)),
                "Should accept TestNodeWithEmbeddedObjectives (contains Dictionary with polymorphic values)");

            _output.WriteLine("Dictionary Accepts checks passed!");
        }

        #endregion

        #region LocaleRef/NestedLocaleRef Tests

        /// <summary>
        /// Test class with LocaleRef property
        /// </summary>
        public class TestNodeWithLocaleRef
        {
            public string Id { get; set; } = string.Empty;
            public Datra.DataTypes.LocaleRef DisplayName { get; set; }
            public Datra.DataTypes.LocaleRef Description { get; set; }
        }

        /// <summary>
        /// Test class with NestedLocaleRef property (simulating Node.Name)
        /// </summary>
        public class TestNodeWithNestedLocaleRef
        {
            public string Id { get; set; } = string.Empty;
            public Datra.Localization.NestedLocaleRef Name => _name;
            private static readonly Datra.Localization.NestedLocaleRef _name =
                Datra.Localization.NestedLocaleRef.Create("Nodes", "Name");
        }

        [Fact]
        public void LocaleRef_ShouldSerializeAsString()
        {
            // Arrange
            var node = new TestNodeWithLocaleRef
            {
                Id = "test_node",
                DisplayName = new Datra.DataTypes.LocaleRef { Key = "UI.Test.Name" },
                Description = new Datra.DataTypes.LocaleRef { Key = "UI.Test.Desc" }
            };

            // Act
            var yaml = _serializer.SerializeSingle(node);
            _output.WriteLine("Serialized LocaleRef:");
            _output.WriteLine(yaml);

            // Assert - LocaleRef should be serialized as simple string, not object
            Assert.Contains("DisplayName: UI.Test.Name", yaml);
            Assert.Contains("Description: UI.Test.Desc", yaml);
            Assert.DoesNotContain("Key:", yaml);
            Assert.DoesNotContain("HasValue:", yaml);
        }

        [Fact]
        public void LocaleRef_ShouldRoundTripCorrectly()
        {
            // Arrange
            var original = new TestNodeWithLocaleRef
            {
                Id = "roundtrip_test",
                DisplayName = new Datra.DataTypes.LocaleRef { Key = "Item.Sword.Name" },
                Description = new Datra.DataTypes.LocaleRef { Key = "Item.Sword.Desc" }
            };

            // Act
            var yaml = _serializer.SerializeSingle(original);
            var deserialized = _serializer.DeserializeSingle<TestNodeWithLocaleRef>(yaml);

            // Assert
            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.DisplayName.Key, deserialized.DisplayName.Key);
            Assert.Equal(original.Description.Key, deserialized.Description.Key);
            _output.WriteLine("LocaleRef round-trip passed!");
        }

        [Fact]
        public void LocaleRef_Empty_ShouldSerializeAsEmptyString()
        {
            // Arrange
            var node = new TestNodeWithLocaleRef
            {
                Id = "empty_locale",
                DisplayName = default,
                Description = default
            };

            // Act
            var yaml = _serializer.SerializeSingle(node);
            _output.WriteLine("Serialized empty LocaleRef:");
            _output.WriteLine(yaml);

            // Assert - Empty LocaleRef should be empty string
            Assert.DoesNotContain("Key:", yaml);
        }

        [Fact]
        public void NestedLocaleRef_ShouldSerializeAsEmptyString()
        {
            // Arrange
            var node = new TestNodeWithNestedLocaleRef
            {
                Id = "nested_test"
            };

            // Act
            var yaml = _serializer.SerializeSingle(node);
            _output.WriteLine("Serialized NestedLocaleRef:");
            _output.WriteLine(yaml);

            // Assert - NestedLocaleRef should NOT expose internal properties
            Assert.DoesNotContain("PathTemplate:", yaml);
            Assert.DoesNotContain("Segments:", yaml);
            Assert.DoesNotContain("HasValue:", yaml);
            Assert.DoesNotContain("PropertyName:", yaml);
            Assert.DoesNotContain("Depth:", yaml);
        }

        #endregion
    }
}
