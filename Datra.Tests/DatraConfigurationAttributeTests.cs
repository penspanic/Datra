using System;
using Xunit;
using Datra.Attributes;
using Datra.Localization;

namespace Datra.Tests
{
    public class DatraConfigurationAttributeTests
    {
        [Fact]
        public void DatraConfigurationAttribute_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var attribute = new DatraConfigurationAttribute();
            
            // Assert
            Assert.Equal("Localizations/LocalizationKeys.csv", attribute.LocalizationKeyDataPath);
            Assert.Equal("Localizations/", attribute.LocalizationDataPath);
            Assert.Equal("en", attribute.DefaultLanguage);
            Assert.Equal("GameDataContext", attribute.DataContextName);
            Assert.Equal("Datra.Generated", attribute.GeneratedNamespace);
            Assert.False(attribute.EnableDebugLogging);
        }
        
        [Fact]
        public void DatraConfigurationAttribute_SetProperties_WorksCorrectly()
        {
            // Arrange
            var attribute = new DatraConfigurationAttribute();
            
            // Act
            attribute.LocalizationKeyDataPath = "Custom/Path/Keys.csv";
            attribute.LocalizationDataPath = "Custom/Localizations/";
            attribute.DefaultLanguage = "Korean";
            attribute.DataContextName = "MyDataContext";
            attribute.GeneratedNamespace = "MyGame.Generated";
            attribute.EnableDebugLogging = true;
            
            // Assert
            Assert.Equal("Custom/Path/Keys.csv", attribute.LocalizationKeyDataPath);
            Assert.Equal("Custom/Localizations/", attribute.LocalizationDataPath);
            Assert.Equal("Korean", attribute.DefaultLanguage);
            Assert.Equal("MyDataContext", attribute.DataContextName);
            Assert.Equal("MyGame.Generated", attribute.GeneratedNamespace);
            Assert.True(attribute.EnableDebugLogging);
        }
        
        [Fact]
        public void DatraConfigurationAttribute_CanBeAppliedToAssembly()
        {
            // Arrange
            var attributeType = typeof(DatraConfigurationAttribute);
            
            // Act
            var usageAttributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
            
            // Assert
            Assert.Single(usageAttributes);
            var usage = (AttributeUsageAttribute)usageAttributes[0];
            Assert.Equal(AttributeTargets.Assembly, usage.ValidOn);
            Assert.False(usage.AllowMultiple);
        }
        
        [Fact]
        public void DatraConfigurationAttribute_InitializerSyntax_WorksCorrectly()
        {
            // Arrange & Act
            var attribute = new DatraConfigurationAttribute
            {
                LocalizationKeyDataPath = "Assets/Data/Localization/Keys.csv",
                LocalizationDataPath = "Assets/Data/Localization/Languages/",
                DefaultLanguage = "Japanese",
                DataContextName = "GameContext",
                GeneratedNamespace = "Game.Data",
                EnableDebugLogging = false
            };
            
            // Assert
            Assert.Equal("Assets/Data/Localization/Keys.csv", attribute.LocalizationKeyDataPath);
            Assert.Equal("Assets/Data/Localization/Languages/", attribute.LocalizationDataPath);
            Assert.Equal("Japanese", attribute.DefaultLanguage);
            Assert.Equal("GameContext", attribute.DataContextName);
            Assert.Equal("Game.Data", attribute.GeneratedNamespace);
            Assert.False(attribute.EnableDebugLogging);
        }
    }
}