using System.Collections.Generic;
using Xunit;
using Datra.Helpers;

namespace Datra.Tests
{
    public class StringTemplateHelperTests
    {
        #region Format with Anonymous Object

        [Fact]
        public void Format_WithAnonymousObject_ReplacesPlaceholders()
        {
            // Arrange
            var template = "Deal {Damage}% damage to {Count} enemies";
            var values = new { Damage = 150, Count = 3 };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Deal 150% damage to 3 enemies", result);
        }

        [Fact]
        public void Format_WithAnonymousObject_CaseInsensitive()
        {
            // Arrange
            var template = "Deal {damage}% damage";
            var values = new { Damage = 150 };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Deal 150% damage", result);
        }

        [Fact]
        public void Format_WithAnonymousObject_MissingProperty_LeavesPlaceholder()
        {
            // Arrange
            var template = "Deal {Damage}% damage, {Missing} value";
            var values = new { Damage = 150 };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Deal 150% damage, {Missing} value", result);
        }

        [Fact]
        public void Format_WithNullValues_ReturnsTemplate()
        {
            // Arrange
            var template = "Deal {Damage}% damage";

            // Act
            var result = StringTemplateHelper.Format(template, (object?)null);

            // Assert
            Assert.Equal("Deal {Damage}% damage", result);
        }

        [Fact]
        public void Format_WithNullTemplate_ReturnsEmpty()
        {
            // Act
            var result = StringTemplateHelper.Format(null!, new { Damage = 150 });

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Format_WithEmptyTemplate_ReturnsEmpty()
        {
            // Act
            var result = StringTemplateHelper.Format("", new { Damage = 150 });

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Format_WithNullPropertyValue_ReplacesWithEmpty()
        {
            // Arrange
            var template = "Name: {Name}";
            var values = new { Name = (string?)null };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Name: ", result);
        }

        [Fact]
        public void Format_WithMultipleSamePlaceholders_ReplacesAll()
        {
            // Arrange
            var template = "{Value} + {Value} = {Result}";
            var values = new { Value = 5, Result = 10 };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("5 + 5 = 10", result);
        }

        [Fact]
        public void Format_WithFloatValue_FormatsCorrectly()
        {
            // Arrange
            var template = "Damage: {Damage}%";
            var values = new { Damage = 150.5f };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Damage: 150.5%", result);
        }

        #endregion

        #region Format with Dictionary

        [Fact]
        public void Format_WithDictionary_ReplacesPlaceholders()
        {
            // Arrange
            var template = "Deal {Damage}% damage to {Count} enemies";
            var values = new Dictionary<string, object?>
            {
                { "Damage", 150 },
                { "Count", 3 }
            };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Deal 150% damage to 3 enemies", result);
        }

        [Fact]
        public void Format_WithDictionary_CaseInsensitive()
        {
            // Arrange
            var template = "Deal {damage}% damage";
            var values = new Dictionary<string, object?>
            {
                { "Damage", 150 }
            };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Deal 150% damage", result);
        }

        [Fact]
        public void Format_WithDictionary_ExactMatchPreferred()
        {
            // Arrange
            var template = "Value: {Value}";
            var values = new Dictionary<string, object?>
            {
                { "value", "lowercase" },
                { "Value", "PascalCase" }
            };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Value: PascalCase", result);
        }

        [Fact]
        public void Format_WithEmptyDictionary_ReturnsTemplate()
        {
            // Arrange
            var template = "Deal {Damage}% damage";
            var values = new Dictionary<string, object?>();

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Deal {Damage}% damage", result);
        }

        [Fact]
        public void Format_WithNullDictionary_ReturnsTemplate()
        {
            // Arrange
            var template = "Deal {Damage}% damage";

            // Act
            var result = StringTemplateHelper.Format(template, (IDictionary<string, object?>)null!);

            // Assert
            Assert.Equal("Deal {Damage}% damage", result);
        }

        #endregion

        #region GetPlaceholders

        [Fact]
        public void GetPlaceholders_ReturnsAllPlaceholders()
        {
            // Arrange
            var template = "Deal {Damage}% damage to {Count} enemies with {Stunt}";

            // Act
            var result = StringTemplateHelper.GetPlaceholders(template);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Contains("Damage", result);
            Assert.Contains("Count", result);
            Assert.Contains("Stunt", result);
        }

        [Fact]
        public void GetPlaceholders_ReturnsDeduplicated()
        {
            // Arrange
            var template = "{Value} + {Value} = {Result}";

            // Act
            var result = StringTemplateHelper.GetPlaceholders(template);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains("Value", result);
            Assert.Contains("Result", result);
        }

        [Fact]
        public void GetPlaceholders_WithNoPlaceholders_ReturnsEmpty()
        {
            // Arrange
            var template = "No placeholders here";

            // Act
            var result = StringTemplateHelper.GetPlaceholders(template);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetPlaceholders_WithNullTemplate_ReturnsEmpty()
        {
            // Act
            var result = StringTemplateHelper.GetPlaceholders(null!);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region HasPlaceholders

        [Fact]
        public void HasPlaceholders_WithPlaceholders_ReturnsTrue()
        {
            // Arrange
            var template = "Deal {Damage}% damage";

            // Act
            var result = StringTemplateHelper.HasPlaceholders(template);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasPlaceholders_WithoutPlaceholders_ReturnsFalse()
        {
            // Arrange
            var template = "No placeholders here";

            // Act
            var result = StringTemplateHelper.HasPlaceholders(template);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasPlaceholders_WithNullTemplate_ReturnsFalse()
        {
            // Act
            var result = StringTemplateHelper.HasPlaceholders(null!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasPlaceholders_WithEmptyTemplate_ReturnsFalse()
        {
            // Act
            var result = StringTemplateHelper.HasPlaceholders("");

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Format_WithUnderscoreInPlaceholder_Works()
        {
            // Arrange
            var template = "Value: {damage_multiplier}";
            var values = new { damage_multiplier = 150 };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Value: 150", result);
        }

        [Fact]
        public void Format_WithNumbersInPlaceholder_Works()
        {
            // Arrange
            var template = "Value: {Value1}";
            var values = new { Value1 = 100 };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert
            Assert.Equal("Value: 100", result);
        }

        [Fact]
        public void Format_DoesNotMatchInvalidPlaceholders()
        {
            // Arrange - placeholders starting with numbers are invalid
            var template = "Value: {1Value}";
            var values = new { };

            // Act
            var result = StringTemplateHelper.Format(template, values);

            // Assert - invalid placeholder should remain as-is
            Assert.Equal("Value: {1Value}", result);
        }

        #endregion
    }
}
