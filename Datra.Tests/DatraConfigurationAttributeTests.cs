using System;
using Xunit;
using Datra.Attributes;

namespace Datra.Tests
{
    public class DatraConfigurationAttributeTests
    {
        [Fact]
        public void DatraConfigurationAttribute_RequiresContextName()
        {
            // Arrange & Act
            var attribute = new DatraConfigurationAttribute("TestContext");

            // Assert
            Assert.Equal("TestContext", attribute.ContextName);
        }

        [Fact]
        public void DatraConfigurationAttribute_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var attribute = new DatraConfigurationAttribute("GameData");

            // Assert
            Assert.Equal("GameData", attribute.ContextName);
            Assert.Equal("Localizations/LocalizationKeys.csv", attribute.LocalizationKeyDataPath);
            Assert.Equal("Localizations/", attribute.LocalizationDataPath);
            Assert.Equal("en", attribute.DefaultLanguage);
            Assert.Null(attribute.Namespace); // Defaults to null (generator uses {AssemblyName}.Generated)
            Assert.False(attribute.EnableDebugLogging);
            Assert.False(attribute.EnableLocalization);
            Assert.False(attribute.EmitPhysicalFiles);
            Assert.Null(attribute.PhysicalFilesPath);
        }

        [Fact]
        public void DatraConfigurationAttribute_SetProperties_WorksCorrectly()
        {
            // Arrange
            var attribute = new DatraConfigurationAttribute("MyContext");

            // Act
            attribute.LocalizationKeyDataPath = "Custom/Path/Keys.csv";
            attribute.LocalizationDataPath = "Custom/Localizations/";
            attribute.DefaultLanguage = "ko";
            attribute.Namespace = "MyGame.Generated";
            attribute.EnableDebugLogging = true;
            attribute.EnableLocalization = true;
            attribute.EmitPhysicalFiles = true;
            attribute.PhysicalFilesPath = "Generated/";

            // Assert
            Assert.Equal("MyContext", attribute.ContextName);
            Assert.Equal("Custom/Path/Keys.csv", attribute.LocalizationKeyDataPath);
            Assert.Equal("Custom/Localizations/", attribute.LocalizationDataPath);
            Assert.Equal("ko", attribute.DefaultLanguage);
            Assert.Equal("MyGame.Generated", attribute.Namespace);
            Assert.True(attribute.EnableDebugLogging);
            Assert.True(attribute.EnableLocalization);
            Assert.True(attribute.EmitPhysicalFiles);
            Assert.Equal("Generated/", attribute.PhysicalFilesPath);
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
            var attribute = new DatraConfigurationAttribute("GameContext")
            {
                LocalizationKeyDataPath = "Assets/Data/Localization/Keys.csv",
                LocalizationDataPath = "Assets/Data/Localization/Languages/",
                DefaultLanguage = "ja",
                Namespace = "Game.Data",
                EnableDebugLogging = false,
                EnableLocalization = true
            };

            // Assert
            Assert.Equal("GameContext", attribute.ContextName);
            Assert.Equal("Assets/Data/Localization/Keys.csv", attribute.LocalizationKeyDataPath);
            Assert.Equal("Assets/Data/Localization/Languages/", attribute.LocalizationDataPath);
            Assert.Equal("ja", attribute.DefaultLanguage);
            Assert.Equal("Game.Data", attribute.Namespace);
            Assert.False(attribute.EnableDebugLogging);
            Assert.True(attribute.EnableLocalization);
        }

        [Fact]
        public void DatraConfigurationAttribute_ThrowsOnNullContextName()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new DatraConfigurationAttribute(null!));
        }
    }
}
