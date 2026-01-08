using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Datra.Localization;
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
                _files["Localizations/en.csv"] = @"Id,Text,Context
Button_Start,Start,Main Menu
Button_Exit,Exit,Main Menu
Button_Settings,Settings,Main Menu
Message_Welcome,Welcome!,First Login
Message_Error,An error occurred,Error Dialog
Character_Hero_Name,Hero,Character Info
Character_Hero_Desc,A brave warrior,Character Info";

                _files["Localizations/ko.csv"] = @"Id,Text,Context
Button_Start,시작,메인 메뉴
Button_Exit,종료,메인 메뉴
Button_Settings,설정,메인 메뉴
Message_Welcome,환영합니다!,첫 접속
Message_Error,오류가 발생했습니다,오류 대화상자
Character_Hero_Name,용사,캐릭터 정보
Character_Hero_Desc,용감한 전사,캐릭터 정보";

                _files["Localizations/ja.csv"] = @"Id,Text,Context
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
                            Category = categoryIndex >= 0 && values.Length > categoryIndex ? values[categoryIndex] : string.Empty
                        };
                        result[data.Id] = data;
                    }
                }
                
                return result;
            }
            
            public static string SerializeCsv(Dictionary<string, LocalizationKeyData> data, object? config)
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
            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
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
            
            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
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
            
            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
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
            
            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();
            
            // Act
            await context.LoadLanguageAsync(LanguageCode.Ko);
            
            // Assert
            Assert.Equal("ko", context.CurrentLanguage);
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
            
            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();
            
            // Act & Assert - English
            await context.LoadLanguageAsync(LanguageCode.En);
            Assert.Equal("en", context.CurrentLanguage);
            Assert.Equal("Start", context.GetText("Button_Start"));
            Assert.Equal("Welcome!", context.GetText("Message_Welcome"));
            
            // Act & Assert - Korean
            await context.LoadLanguageAsync(LanguageCode.Ko);
            Assert.Equal("ko", context.CurrentLanguage);
            Assert.Equal("시작", context.GetText("Button_Start"));
            Assert.Equal("환영합니다!", context.GetText("Message_Welcome"));
            
            // Act & Assert - Japanese
            await context.LoadLanguageAsync(LanguageCode.Ja);
            Assert.Equal("ja", context.CurrentLanguage);
            Assert.Equal("スタート", context.GetText("Button_Start"));
            Assert.Equal("ようこそ！", context.GetText("Message_Welcome"));
        }
        
        [Fact]
        public async Task LocalizationContext_GetText_ReturnsKeyForMissingTranslation()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();
            await context.LoadLanguageAsync(LanguageCode.En);
            
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

            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
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
            Assert.Contains(LanguageCode.En, languages);
            Assert.Contains(LanguageCode.Ko, languages);
            Assert.Contains(LanguageCode.Ja, languages);
        }

        [Fact]
        public async Task LocalizationContext_GetAvailableLanguages_ReturnsOnlyExistingFiles()
        {
            // Arrange - Provider with only 2 language files (en, ko - no ja)
            var rawDataProvider = new PartialLanguageTestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);

            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );

            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();

            // Act
            var languages = context.GetAvailableLanguages().ToList();

            // Assert - Only en and ko should be detected (no ja)
            Assert.Equal(2, languages.Count);
            Assert.Contains(LanguageCode.En, languages);
            Assert.Contains(LanguageCode.Ko, languages);
            Assert.DoesNotContain(LanguageCode.Ja, languages);
        }

        [Fact]
        public async Task LocalizationContext_GetAvailableLanguageIsoCodes_ReturnsCorrectCodes()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);

            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );

            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();

            // Act
            var isoCodes = context.GetAvailableLanguageIsoCodes().ToList();

            // Assert
            Assert.Contains("en", isoCodes);
            Assert.Contains("ko", isoCodes);
            Assert.Contains("ja", isoCodes);
        }

        [Fact]
        public async Task LocalizationContext_GetAvailableLanguages_WithLanguageInfo_ReturnsCorrectMetadata()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);

            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );

            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();

            // Act - Get available languages and their info
            var availableLanguages = context.GetAvailableLanguages().ToList();
            var languageInfos = availableLanguages.Select(l => l.GetLanguageInfo()).ToList();

            // Assert - Verify metadata for available languages
            var koreanInfo = languageInfos.FirstOrDefault(i => i.Code == LanguageCode.Ko);
            Assert.Equal("ko", koreanInfo.IsoCode);
            Assert.Equal("한국어", koreanInfo.NativeName);
            Assert.Equal("Korean", koreanInfo.EnglishName);

            var japaneseInfo = languageInfos.FirstOrDefault(i => i.Code == LanguageCode.Ja);
            Assert.Equal("ja", japaneseInfo.IsoCode);
            Assert.Equal("日本語", japaneseInfo.NativeName);
            Assert.Equal("Japanese", japaneseInfo.EnglishName);
        }

        [Fact]
        public async Task LocalizationContext_GetAvailableLanguages_AfterLanguageSwitch_StillReturnsAll()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);

            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );

            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();

            // Act - Switch languages multiple times
            await context.LoadLanguageAsync(LanguageCode.Ko);
            var languagesAfterKo = context.GetAvailableLanguages().ToList();

            await context.LoadLanguageAsync(LanguageCode.Ja);
            var languagesAfterJa = context.GetAvailableLanguages().ToList();

            await context.LoadLanguageAsync(LanguageCode.En);
            var languagesAfterEn = context.GetAvailableLanguages().ToList();

            // Assert - Available languages should remain consistent
            Assert.Equal(3, languagesAfterKo.Count);
            Assert.Equal(3, languagesAfterJa.Count);
            Assert.Equal(3, languagesAfterEn.Count);
        }

        /// <summary>
        /// Test provider with only partial language files (en, ko - missing ja)
        /// </summary>
        private class PartialLanguageTestRawDataProvider : IRawDataProvider
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

            public PartialLanguageTestRawDataProvider()
            {
                _files["Localizations/LocalizationKeys.csv"] = @"Id,Description,Category
Button_Start,Start button text,UI
Button_Exit,Exit button text,UI";

                _files["Localizations/en.csv"] = @"Id,Text,Context
Button_Start,Start,Main Menu
Button_Exit,Exit,Main Menu";

                _files["Localizations/ko.csv"] = @"Id,Text,Context
Button_Start,시작,메인 메뉴
Button_Exit,종료,메인 메뉴";

                // Note: ja.csv is intentionally missing
            }

            public bool Exists(string path) => _files.ContainsKey(path);

            public Task<string> LoadTextAsync(string path)
            {
                if (_files.TryGetValue(path, out var content))
                    return Task.FromResult(content);
                throw new System.IO.FileNotFoundException($"File not found: {path}");
            }

            public Task SaveTextAsync(string path, string text)
            {
                _files[path] = text;
                return Task.CompletedTask;
            }

            public string ResolveFilePath(string path) => path;
        }
        
        [Fact]
        public async Task LocalizationContext_SetTextAndSave_PreservesContextAndSavesCorrectly()
        {
            // Arrange
            var rawDataProvider = new TestRawDataProvider();
            var context = new LocalizationContext(rawDataProvider);
            
            var keyRepository = new KeyValueDataRepository<string, LocalizationKeyData>(
                "Localizations/LocalizationKeys.csv",
                rawDataProvider,
                (data) => TestLocalizationKeyDataSerializer.DeserializeCsv(data, null),
                (table) => TestLocalizationKeyDataSerializer.SerializeCsv(table, null)
            );
            
            context.SetKeyRepository(keyRepository);
            await context.InitializeAsync();
            await context.LoadLanguageAsync(LanguageCode.En);
            
            // Act - Update text values
            context.SetText("Button_Start", "Begin");
            context.SetText("Button_Exit", "Quit");
            
            // Save the changes
            await context.SaveCurrentLanguageAsync();
            
            // Assert - Verify the saved content
            var savedContent = await rawDataProvider.LoadTextAsync("Localizations/en.csv");
            Assert.Contains("Id,Text,Context", savedContent);
            Assert.Contains("Button_Start,Begin,Main Menu", savedContent);
            Assert.Contains("Button_Exit,Quit,Main Menu", savedContent);
            
            // Verify context is preserved
            Assert.Contains("Main Menu", savedContent);
            Assert.Contains("First Login", savedContent);
        }

        #region Single-File Localization Tests

        private class SingleFileTestRawDataProvider : IRawDataProvider
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

            public SingleFileTestRawDataProvider()
            {
                // Add test single-file localization CSV (horizontal format)
                _files["Localization.csv"] = @"Key,~Description,ko,en,ja,zh-TW
Button_Start,시작 버튼,시작,Start,スタート,開始
Button_Exit,종료 버튼,종료,Exit,終了,結束
Message_Welcome,환영 메시지,환영합니다!,Welcome!,ようこそ！,歡迎！";
            }

            public void AddFile(string path, string content) => _files[path] = content;
            public string GetFile(string path) => _files.TryGetValue(path, out var content) ? content : null!;

            public bool Exists(string path) => _files.ContainsKey(path);

            public Task<byte[]> LoadAsync(string path)
            {
                if (_files.TryGetValue(path, out var content))
                    return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(content));
                throw new System.IO.FileNotFoundException($"File not found: {path}");
            }

            public Task<string> LoadTextAsync(string path)
            {
                if (_files.TryGetValue(path, out var content))
                    return Task.FromResult(content);
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
                return _files.Keys.Where(k => k.StartsWith(directory)).ToArray();
            }

            public string ResolveFilePath(string path) => path;
        }

        [Fact]
        public async Task SingleFile_LoadsAllLanguages()
        {
            // Arrange
            var rawDataProvider = new SingleFileTestRawDataProvider();
            var config = new Configuration.DatraConfigurationValue(
                enableLocalization: true,
                useSingleFileLocalization: true,
                singleLocalizationFilePath: "Localization.csv",
                localizationKeyColumn: "Key"
            );

            var context = new LocalizationContext(rawDataProvider, config: config);

            // Act
            await context.InitializeAsync();

            // Assert - All languages should be loaded
            var availableLanguages = context.GetAvailableLanguages().ToList();
            Assert.Contains(LanguageCode.Ko, availableLanguages);
            Assert.Contains(LanguageCode.En, availableLanguages);
            Assert.Contains(LanguageCode.Ja, availableLanguages);
            Assert.Contains(LanguageCode.ZhTW, availableLanguages);
        }

        [Fact]
        public async Task SingleFile_GetText_ReturnsCorrectTranslation()
        {
            // Arrange
            var rawDataProvider = new SingleFileTestRawDataProvider();
            var config = new Configuration.DatraConfigurationValue(
                enableLocalization: true,
                useSingleFileLocalization: true,
                singleLocalizationFilePath: "Localization.csv",
                localizationKeyColumn: "Key",
                defaultLanguage: "ko"
            );

            var context = new LocalizationContext(rawDataProvider, config: config);
            await context.InitializeAsync();

            // Act & Assert - Korean (default)
            Assert.Equal("시작", context.GetText("Button_Start"));
            Assert.Equal("환영합니다!", context.GetText("Message_Welcome"));

            // Switch to English
            await context.LoadLanguageAsync(LanguageCode.En);
            Assert.Equal("Start", context.GetText("Button_Start"));
            Assert.Equal("Welcome!", context.GetText("Message_Welcome"));

            // Switch to Japanese
            await context.LoadLanguageAsync(LanguageCode.Ja);
            Assert.Equal("スタート", context.GetText("Button_Start"));
            Assert.Equal("ようこそ！", context.GetText("Message_Welcome"));
        }

        [Fact]
        public async Task SingleFile_SetText_UpdatesValue()
        {
            // Arrange
            var rawDataProvider = new SingleFileTestRawDataProvider();
            var config = new Configuration.DatraConfigurationValue(
                enableLocalization: true,
                useSingleFileLocalization: true,
                singleLocalizationFilePath: "Localization.csv",
                localizationKeyColumn: "Key",
                defaultLanguage: "en"
            );

            var context = new LocalizationContext(rawDataProvider, config: config);
            await context.InitializeAsync();

            // Act
            context.SetText("Button_Start", "Begin");

            // Assert
            Assert.Equal("Begin", context.GetText("Button_Start"));
        }

        [Fact]
        public async Task SingleFile_Save_WritesHorizontalFormat()
        {
            // Arrange
            var rawDataProvider = new SingleFileTestRawDataProvider();
            var config = new Configuration.DatraConfigurationValue(
                enableLocalization: true,
                useSingleFileLocalization: true,
                singleLocalizationFilePath: "Localization.csv",
                localizationKeyColumn: "Key",
                defaultLanguage: "en"
            );

            var context = new LocalizationContext(rawDataProvider, config: config);
            await context.InitializeAsync();

            // Act - Modify and save
            context.SetText("Button_Start", "Begin", LanguageCode.En);
            await context.SaveCurrentLanguageAsync();

            // Assert - Check saved content
            var savedContent = rawDataProvider.GetFile("Localization.csv");
            Assert.Contains("Key,", savedContent); // Header starts with Key column
            Assert.Contains("Button_Start", savedContent);
            Assert.Contains("Begin", savedContent); // Updated English value
            Assert.Contains("시작", savedContent); // Korean value preserved
        }

        [Fact]
        public async Task SingleFile_WithTypeRow_SkipsTypeRow()
        {
            // Arrange - CSV with type declaration row (like PetroHunter format)
            var rawDataProvider = new SingleFileTestRawDataProvider();
            rawDataProvider.AddFile("Localization.csv", @"Key,~Description,ko,en
string,~string,string,string
Button_Start,시작 버튼,시작,Start
Button_Exit,종료 버튼,종료,Exit");

            var config = new Configuration.DatraConfigurationValue(
                enableLocalization: true,
                useSingleFileLocalization: true,
                singleLocalizationFilePath: "Localization.csv",
                localizationKeyColumn: "Key",
                defaultLanguage: "ko"
            );

            var context = new LocalizationContext(rawDataProvider, config: config);

            // Act
            await context.InitializeAsync();

            // Assert - Should skip the type row and load actual data
            Assert.Equal("시작", context.GetText("Button_Start"));
            Assert.Equal("종료", context.GetText("Button_Exit"));

            // Should NOT have "string" as a key
            Assert.Equal("[Missing: string]", context.GetText("string"));
        }

        [Fact]
        public async Task SingleFile_WithCustomKeyColumn_Works()
        {
            // Arrange - CSV with StringId as key column (like PetroHunter format)
            var rawDataProvider = new SingleFileTestRawDataProvider();
            rawDataProvider.AddFile("Localization.csv", @"Id,StringId,~Description,ko,en
int,string,~string,string,string
0,Name_Hero,영웅 이름,영웅,Hero
1,Name_Villain,악당 이름,악당,Villain");

            var config = new Configuration.DatraConfigurationValue(
                enableLocalization: true,
                useSingleFileLocalization: true,
                singleLocalizationFilePath: "Localization.csv",
                localizationKeyColumn: "StringId",
                defaultLanguage: "en"
            );

            var context = new LocalizationContext(rawDataProvider, config: config);

            // Act
            await context.InitializeAsync();

            // Assert - Should use StringId as key
            Assert.Equal("Hero", context.GetText("Name_Hero"));
            Assert.Equal("Villain", context.GetText("Name_Villain"));

            // Switch to Korean
            await context.LoadLanguageAsync(LanguageCode.Ko);
            Assert.Equal("영웅", context.GetText("Name_Hero"));
            Assert.Equal("악당", context.GetText("Name_Villain"));
        }

        #endregion
    }
}