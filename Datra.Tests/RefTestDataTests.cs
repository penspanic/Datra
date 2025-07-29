using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.DataTypes;
using Datra.SampleData.Models;
using Xunit;

namespace Datra.Tests
{
    public class RefTestDataTests
    {
        
        [Fact]
        public void RefTestData_CsvSerialization_ShouldWorkCorrectly()
        {
            // Arrange
            var refTestData = new Dictionary<string, RefTestData>
            {
                ["ref1"] = new RefTestData("ref1", new StringDataRef<CharacterData> { Value = "char_001" }, new IntDataRef<ItemData> { Value = 1001 }, new IntDataRef<ItemData>[0]),
                ["ref2"] = new RefTestData("ref2", new StringDataRef<CharacterData> { Value = "char_002" }, new IntDataRef<ItemData> { Value = 1002 }, new IntDataRef<ItemData>[0])
            };
            
            // Act - Serialize
            var csv = RefTestDataSerializer.SerializeCsv(refTestData);
            
            // Assert - Check CSV format
            Assert.Contains("Id,CharacterRef,ItemRef,ItemRefs", csv);
            Assert.Contains("ref1,char_001,1001", csv);
            Assert.Contains("ref2,char_002,1002", csv);
            
            // Act - Deserialize
            var deserialized = RefTestDataSerializer.DeserializeCsv(csv);
            
            // Assert - Check deserialized data
            Assert.Equal(2, deserialized.Count);
            Assert.Equal("char_001", deserialized["ref1"].CharacterRef.Value);
            Assert.Equal("char_002", deserialized["ref2"].CharacterRef.Value);
            Assert.Equal(1001, deserialized["ref1"].ItemRef.Value);
            Assert.Equal(1002, deserialized["ref2"].ItemRef.Value);
        }
        
        [Fact]
        public async Task RefTestData_Evaluate_ShouldResolveReferences()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Get the first character ID from loaded data
            var contextCharacter = context.Character;
            var firstCharacter = contextCharacter.Values.FirstOrDefault();
            Assert.NotNull(firstCharacter);
            
            // Get the first item ID from loaded data
            var firstItem = context.Item.Values.ToList().FirstOrDefault();
            Assert.NotNull(firstItem);
            
            // Create ref test data pointing to the first character and item
            var refData = new RefTestData("ref1", new StringDataRef<CharacterData> { Value = firstCharacter.Id }, new IntDataRef<ItemData> { Value = firstItem.Id }, new IntDataRef<ItemData>[0]);
            
            // Act - Evaluate the reference
            var character = refData.CharacterRef.Evaluate(context);
            
            // Assert character reference
            Assert.NotNull(character);
            Assert.Equal(firstCharacter.Id, character.Id);
            Assert.Equal(firstCharacter.Name, character.Name);
            Assert.Equal(firstCharacter.Level, character.Level);
            
            // Act - Evaluate item reference
            var item = refData.ItemRef.Evaluate(context);
            
            // Assert item reference
            Assert.NotNull(item);
            Assert.Equal(firstItem.Id, item.Id);
            Assert.Equal(firstItem.Name, item.Name);
            Assert.Equal(firstItem.Price, item.Price);
        }
        
        [Fact]
        public void RefTestData_Evaluate_WithEmptyValue_ShouldReturnNull()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            
            var refData = new RefTestData("ref1", new StringDataRef<CharacterData> { Value = "" }, new IntDataRef<ItemData> { Value = 0 }, new IntDataRef<ItemData>[0]);
            
            // Act
            var characterResult = refData.CharacterRef.Evaluate(context);
            var itemResult = refData.ItemRef.Evaluate(context);
            
            // Assert
            Assert.Null(characterResult);
            Assert.Null(itemResult);
        }
    }
}