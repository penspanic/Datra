using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Data.DataTypes;
using Datra.Tests.Models;
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
                ["ref1"] = new RefTestData("ref1", new StringDataRef<CharacterData> { Value = "char_001" }),
                ["ref2"] = new RefTestData("ref2", new StringDataRef<CharacterData> { Value = "char_002" })
            };
            
            // Act - Serialize
            var csv = RefTestDataSerializer.SerializeCsv(refTestData);
            
            // Assert - Check CSV format
            Assert.Contains("Id,CharacterRef", csv);
            Assert.Contains("ref1,char_001", csv);
            Assert.Contains("ref2,char_002", csv);
            
            // Act - Deserialize
            var deserialized = RefTestDataSerializer.DeserializeCsv(csv);
            
            // Assert - Check deserialized data
            Assert.Equal(2, deserialized.Count);
            Assert.Equal("char_001", deserialized["ref1"].CharacterRef.Value);
            Assert.Equal("char_002", deserialized["ref2"].CharacterRef.Value);
        }
        
        [Fact]
        public async Task RefTestData_Evaluate_ShouldResolveReferences()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Get the first character ID from loaded data
            var firstCharacter = context.Character.GetAll().Values.FirstOrDefault();
            Assert.NotNull(firstCharacter);
            
            // Create ref test data pointing to the first character
            var refData = new RefTestData("ref1", new StringDataRef<CharacterData> { Value = firstCharacter.Id });
            
            // Act - Evaluate the reference
            var character = refData.CharacterRef.Evaluate(context);
            
            // Assert
            Assert.NotNull(character);
            Assert.Equal(firstCharacter.Id, character.Id);
            Assert.Equal(firstCharacter.Name, character.Name);
            Assert.Equal(firstCharacter.Level, character.Level);
        }
        
        [Fact]
        public async Task RefTestData_Evaluate_WithInvalidId_ShouldThrow()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            var refData = new RefTestData("ref1", new StringDataRef<CharacterData> { Value = "invalid_id_that_does_not_exist" });
            
            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => refData.CharacterRef.Evaluate(context));
        }
        
        [Fact]
        public void RefTestData_Evaluate_WithEmptyValue_ShouldReturnNull()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            
            var refData = new RefTestData("ref1", new StringDataRef<CharacterData> { Value = "" });
            
            // Act
            var result = refData.CharacterRef.Evaluate(context);
            
            // Assert
            Assert.Null(result);
        }
    }
}