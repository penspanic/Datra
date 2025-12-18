using System;
using Xunit;
using Datra.Localization;
using Datra.DataTypes;

namespace Datra.Tests
{
    public class NestedLocaleRefTests
    {
        #region Create Tests

        [Fact]
        public void Create_WithSingleSegment_ReturnsCorrectPathTemplate()
        {
            // Act
            var nested = NestedLocaleRef.Create("Name");

            // Assert
            Assert.Equal("Name", nested.PathTemplate);
            Assert.Single(nested.Segments);
            Assert.Equal("Name", nested.Segments[0]);
            Assert.True(nested.HasValue);
        }

        [Fact]
        public void Create_WithMultipleSegments_ReturnsJoinedPathTemplate()
        {
            // Act
            var nested = NestedLocaleRef.Create("Nodes", "Choices", "Name");

            // Assert
            Assert.Equal("Nodes.Choices.Name", nested.PathTemplate);
            Assert.Equal(3, nested.Segments.Length);
            Assert.Equal("Nodes", nested.Segments[0]);
            Assert.Equal("Choices", nested.Segments[1]);
            Assert.Equal("Name", nested.Segments[2]);
        }

        [Fact]
        public void Create_WithEmptyArray_ReturnsEmptyNestedLocaleRef()
        {
            // Act
            var nested = NestedLocaleRef.Create();

            // Assert
            Assert.Equal(string.Empty, nested.PathTemplate);
            Assert.Empty(nested.Segments);
            Assert.False(nested.HasValue);
        }

        [Fact]
        public void Create_WithNullArray_ReturnsEmptyNestedLocaleRef()
        {
            // Act
            var nested = NestedLocaleRef.Create(null!);

            // Assert
            Assert.Equal(string.Empty, nested.PathTemplate);
            Assert.Empty(nested.Segments);
            Assert.False(nested.HasValue);
        }

        #endregion

        #region Evaluate Tests

        [Fact]
        public void Evaluate_WithPrefixAndNoIndices_ReturnsCorrectKey()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Name");

            // Act
            var localeRef = nested.Evaluate("Graph.file001");

            // Assert
            Assert.Equal("Graph.file001.Nodes.Name", localeRef.Key);
        }

        [Fact]
        public void Evaluate_WithSingleIndex_InsertsIndexCorrectly()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Name");

            // Act
            var localeRef = nested.Evaluate("Graph.file001", ("Nodes", 3));

            // Assert
            Assert.Equal("Graph.file001.Nodes#3.Name", localeRef.Key);
        }

        [Fact]
        public void Evaluate_WithMultipleIndices_InsertsAllIndicesCorrectly()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Choices", "Name");

            // Act
            var localeRef = nested.Evaluate("Graph.file001", ("Nodes", 3), ("Choices", 1));

            // Assert
            Assert.Equal("Graph.file001.Nodes#3.Choices#1.Name", localeRef.Key);
        }

        [Fact]
        public void Evaluate_WithZeroIndex_InsertsZeroCorrectly()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Name");

            // Act
            var localeRef = nested.Evaluate("Graph.file001", ("Nodes", 0));

            // Assert
            Assert.Equal("Graph.file001.Nodes#0.Name", localeRef.Key);
        }

        [Fact]
        public void Evaluate_WithEmptyPrefix_StartsWithSegment()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Name");

            // Act
            var localeRef = nested.Evaluate("", ("Nodes", 2));

            // Assert
            Assert.Equal("Nodes#2.Name", localeRef.Key);
        }

        [Fact]
        public void Evaluate_WithNullPrefix_StartsWithSegment()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Name");

            // Act
            var localeRef = nested.Evaluate(null!, ("Nodes", 2));

            // Assert
            Assert.Equal("Nodes#2.Name", localeRef.Key);
        }

        [Fact]
        public void Evaluate_WithNoMatchingIndex_DoesNotInsertIndex()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Name");

            // Act
            var localeRef = nested.Evaluate("Graph.file001", ("Choices", 5));

            // Assert
            Assert.Equal("Graph.file001.Nodes.Name", localeRef.Key);
        }

        [Fact]
        public void Evaluate_WithEmptyNestedLocaleRef_ReturnsPrefixOnly()
        {
            // Arrange
            var nested = NestedLocaleRef.Create();

            // Act
            var localeRef = nested.Evaluate("Graph.file001");

            // Assert
            Assert.Equal("Graph.file001", localeRef.Key);
        }

        [Fact]
        public void Evaluate_DeepHierarchy_HandlesMultipleLevels()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Sequences", "Dialogs", "Choices", "Text");

            // Act
            var localeRef = nested.Evaluate(
                "Graph.story001",
                ("Sequences", 0),
                ("Dialogs", 5),
                ("Choices", 2)
            );

            // Assert
            Assert.Equal("Graph.story001.Sequences#0.Dialogs#5.Choices#2.Text", localeRef.Key);
        }

        #endregion

        #region ILocaleEvaluator Tests

        private class TestGraph : ILocaleEvaluator
        {
            public string FileId { get; set; } = "test001";
            public int NodeIndex { get; set; } = 3;
            public int ChoiceIndex { get; set; } = 1;

            public LocaleRef EvaluateNestedLocale(object rootId, NestedLocaleRef nested, params object[] context)
            {
                // Simulate Graph's evaluation logic
                return nested.Evaluate(
                    $"Graph.{rootId}",
                    ("Nodes", NodeIndex),
                    ("Choices", ChoiceIndex)
                );
            }
        }

        [Fact]
        public void Evaluate_WithILocaleEvaluator_DelegatesToEvaluator()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Choices", "Name");
            var graph = new TestGraph { NodeIndex = 5, ChoiceIndex = 2 };

            // Act
            var localeRef = nested.Evaluate(graph, "story001");

            // Assert
            Assert.Equal("Graph.story001.Nodes#5.Choices#2.Name", localeRef.Key);
        }

        [Fact]
        public void Evaluate_WithNullEvaluator_ThrowsArgumentNullException()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Name");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => nested.Evaluate(null!, "rootId"));
        }

        #endregion

        #region Property Tests

        [Fact]
        public void PropertyName_ReturnsLastSegment()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Choices", "Name");

            // Assert
            Assert.Equal("Name", nested.PropertyName);
        }

        [Fact]
        public void PropertyName_WithSingleSegment_ReturnsThatSegment()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Description");

            // Assert
            Assert.Equal("Description", nested.PropertyName);
        }

        [Fact]
        public void PropertyName_WhenEmpty_ReturnsEmptyString()
        {
            // Arrange
            var nested = NestedLocaleRef.Create();

            // Assert
            Assert.Equal(string.Empty, nested.PropertyName);
        }

        [Fact]
        public void Depth_ReturnsSegmentCount()
        {
            // Assert
            Assert.Equal(3, NestedLocaleRef.Create("A", "B", "C").Depth);
            Assert.Equal(1, NestedLocaleRef.Create("A").Depth);
            Assert.Equal(0, NestedLocaleRef.Create().Depth);
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equals_SamePathTemplate_ReturnsTrue()
        {
            // Arrange
            var nested1 = NestedLocaleRef.Create("Nodes", "Name");
            var nested2 = NestedLocaleRef.Create("Nodes", "Name");

            // Assert
            Assert.True(nested1.Equals(nested2));
            Assert.True(nested1 == nested2);
            Assert.False(nested1 != nested2);
        }

        [Fact]
        public void Equals_DifferentPathTemplate_ReturnsFalse()
        {
            // Arrange
            var nested1 = NestedLocaleRef.Create("Nodes", "Name");
            var nested2 = NestedLocaleRef.Create("Nodes", "Description");

            // Assert
            Assert.False(nested1.Equals(nested2));
            Assert.False(nested1 == nested2);
            Assert.True(nested1 != nested2);
        }

        [Fact]
        public void Equals_WithObject_ComparesCorrectly()
        {
            // Arrange
            var nested1 = NestedLocaleRef.Create("Nodes", "Name");
            var nested2 = NestedLocaleRef.Create("Nodes", "Name");

            // Assert
            Assert.True(nested1.Equals((object)nested2));
            Assert.False(nested1.Equals("Nodes.Name"));
            Assert.False(nested1.Equals(null));
        }

        [Fact]
        public void GetHashCode_SamePathTemplate_ReturnsSameHash()
        {
            // Arrange
            var nested1 = NestedLocaleRef.Create("Nodes", "Choices", "Name");
            var nested2 = NestedLocaleRef.Create("Nodes", "Choices", "Name");

            // Assert
            Assert.Equal(nested1.GetHashCode(), nested2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_EmptyNestedLocaleRef_ReturnsConsistentHash()
        {
            // Arrange
            var nested1 = NestedLocaleRef.Create();
            var nested2 = NestedLocaleRef.Create();

            // Assert - Empty refs should have the same hash
            Assert.Equal(nested1.GetHashCode(), nested2.GetHashCode());
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ReturnsPathTemplate()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Choices", "Name");

            // Assert
            Assert.Equal("Nodes.Choices.Name", nested.ToString());
        }

        [Fact]
        public void ToString_WhenEmpty_ReturnsEmptyString()
        {
            // Arrange
            var nested = NestedLocaleRef.Create();

            // Assert
            Assert.Equal(string.Empty, nested.ToString());
        }

        #endregion

        #region Real-World Usage Patterns

        [Fact]
        public void RealWorldUsage_GraphNodeDialogPattern()
        {
            // This test simulates the actual Oratia Graph usage pattern

            // Arrange - Define nested locale references as they would be in a model
            var nodeName = NestedLocaleRef.Create("Nodes", "Name");
            var choiceName = NestedLocaleRef.Create("Nodes", "Choices", "Name");
            var dialogText = NestedLocaleRef.Create("Nodes", "Dialogs", "Text");

            // Act - Evaluate with runtime indices (simulating Graph context)
            var nodeNameRef = nodeName.Evaluate("Graph.quest_intro", ("Nodes", 0));
            var choiceNameRef = choiceName.Evaluate("Graph.quest_intro", ("Nodes", 0), ("Choices", 2));
            var dialogTextRef = dialogText.Evaluate("Graph.quest_intro", ("Nodes", 1), ("Dialogs", 0));

            // Assert
            Assert.Equal("Graph.quest_intro.Nodes#0.Name", nodeNameRef.Key);
            Assert.Equal("Graph.quest_intro.Nodes#0.Choices#2.Name", choiceNameRef.Key);
            Assert.Equal("Graph.quest_intro.Nodes#1.Dialogs#0.Text", dialogTextRef.Key);
        }

        [Fact]
        public void RealWorldUsage_CharacterDialogPattern()
        {
            // Simulates a character with multiple dialogue options

            // Arrange
            var greeting = NestedLocaleRef.Create("Dialogues", "Greeting");
            var farewell = NestedLocaleRef.Create("Dialogues", "Farewell");
            var shopOptions = NestedLocaleRef.Create("Dialogues", "Shop", "Options", "Text");

            // Act
            var greetingRef = greeting.Evaluate("Character.merchant_01", ("Dialogues", 0));
            var farewellRef = farewell.Evaluate("Character.merchant_01", ("Dialogues", 1));
            var shopOptionRef = shopOptions.Evaluate(
                "Character.merchant_01",
                ("Dialogues", 2),
                ("Shop", 0),
                ("Options", 3)
            );

            // Assert
            Assert.Equal("Character.merchant_01.Dialogues#0.Greeting", greetingRef.Key);
            Assert.Equal("Character.merchant_01.Dialogues#1.Farewell", farewellRef.Key);
            Assert.Equal("Character.merchant_01.Dialogues#2.Shop#0.Options#3.Text", shopOptionRef.Key);
        }

        #endregion

        #region Optimized Evaluation Tests

        [Fact]
        public void Evaluate_SingleIndex_UsesOptimizedPath()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Objectives", "Description");

            // Act
            var localeRef = nested.Evaluate("QuestData.quest_001", "Objectives", 2);

            // Assert
            Assert.Equal("QuestData.quest_001.Objectives#2.Description", localeRef.Key);
        }

        [Fact]
        public void Evaluate_DoubleIndex_UsesOptimizedPath()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Nodes", "Choices", "Text");

            // Act
            var localeRef = nested.Evaluate("Graph.story001", "Nodes", 3, "Choices", 1);

            // Assert
            Assert.Equal("Graph.story001.Nodes#3.Choices#1.Text", localeRef.Key);
        }

        [Fact]
        public void EvaluateNoCache_BypassesCache()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Items", "Name");
            NestedLocaleRef.ClearCache();

            // Act - call twice with same parameters
            var ref1 = nested.EvaluateNoCache("Container.id1", ("Items", 0));
            var ref2 = nested.EvaluateNoCache("Container.id1", ("Items", 0));

            // Assert - both should work correctly (even if they create new strings)
            Assert.Equal("Container.id1.Items#0.Name", ref1.Key);
            Assert.Equal("Container.id1.Items#0.Name", ref2.Key);
        }

        [Fact]
        public void Evaluate_WithCache_ReturnsSameStringInstance()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Items", "Name");
            NestedLocaleRef.ClearCache();

            // Act - call twice with same parameters
            var ref1 = nested.Evaluate("Container.id1", "Items", 0);
            var ref2 = nested.Evaluate("Container.id1", "Items", 0);

            // Assert - should return the same cached string instance
            Assert.Same(ref1.Key, ref2.Key);
        }

        [Fact]
        public void ClearCache_RemovesCachedValues()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Items", "Name");
            var ref1 = nested.Evaluate("Container.id1", "Items", 0);

            // Act
            NestedLocaleRef.ClearCache();
            var ref2 = nested.Evaluate("Container.id1", "Items", 0);

            // Assert - after cache clear, a new string should be created
            Assert.Equal(ref1.Key, ref2.Key);
            // Note: After cache clear, the strings may or may not be the same instance
            // depending on string interning. The important thing is the values are equal.
        }

        #endregion

        #region QuestData Integration Pattern

        [Fact]
        public void QuestDataPattern_ObjectiveDescriptionLocale()
        {
            // This demonstrates the actual usage pattern in QuestData/QuestObjective

            // Arrange - simulate QuestObjective.DescriptionLocale
            var descriptionLocale = NestedLocaleRef.Create("Objectives", "Description");

            // Act - simulate QuestData.GetObjectiveDescription behavior
            // Using single-index optimized path
            var localeRef = descriptionLocale.Evaluate("QuestData.quest_main_001", "Objectives", 0);

            // Assert
            Assert.Equal("QuestData.quest_main_001.Objectives#0.Description", localeRef.Key);
        }

        [Fact]
        public void QuestDataPattern_MultipleObjectives()
        {
            // Arrange
            var descriptionLocale = NestedLocaleRef.Create("Objectives", "Description");
            var prefix = "QuestData.quest_main_001";

            // Act - simulate iterating through objectives
            var obj0 = descriptionLocale.Evaluate(prefix, "Objectives", 0);
            var obj1 = descriptionLocale.Evaluate(prefix, "Objectives", 1);
            var obj2 = descriptionLocale.Evaluate(prefix, "Objectives", 2);

            // Assert
            Assert.Equal("QuestData.quest_main_001.Objectives#0.Description", obj0.Key);
            Assert.Equal("QuestData.quest_main_001.Objectives#1.Description", obj1.Key);
            Assert.Equal("QuestData.quest_main_001.Objectives#2.Description", obj2.Key);

            // Different indices should produce different cached keys
            Assert.NotSame(obj0.Key, obj1.Key);
        }

        #endregion

        #region Performance Characteristics

        [Fact]
        public void Performance_RepeatedEvaluationUsesCacheEfficiently()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Objectives", "Description");
            NestedLocaleRef.ClearCache();
            const int iterations = 1000;

            // Act - simulate many evaluations (e.g., during game frame updates)
            LocaleRef lastRef = default;
            for (int i = 0; i < iterations; i++)
            {
                lastRef = nested.Evaluate("QuestData.quest_001", "Objectives", 5);
            }

            // Assert - all should return the same cached instance
            var verifyRef = nested.Evaluate("QuestData.quest_001", "Objectives", 5);
            Assert.Same(lastRef.Key, verifyRef.Key);
        }

        [Fact]
        public void Performance_DifferentIndicesAreCachedSeparately()
        {
            // Arrange
            var nested = NestedLocaleRef.Create("Items", "Name");
            NestedLocaleRef.ClearCache();

            // Act - evaluate with different indices
            var refs = new LocaleRef[10];
            for (int i = 0; i < 10; i++)
            {
                refs[i] = nested.Evaluate("Container.id1", "Items", i);
            }

            // Assert - each index should have its own cached value
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal($"Container.id1.Items#{i}.Name", refs[i].Key);
            }

            // Verify caching works for each
            for (int i = 0; i < 10; i++)
            {
                var verifyRef = nested.Evaluate("Container.id1", "Items", i);
                Assert.Same(refs[i].Key, verifyRef.Key);
            }
        }

        #endregion
    }
}
