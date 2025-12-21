using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Datra.DataTypes;
using Datra.Models;
using Datra.Services;
using Datra.Interfaces;
using Datra.Configuration;
using Datra.Localization;
using Datra.Repositories;

namespace Datra.Tests
{
    public class FixedLocaleTests
    {
        private class MockRawDataProvider : IRawDataProvider
        {
            private Dictionary<string, string> _files = new Dictionary<string, string>();

            public MockRawDataProvider()
            {
                // Setup LocalizationKeys.csv with fixed keys
                _files["Localizations/LocalizationKeys.csv"] =
                    "Id,Description,Category,IsFixedKey\n" +
                    "Button_Start,Start button,UI,false\n" +
                    "Button_Exit,Exit button,UI,false\n" +
                    "Character_Hero_Name,Hero name,Character,true\n" +
                    "Character_Hero_Desc,Hero description,Character,true\n" +
                    "Item_Sword_Name,Sword name,Item,true\n" +
                    "Item_Sword_Desc,Sword description,Item,true\n";

                // Setup English translations
                _files["Localizations/en.csv"] =
                    "Id,Text,Context\n" +
                    "Button_Start,Start,Main Menu\n" +
                    "Button_Exit,Exit,Main Menu\n" +
                    "Character_Hero_Name,Hero,Character Info\n" +
                    "Character_Hero_Desc,A brave warrior,Character Info\n" +
                    "Item_Sword_Name,Iron Sword,Item\n" +
                    "Item_Sword_Desc,A basic sword,Item\n";

                // Setup Korean translations
                _files["Localizations/ko.csv"] =
                    "Id,Text,Context\n" +
                    "Button_Start,시작,메인 메뉴\n" +
                    "Button_Exit,종료,메인 메뉴\n" +
                    "Character_Hero_Name,용사,캐릭터 정보\n" +
                    "Character_Hero_Desc,용감한 전사,캐릭터 정보\n" +
                    "Item_Sword_Name,강철 검,아이템\n" +
                    "Item_Sword_Desc,기본 검,아이템\n";
            }

            public bool Exists(string path) => _files.ContainsKey(path);

            public Task<string> LoadTextAsync(string path)
            {
                if (_files.TryGetValue(path, out var content))
                    return Task.FromResult(content);
                throw new System.IO.FileNotFoundException($"File not found: {path}");
            }

            public Task SaveTextAsync(string path, string content)
            {
                _files[path] = content;
                return Task.CompletedTask;
            }

            public string ResolveFilePath(string path)
            {
                // For mock purposes, just return the path as-is
                return path;
            }

            public string GetSavedContent(string path)
            {
                return _files.TryGetValue(path, out var content) ? content : string.Empty;
            }
        }

        private class TestLocalizationKeyDataSerializer
        {
            public static Dictionary<string, LocalizationKeyData> DeserializeCsv(string csvData, object? config)
            {
                var result = new Dictionary<string, LocalizationKeyData>();
                var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length <= 1) return result;

                // Parse header
                var headers = lines[0].Split(',');
                var idIndex = Array.FindIndex(headers, h => h.Equals("Id", StringComparison.OrdinalIgnoreCase));
                var descIndex = Array.FindIndex(headers, h => h.Equals("Description", StringComparison.OrdinalIgnoreCase));
                var categoryIndex = Array.FindIndex(headers, h => h.Equals("Category", StringComparison.OrdinalIgnoreCase));
                var isFixedKeyIndex = Array.FindIndex(headers, h => h.Equals("IsFixedKey", StringComparison.OrdinalIgnoreCase));

                // Parse data rows
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length > idIndex && !string.IsNullOrWhiteSpace(values[idIndex]))
                    {
                        var data = new LocalizationKeyData
                        {
                            Id = values[idIndex],
                            Description = descIndex >= 0 && values.Length > descIndex ? values[descIndex] : string.Empty,
                            Category = categoryIndex >= 0 && values.Length > categoryIndex ? values[categoryIndex] : string.Empty,
                            IsFixedKey = isFixedKeyIndex >= 0 && values.Length > isFixedKeyIndex && bool.TryParse(values[isFixedKeyIndex], out var isFixed) ? isFixed : false
                        };
                        result[data.Id] = data;
                    }
                }

                return result;
            }

            public static string SerializeCsv(Dictionary<string, LocalizationKeyData> data, object? config)
            {
                var lines = new List<string>();
                lines.Add("Id,Description,Category,IsFixedKey");

                foreach (var item in data.Values.OrderBy(x => x.Id))
                {
                    lines.Add($"{item.Id},{item.Description},{item.Category},{item.IsFixedKey.ToString()}");
                }

                return string.Join("\n", lines);
            }
        }

        private LocalizationContext CreateTestContext(MockRawDataProvider provider, DatraConfigurationValue config)
        {
            var context = new LocalizationContext(provider, null, config);

            // Create and set key repository
            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
                config.LocalizationKeyDataPath,
                provider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );

            context.SetKeyRepository(keyRepository);
            return context;
        }

        [Fact]
        public void LocaleRef_CreateFixed_GeneratesCorrectKey()
        {
            // Act
            var itemName = LocaleRef.CreateFixed("ItemInfo", "sword_001", "Name");
            var characterDesc = LocaleRef.CreateFixed("CharacterInfo", "hero", "Desc");

            // Assert
            Assert.Equal("ItemInfo.sword_001.Name", itemName.Key);
            Assert.Equal("CharacterInfo.hero.Desc", characterDesc.Key);
        }

        [Fact]
        public void LocaleRef_CreateFixedGeneric_GeneratesCorrectKey()
        {
            // Act - using anonymous types to simulate entity types
            var itemName = LocaleRef.CreateFixed<ItemInfo>("sword_001", "Name");
            var characterDesc = LocaleRef.CreateFixed<CharacterInfo>("hero", "Desc");

            // Assert
            Assert.Equal("ItemInfo.sword_001.Name", itemName.Key);
            Assert.Equal("CharacterInfo.hero.Desc", characterDesc.Key);
        }

        [Fact]
        public void LocaleRef_CreateNested_GeneratesCorrectPath()
        {
            // Act
            var nestedPath = LocaleRef.CreateNested("Graph", "Nodes", "Name");
            var complexPath = LocaleRef.CreateNested("Dialog", "Fragments", "Text");

            // Assert
            Assert.Equal("Graph.Nodes.Name", nestedPath.Key);
            Assert.Equal("Dialog.Fragments.Text", complexPath.Key);
        }

        [Fact]
        public async Task LocalizationKeyData_IsFixedKey_ParsedCorrectly()
        {
            // Arrange
            var provider = new MockRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                localizationKeyDataPath: "Localizations/LocalizationKeys.csv",
                localizationDataPath: "Localizations",
                defaultLanguage: "en"
            );

            var context = CreateTestContext(provider, config);

            // Act
            await context.InitializeAsync();

            // Assert - Check fixed keys
            Assert.True(context.IsFixedKey("Character_Hero_Name"));
            Assert.True(context.IsFixedKey("Character_Hero_Desc"));
            Assert.True(context.IsFixedKey("Item_Sword_Name"));
            Assert.True(context.IsFixedKey("Item_Sword_Desc"));

            // Assert - Check non-fixed keys
            Assert.False(context.IsFixedKey("Button_Start"));
            Assert.False(context.IsFixedKey("Button_Exit"));
        }

        [Fact]
        public async Task LocalizationKeyData_GetKeyData_ReturnsCorrectMetadata()
        {
            // Arrange
            var provider = new MockRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                localizationKeyDataPath: "Localizations/LocalizationKeys.csv",
                localizationDataPath: "Localizations",
                defaultLanguage: "en"
            );

            var context = CreateTestContext(provider, config);
            await context.InitializeAsync();

            // Act
            var heroNameData = context.GetKeyData("Character_Hero_Name");
            var buttonStartData = context.GetKeyData("Button_Start");

            // Assert
            Assert.NotNull(heroNameData);
            Assert.Equal("Character_Hero_Name", heroNameData.Id);
            Assert.Equal("Hero name", heroNameData.Description);
            Assert.Equal("Character", heroNameData.Category);
            Assert.True(heroNameData.IsFixedKey);

            Assert.NotNull(buttonStartData);
            Assert.Equal("Button_Start", buttonStartData.Id);
            Assert.Equal("Start button", buttonStartData.Description);
            Assert.Equal("UI", buttonStartData.Category);
            Assert.False(buttonStartData.IsFixedKey);
        }

        [Fact]
        public async Task FixedKey_ValuesCanBeModified()
        {
            // Arrange
            var provider = new MockRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                localizationKeyDataPath: "Localizations/LocalizationKeys.csv",
                localizationDataPath: "Localizations",
                defaultLanguage: "en"
            );

            var context = CreateTestContext(provider, config);
            await context.InitializeAsync();
            await context.LoadLanguageAsync(LanguageCode.En);

            // Act - Modify a fixed key's value (this should be allowed)
            var originalValue = context.GetText("Character_Hero_Name");
            Assert.Equal("Hero", originalValue);

            context.SetText("Character_Hero_Name", "Brave Hero");
            var newValue = context.GetText("Character_Hero_Name");

            // Assert
            Assert.Equal("Brave Hero", newValue);
            Assert.True(context.IsFixedKey("Character_Hero_Name")); // Still marked as fixed
        }

        [Fact]
        public async Task FixedKey_ValuesSavedCorrectly()
        {
            // Arrange
            var provider = new MockRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                localizationKeyDataPath: "Localizations/LocalizationKeys.csv",
                localizationDataPath: "Localizations",
                defaultLanguage: "en"
            );

            var context = CreateTestContext(provider, config);
            await context.InitializeAsync();
            await context.LoadLanguageAsync(LanguageCode.En);

            // Act - Modify fixed key value and save
            context.SetText("Character_Hero_Name", "Legendary Hero");
            await context.SaveCurrentLanguageAsync();

            // Assert - Check saved content
            var savedContent = provider.GetSavedContent("Localizations/en.csv");
            Assert.NotNull(savedContent);
            Assert.Contains("Character_Hero_Name,Legendary Hero,Character Info", savedContent);
        }

        [Fact]
        public async Task FixedKey_WorksAcrossLanguages()
        {
            // Arrange
            var provider = new MockRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                localizationKeyDataPath: "Localizations/LocalizationKeys.csv",
                localizationDataPath: "Localizations",
                defaultLanguage: "en"
            );

            var context = CreateTestContext(provider, config);
            await context.InitializeAsync();

            // Act & Assert - English
            await context.LoadLanguageAsync(LanguageCode.En);
            Assert.Equal("Hero", context.GetText("Character_Hero_Name"));
            Assert.True(context.IsFixedKey("Character_Hero_Name"));

            // Act & Assert - Korean
            await context.LoadLanguageAsync(LanguageCode.Ko);
            Assert.Equal("용사", context.GetText("Character_Hero_Name"));
            Assert.True(context.IsFixedKey("Character_Hero_Name")); // Still fixed in Korean
        }

        [Fact]
        public void LocalizationKeyData_DefaultIsFixedKey_IsFalse()
        {
            // Arrange & Act
            var keyData = new LocalizationKeyData
            {
                Id = "Test_Key",
                Description = "Test description",
                Category = "Test"
            };

            // Assert
            Assert.False(keyData.IsFixedKey);
        }

        [Fact]
        public async Task DeleteKeyAsync_FixedKey_ThrowsException()
        {
            // Arrange
            var provider = new MockRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                localizationKeyDataPath: "Localizations/LocalizationKeys.csv",
                localizationDataPath: "Localizations",
                defaultLanguage: "en"
            );

            var context = CreateTestContext(provider, config);
            await context.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await context.DeleteKeyAsync("Character_Hero_Name");
            });
        }

        [Fact]
        public async Task DeleteKeyAsync_RegularKey_DeletesSuccessfully()
        {
            // Arrange
            var provider = new MockRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                localizationKeyDataPath: "Localizations/LocalizationKeys.csv",
                localizationDataPath: "Localizations",
                defaultLanguage: "en"
            );

            var context = CreateTestContext(provider, config);
            await context.InitializeAsync();
            await context.LoadLanguageAsync(LanguageCode.En);

            // Act
            await context.DeleteKeyAsync("Button_Start");

            // Assert
            Assert.False(context.HasKey("Button_Start"));
            Assert.Null(context.GetKeyData("Button_Start"));
        }

        [Fact]
        public async Task DeleteKeyAsync_NonExistentKey_ThrowsNothing()
        {
            // Arrange
            var provider = new MockRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                localizationKeyDataPath: "Localizations/LocalizationKeys.csv",
                localizationDataPath: "Localizations",
                defaultLanguage: "en"
            );

            var context = CreateTestContext(provider, config);
            await context.InitializeAsync();

            // Act - deleting non-existent key should not throw
            await context.DeleteKeyAsync("NonExistent_Key");

            // Assert - no exception thrown
            Assert.True(true);
        }

        [Fact]
        public async Task AddKeyAsync_NewKey_AddsSuccessfully()
        {
            // Arrange
            var provider = new MockRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                localizationKeyDataPath: "Localizations/LocalizationKeys.csv",
                localizationDataPath: "Localizations",
                defaultLanguage: "en"
            );

            var context = CreateTestContext(provider, config);
            await context.InitializeAsync();
            await context.LoadLanguageAsync(LanguageCode.En);

            // Act
            await context.AddKeyAsync("NewKey_Test", "Test description", "Test", false);

            // Assert
            var keyData = context.GetKeyData("NewKey_Test");
            Assert.NotNull(keyData);
            Assert.Equal("NewKey_Test", keyData.Id);
            Assert.Equal("Test description", keyData.Description);
            Assert.Equal("Test", keyData.Category);
            Assert.False(keyData.IsFixedKey);
        }

        [Fact]
        public async Task AddKeyAsync_DuplicateKey_ThrowsException()
        {
            // Arrange
            var provider = new MockRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                localizationKeyDataPath: "Localizations/LocalizationKeys.csv",
                localizationDataPath: "Localizations",
                defaultLanguage: "en"
            );

            var context = CreateTestContext(provider, config);
            await context.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await context.AddKeyAsync("Button_Start", "Duplicate", "UI", false);
            });
        }

        // Helper classes for generic type testing
        private class ItemInfo { }
        private class CharacterInfo { }
    }
}
