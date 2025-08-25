using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Datra.Models;
using Datra.Services;
using Datra.Interfaces;
using Datra.Repositories;
using Datra.Serializers;

namespace Datra.Tests
{
    public class LocalizationContextTests
    {
        private class TestRawDataProvider : IRawDataProvider
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();
            
            public TestRawDataProvider()
            {
                // Add test LocalizationKeys.csv
                _files["Localizations/LocalizationKeys.csv"] = @"Id,Description,Category
Button_Start,Start button text,UI
Button_Exit,Exit button text,UI
Button_Settings,Settings button text,UI
Message_Welcome,Welcome message,System
Message_Error,Error message,System
Character_Hero_Name,Hero character name,Character
Character_Hero_Desc,Hero character description,Character";

                // Add test language files
                _files["Localizations/English.csv"] = @"Id,Text,Context
Button_Start,Start,Main Menu
Button_Exit,Exit,Main Menu
Button_Settings,Settings,Main Menu
Message_Welcome,Welcome!,First Login
Message_Error,An error occurred,Error Dialog
Character_Hero_Name,Hero,Character Info
Character_Hero_Desc,A brave warrior,Character Info";

                _files["Localizations/Korean.csv"] = @"Id,Text,Context
Button_Start,시작,메인 메뉴
Button_Exit,종료,메인 메뉴
Button_Settings,설정,메인 메뉴
Message_Welcome,환영합니다!,첫 접속
Message_Error,오류가 발생했습니다,오류 대화상자
Character_Hero_Name,용사,캐릭터 정보
Character_Hero_Desc,용감한 전사,캐릭터 정보";

                _files["Localizations/Japanese.csv"] = @"Id,Text,Context
Button_Start,スタート,メインメニュー
Button_Exit,終了,メインメニュー
Button_Settings,設定,メインメニュー
Message_Welcome,ようこそ！,初回ログイン
Message_Error,エラーが発生しました,エラーダイアログ
Character_Hero_Name,勇者,キャラクター情報
Character_Hero_Desc,勇敢な戦士,キャラクター情報";
            }
            
            public bool Exists(string path)
            {
                return _files.ContainsKey(path);
            }
            
            public Task<byte[]> LoadAsync(string path)
            {
                if (_files.TryGetValue(path, out var content))
                {
                    return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(content));
                }
                throw new System.IO.FileNotFoundException($"File not found: {path}");
            }
            
            public Task<string> LoadTextAsync(string path)
            {
                if (_files.TryGetValue(path, out var content))
                {
                    return Task.FromResult(content);
                }
                throw new System.IO.FileNotFoundException($"File not found: {path}");
            }
            
            public Task SaveAsync(string path, byte[] data)
            {
                _files[path] = System.Text.Encoding.UTF8.GetString(data);
                return Task.CompletedTask;
            }
            
            public Task SaveTextAsync(string path, string text)
            {
                _files[path] = text;
                return Task.CompletedTask;
            }
            
            public string[] GetFiles(string directory, string searchPattern = "*", bool recursive = false)
            {
                return _files.Keys
                    .Where(k => k.StartsWith(directory))
                    .ToArray();
            }
            
            public string ResolveFilePath(string path)
            {
                // For test purposes, just return the path as-is
                return path;
            }
        }
        
        private class TestLocalizationKeyDataSerializer
        {
            public static Dictionary<string, LocalizationKeyData> DeserializeCsv(string csvData, object config)
            {
                var result = new Dictionary<string, LocalizationKeyData>();
                var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length <= 1) return result;
                
                // Parse header
                var headers = lines[0].Split(',');
                var idIndex = Array.FindIndex(headers, h => h.Equals("Id", StringComparison.OrdinalIgnoreCase));
                var descIndex = Array.FindIndex(headers, h => h.Equals("Description", StringComparison.OrdinalIgnoreCase));
                var categoryIndex = Array.FindIndex(headers, h => h.Equals("Category", StringComparison.OrdinalIgnoreCase));
                
                // Parse data rows
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length > idIndex && !string.IsNullOrWhiteSpace(values[idIndex]))
                    {
                        var data = new LocalizationKeyData
                        {
                            Id = values[idIndex],
                            Description = descIndex >= 0 && values.Length > descIndex ? values[descIndex] : null,
                            Category = categoryIndex >= 0 && values.Length > categoryIndex ? values[categoryIndex] : null
                        };
                        result[data.Id] = data;
                    }
                }
                
                return result;
            }
            
            public static string SerializeCsv(Dictionary<string, LocalizationKeyData> data, object config)
            {
                var lines = new List<string>();
                lines.Add("Id,Description,Category");
                
                foreach (var item in data.Values.OrderBy(x => x.Id))
                {
                    lines.Add($"{item.Id},{item.Description},{item.Category}");
                }
                
                return string.Join("\n", lines);
            }
        }
        
        [Fact]
        public async Task LocalizationContext_InitializeAsync_LoadsKeysFromRepository()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            // Create and set key repository
            var keyRepository = new DataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            
            // Act
            await context.InitializeAsync();
            var keys = context.GetAllKeys().ToList();
            
            // Assert
            Assert.NotEmpty(keys);
            Assert.Contains("Button_Start", keys);
            Assert.Contains("Button_Exit", keys);
            Assert.Contains("Button_Settings", keys);
            Assert.Contains("Message_Welcome", keys);
            Assert.Contains("Message_Error", keys);
            Assert.Contains("Character_Hero_Name", keys);
            Assert.Contains("Character_Hero_Desc", keys);
            Assert.Equal(7, keys.Count);
        }
        
        [Fact]
        public async Task LocalizationContext_GetKeyInfo_ReturnsCorrectKeyData()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            var keyRepository = new DataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();
            
            // Act
            var keyInfo = context.GetKeyData("Button_Start") as LocalizationKeyData;
            
            // Assert
            Assert.NotNull(keyInfo);
            Assert.Equal("Button_Start", keyInfo.Id);
            Assert.Equal("Start button text", keyInfo.Description);
            Assert.Equal("UI", keyInfo.Category);
        }
        
        [Fact]
        public async Task LocalizationContext_GetKeyInfo_ReturnsNullForInvalidKey()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            var keyRepository = new DataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();
            
            // Act
            var keyInfo = context.GetKeyData("NonExistentKey");
            
            // Assert
            Assert.Null(keyInfo);
        }
        
        [Fact]
        public async Task LocalizationContext_LoadLanguageAsync_LoadsTranslations()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            var keyRepository = new DataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();
            
            // Act
            await context.LoadLanguageAsync("Korean");
            
            // Assert
            Assert.Equal("Korean", context.CurrentLanguage);
            Assert.Equal("시작", context.GetText("Button_Start"));
            Assert.Equal("종료", context.GetText("Button_Exit"));
            Assert.Equal("환영합니다!", context.GetText("Message_Welcome"));
        }
        
        [Fact]
        public async Task LocalizationContext_SwitchLanguage_UpdatesTranslations()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            var keyRepository = new DataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();
            
            // Act & Assert - English
            await context.LoadLanguageAsync("English");
            Assert.Equal("English", context.CurrentLanguage);
            Assert.Equal("Start", context.GetText("Button_Start"));
            Assert.Equal("Welcome!", context.GetText("Message_Welcome"));
            
            // Act & Assert - Korean
            await context.LoadLanguageAsync("Korean");
            Assert.Equal("Korean", context.CurrentLanguage);
            Assert.Equal("시작", context.GetText("Button_Start"));
            Assert.Equal("환영합니다!", context.GetText("Message_Welcome"));
            
            // Act & Assert - Japanese
            await context.LoadLanguageAsync("Japanese");
            Assert.Equal("Japanese", context.CurrentLanguage);
            Assert.Equal("スタート", context.GetText("Button_Start"));
            Assert.Equal("ようこそ！", context.GetText("Message_Welcome"));
        }
        
        [Fact]
        public async Task LocalizationContext_GetText_ReturnsKeyForMissingTranslation()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            var keyRepository = new DataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();
            await context.LoadLanguageAsync("English");
            
            // Act
            var text = context.GetText("NonExistentKey");
            
            // Assert
            Assert.Equal("[Missing: NonExistentKey]", text);
        }
        
        [Fact]
        public void LocalizationContext_GetAllKeys_ReturnsEmptyWhenNotInitialized()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            // Act (without initialization)
            var keys = context.GetAllKeys();
            
            // Assert
            Assert.Empty(keys);
        }
        
        [Fact]
        public async Task LocalizationContext_DetectAvailableLanguages_FindsAllLanguageFiles()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            var keyRepository = new DataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            
            // Act - Initialize will also detect available languages
            await context.InitializeAsync();
            var languages = context.GetAvailableLanguages().ToList();
            
            // Assert
            Assert.Contains("English", languages);
            Assert.Contains("Korean", languages);
            Assert.Contains("Japanese", languages);
        }
        
        [Fact]
        public async Task LocalizationContext_SetTextAndSave_PreservesContextAndSavesCorrectly()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            var keyRepository = new DataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();
            await context.LoadLanguageAsync("English");
            
            // Act - Update text values
            context.SetText("Button_Start", "Begin");
            context.SetText("Button_Exit", "Quit");
            
            // Save the changes
            await context.SaveCurrentLanguageAsync();
            
            // Assert - Verify the saved content
            var savedContent = await rawDataProvider.LoadTextAsync("Localizations/English.csv");
            Assert.Contains("Id,Text,Context", savedContent);
            Assert.Contains("Button_Start,Begin,Main Menu", savedContent);
            Assert.Contains("Button_Exit,Quit,Main Menu", savedContent);
            
            // Verify context is preserved
            Assert.Contains("Main Menu", savedContent);
            Assert.Contains("First Login", savedContent);
        }
    }
}