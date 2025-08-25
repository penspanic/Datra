using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra;
using Datra.Configuration;
using Datra.DataTypes;
using Datra.Generated;
using Datra.Interfaces;
using Datra.SampleData.Models;
using Datra.Serializers;
using Xunit;
using Xunit.Abstractions;

namespace Datra.Tests
{
    public class DataRefGeneratorTests
    {
        private readonly ITestOutputHelper _output;

        public DataRefGeneratorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ItemData_Should_Have_Ref_Property()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Act - Get an item
            var item = context.Item.Values.FirstOrDefault();
            
            // Assert
            Assert.NotNull(item);
            _output.WriteLine($"Testing ItemData with Id: {item.Id}");
            
            // Check that Ref property exists and returns correct type
            var itemRef = item.Ref;
            Assert.NotNull(itemRef);
            Assert.IsType<IntDataRef<ItemData>>(itemRef);
            
            // Check that Ref has correct value
            Assert.Equal(item.Id, itemRef.Value);
            _output.WriteLine($"ItemData.Ref property works correctly. Ref.Value = {itemRef.Value}");
        }

        [Fact]
        public async Task CharacterData_Should_Have_Ref_Property()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Act - Get a character
            var character = context.Character.Values.FirstOrDefault();
            
            // Assert
            Assert.NotNull(character);
            _output.WriteLine($"Testing CharacterData with Id: {character.Id}");
            
            // Check that Ref property exists and returns correct type
            var characterRef = character.Ref;
            Assert.NotNull(characterRef);
            Assert.IsType<StringDataRef<CharacterData>>(characterRef);
            
            // Check that Ref has correct value
            Assert.Equal(character.Id, characterRef.Value);
            _output.WriteLine($"CharacterData.Ref property works correctly. Ref.Value = {characterRef.Value}");
        }

        [Fact]
        public async Task DataRef_Should_Evaluate_Correctly()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Act - Get an item and its ref
            var item = context.Item.Values.FirstOrDefault();
            Assert.NotNull(item);
            
            var itemRef = item.Ref;
            
            // Evaluate the ref
            var evaluatedItem = itemRef.Evaluate(context);
            
            // Assert - Should return the same item
            Assert.NotNull(evaluatedItem);
            Assert.Equal(item.Id, evaluatedItem.Id);
            Assert.Equal(item.Name, evaluatedItem.Name);
            _output.WriteLine($"DataRef.Evaluate() works correctly for ItemData with Id: {item.Id}");
        }

        [Fact]
        public async Task Multiple_Items_Should_Have_Unique_Refs()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Act - Get all items
            var items = context.Item.Values.ToList();
            Assert.True(items.Count > 1, "Need at least 2 items for this test");
            
            // Check that each item has a unique ref
            var item1 = items[0];
            var item2 = items[1];
            
            var ref1 = item1.Ref;
            var ref2 = item2.Ref;
            
            // Assert
            Assert.NotEqual(ref1.Value, ref2.Value);
            Assert.Equal(item1.Id, ref1.Value);
            Assert.Equal(item2.Id, ref2.Value);
            _output.WriteLine($"Multiple items have unique Refs: Item1.Id={item1.Id}, Item2.Id={item2.Id}");
        }

        [Fact]
        public async Task CharacterRef_Should_Evaluate_Correctly()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Act - Get a character and its ref
            var character = context.Character.Values.FirstOrDefault();
            Assert.NotNull(character);
            
            var characterRef = character.Ref;
            
            // Evaluate the ref
            var evaluatedCharacter = characterRef.Evaluate(context);
            
            // Assert - Should return the same character
            Assert.NotNull(evaluatedCharacter);
            Assert.Equal(character.Id, evaluatedCharacter.Id);
            Assert.Equal(character.Name, evaluatedCharacter.Name);
            _output.WriteLine($"DataRef.Evaluate() works correctly for CharacterData with Id: {character.Id}");
        }
    }
}