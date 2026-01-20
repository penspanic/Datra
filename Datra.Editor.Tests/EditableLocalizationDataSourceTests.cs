#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Configuration;
using Datra.Editor.DataSources;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Services;
using Xunit;

namespace Datra.Editor.Tests
{
    /// <summary>
    /// Tests for EditableLocalizationDataSource, specifically testing the issue where
    /// changes made directly to LocalizationContext bypass the baseline tracking.
    /// </summary>
    public class EditableLocalizationDataSourceTests
    {
        private class TestRawDataProvider : IRawDataProvider
        {
            private readonly Dictionary<string, string> _files = new();

            public TestRawDataProvider()
            {
                // Single-file localization format
                _files["Localization.csv"] = @"Key,ko,en,ja
Stage.1.Name,스테이지1,Stage 01,ステージ1
Stage.1.MonsterName,랩터,Raptor,ラプター
Stage.2.Name,스테이지2,Stage 02,ステージ2
Stage.2.MonsterName,리자드,Lizard,リザード";
            }

            public void SetFile(string path, string content) => _files[path] = content;
            public string? GetFile(string path) => _files.TryGetValue(path, out var content) ? content : null;

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

        private async Task<(LocalizationContext context, EditableLocalizationDataSource dataSource, TestRawDataProvider provider)> CreateTestSetup()
        {
            var provider = new TestRawDataProvider();
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                useSingleFileLocalization: true,
                singleLocalizationFilePath: "Localization.csv",
                localizationKeyColumn: "Key",
                defaultLanguage: "en"
            );

            var context = new LocalizationContext(provider, config: config);
            await context.InitializeAsync();

            var dataSource = new EditableLocalizationDataSource(context);

            // Initialize baselines for all loaded languages
            foreach (var lang in context.GetLoadedLanguages())
            {
                dataSource.InitializeBaseline(lang);
            }

            return (context, dataSource, provider);
        }

        [Fact]
        public async Task Debug_CheckBaselineInitialization()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();

            // Debug: Check what keys are available
            var allKeys = context.GetAllKeys().ToList();
            var loadedLanguages = context.GetLoadedLanguages().ToList();

            // Check if baseline is initialized for loaded languages
            foreach (var lang in loadedLanguages)
            {
                var isInitialized = dataSource.IsLanguageInitialized(lang);

                // Try to get text for a known key
                var text = context.GetText("Stage.1.Name", lang);
            }

            // The issue: GetAllKeys() returns empty because KeyRepository is not set!
            // In single-file mode, keys are stored in _languageData, not KeyRepository
            Assert.NotEmpty(allKeys); // This will likely FAIL - revealing the bug
        }

        [Fact]
        public async Task DirectContextSetText_DoesNotTrackBaseline()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();

            // Act - Directly call LocalizationContext.SetText() (bypassing dataSource)
            // This simulates what LocaleEditPopup was doing before the fix
            context.SetText("Stage.1.Name", "Modified Stage 01", LanguageCode.En);

            // Assert - DataSource should NOT detect this change
            // because baseline tracking was bypassed
            Assert.False(dataSource.IsKeyModified("Stage.1.Name", LanguageCode.En),
                "Direct context SetText should NOT be tracked by dataSource baseline");
            Assert.False(dataSource.HasModifications,
                "DataSource should NOT show modifications for direct context changes");
        }

        [Fact]
        public async Task DataSourceSetText_TracksBaseline()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();

            // Act - Use EditableLocalizationDataSource.SetText() (correct approach)
            dataSource.SetText("Stage.1.Name", "Modified Stage 01", LanguageCode.En);

            // Assert - DataSource SHOULD detect this change
            Assert.True(dataSource.IsKeyModified("Stage.1.Name", LanguageCode.En),
                "DataSource SetText should be tracked by baseline");
            Assert.True(dataSource.HasModifications,
                "DataSource should show modifications");
        }

        [Fact]
        public async Task DataSourceSetText_FiresOnTextChangedEvent()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();
            string? firedKey = null;
            LanguageCode? firedLang = null;

            dataSource.OnTextChanged += (key, lang) =>
            {
                firedKey = key;
                firedLang = lang;
            };

            // Act
            dataSource.SetText("Stage.1.Name", "Modified Stage 01", LanguageCode.En);

            // Assert
            Assert.Equal("Stage.1.Name", firedKey);
            Assert.Equal(LanguageCode.En, firedLang);
        }

        [Fact]
        public async Task DirectContextSetText_DoesNotFireDataSourceEvent()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();
            bool eventFired = false;

            dataSource.OnTextChanged += (key, lang) =>
            {
                eventFired = true;
            };

            // Act - Direct context call
            context.SetText("Stage.1.Name", "Modified Stage 01", LanguageCode.En);

            // Assert - DataSource event should NOT fire
            Assert.False(eventFired,
                "Direct context SetText should NOT fire dataSource.OnTextChanged event");
        }

        [Fact]
        public async Task NewKey_SetViaDataSource_IsTracked()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();

            // Act - Set text for a key that doesn't exist yet (simulates adding new locale from table)
            dataSource.SetText("Stage.0.MonsterName", "New Monster", LanguageCode.En);

            // Assert
            Assert.Equal("New Monster", context.GetText("Stage.0.MonsterName", LanguageCode.En));
            Assert.True(dataSource.HasModifications);
        }

        [Fact]
        public async Task NewKey_SetDirectlyOnContext_NotTracked()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();

            // Act - Set text directly on context (bypassing dataSource)
            context.SetText("Stage.0.MonsterName", "New Monster", LanguageCode.En);

            // Assert - Value is set in context but NOT tracked by dataSource
            Assert.Equal("New Monster", context.GetText("Stage.0.MonsterName", LanguageCode.En));
            Assert.False(dataSource.HasModifications,
                "Direct context changes should not be tracked by dataSource");
        }

        [Fact]
        public async Task MultipleLanguages_SetViaDataSource_AllTracked()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();

            // Act - Set text in multiple languages
            dataSource.SetText("Stage.1.Name", "Modified EN", LanguageCode.En);
            dataSource.SetText("Stage.1.Name", "수정된 KO", LanguageCode.Ko);
            dataSource.SetText("Stage.1.Name", "修正JA", LanguageCode.Ja);

            // Assert
            Assert.True(dataSource.IsKeyModified("Stage.1.Name", LanguageCode.En));
            Assert.True(dataSource.IsKeyModified("Stage.1.Name", LanguageCode.Ko));
            Assert.True(dataSource.IsKeyModified("Stage.1.Name", LanguageCode.Ja));
            Assert.True(dataSource.HasModifications);
        }

        [Fact]
        public async Task Revert_ClearsModifications()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();

            dataSource.SetText("Stage.1.Name", "Modified", LanguageCode.En);
            Assert.True(dataSource.HasModifications);

            // Act
            dataSource.Revert();

            // Assert
            Assert.False(dataSource.HasModifications);
            Assert.Equal("Stage 01", context.GetText("Stage.1.Name", LanguageCode.En));
        }

        [Fact]
        public async Task Save_ClearsModificationsAndUpdatesBaseline()
        {
            // Arrange
            var (context, dataSource, provider) = await CreateTestSetup();

            dataSource.SetText("Stage.1.Name", "Saved Value", LanguageCode.En);
            Assert.True(dataSource.HasModifications);

            // Act
            await dataSource.SaveAsync();

            // Assert
            Assert.False(dataSource.HasModifications);

            // Verify saved to file
            var savedContent = provider.GetFile("Localization.csv");
            Assert.Contains("Saved Value", savedContent);
        }

        /// <summary>
        /// This test reproduces the exact bug scenario:
        /// LocaleEditPopup calls LocalizationContext.SetText() directly,
        /// which updates the memory but doesn't track the change in EditableLocalizationDataSource.
        /// </summary>
        [Fact]
        public async Task BugRepro_LocaleEditPopup_DirectContextCall_NotReflectedInLocalizationView()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();

            // Simulate what LocaleEditPopup was doing (BEFORE the fix):
            // It called localizationContext.SetText() directly instead of localizationDataSource.SetText()

            // This is what LocaleEditPopup.ApplyChanges() was doing:
            var localeKey = "Stage.0.MonsterName"; // New key that doesn't exist
            var newText = "변경된값";
            var languageCode = LanguageCode.En;

            // Get current text (will return "[Missing: Stage.0.MonsterName]")
            var currentText = context.GetText(localeKey, languageCode);
            Assert.Equal("[Missing: Stage.0.MonsterName]", currentText);

            // The bug: calling context directly instead of dataSource
            context.SetText(localeKey, newText, languageCode);

            // Assert - The value IS updated in context
            Assert.Equal(newText, context.GetText(localeKey, languageCode));

            // But the dataSource doesn't know about it!
            Assert.False(dataSource.IsKeyModified(localeKey, languageCode),
                "BUG: DataSource doesn't track direct context changes");
            Assert.False(dataSource.HasModifications,
                "BUG: DataSource shows no modifications even though context was changed");

            // This means the Localization table view won't show the change as modified,
            // and the save button won't be enabled!
        }

        /// <summary>
        /// This test shows the correct behavior after the fix:
        /// LocaleEditPopup should call EditableLocalizationDataSource.SetText()
        /// </summary>
        [Fact]
        public async Task Fixed_LocaleEditPopup_DataSourceCall_ReflectedCorrectly()
        {
            // Arrange
            var (context, dataSource, _) = await CreateTestSetup();

            // Simulate the FIXED behavior:
            // LocaleEditPopup.ApplyChanges() now calls localizationDataSource.SetText()

            var localeKey = "Stage.0.MonsterName";
            var newText = "변경된값";
            var languageCode = LanguageCode.En;

            // The fix: calling dataSource instead of context directly
            dataSource.SetText(localeKey, newText, languageCode);

            // Assert - The value IS updated in context
            Assert.Equal(newText, context.GetText(localeKey, languageCode));

            // AND the dataSource tracks it!
            Assert.True(dataSource.HasModifications,
                "FIXED: DataSource correctly shows modifications");

            // The Localization table view will now show the change,
            // and the save button will be enabled!
        }

        #region KeyRepository Setup for Bug Reproduction

        /// <summary>
        /// Creates a test setup WITH KeyRepository (like PetroHunter).
        /// This is where the bug manifests because GetAllKeys() returns _keyRepository.Keys
        /// instead of collecting from _languageData.
        /// </summary>
        private async Task<(LocalizationContext context, EditableLocalizationDataSource dataSource, TestRawDataProvider provider)> CreateTestSetupWithKeyRepository()
        {
            var provider = new TestRawDataProvider();

            // Add key repository file
            provider.SetFile("LocalizationKeys.csv", @"Id,Description,Category,IsFixedKey
Stage.1.Name,Stage 1 name,Stage,false
Stage.1.MonsterName,Stage 1 monster,Stage,false
Stage.2.Name,Stage 2 name,Stage,false
Stage.2.MonsterName,Stage 2 monster,Stage,false");

            // Single-file localization
            var config = new DatraConfigurationValue(
                enableLocalization: true,
                useSingleFileLocalization: true,
                singleLocalizationFilePath: "Localization.csv",
                localizationKeyColumn: "Key",
                defaultLanguage: "en"
            );

            var context = new LocalizationContext(provider, config: config);
            await context.InitializeAsync();

            // Create and set KeyRepository (this is what PetroHunter does)
            // Use CSV deserialize function
            var keyRepo = new Datra.Repositories.KeyValueDataRepository<string, Datra.Models.LocalizationKeyData>(
                "LocalizationKeys.csv",
                provider,
                csvContent => ParseLocalizationKeysCsv(csvContent),
                dict => SerializeLocalizationKeysCsv(dict)
            );
            await keyRepo.InitializeAsync();
            context.SetKeyRepository(keyRepo);

            var dataSource = new EditableLocalizationDataSource(context);

            // Initialize baselines for all loaded languages
            foreach (var lang in context.GetLoadedLanguages())
            {
                dataSource.InitializeBaseline(lang);
            }

            return (context, dataSource, provider);
        }

        private static Dictionary<string, Datra.Models.LocalizationKeyData> ParseLocalizationKeysCsv(string csvContent)
        {
            var result = new Dictionary<string, Datra.Models.LocalizationKeyData>();
            var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= 1) return result;

            // Skip header
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length >= 4)
                {
                    var data = new Datra.Models.LocalizationKeyData
                    {
                        Id = parts[0],
                        Description = parts[1],
                        Category = parts[2],
                        IsFixedKey = bool.TryParse(parts[3], out var isFixed) && isFixed
                    };
                    result[data.Id] = data;
                }
            }

            return result;
        }

        private static string SerializeLocalizationKeysCsv(Dictionary<string, Datra.Models.LocalizationKeyData> data)
        {
            var lines = new List<string> { "Id,Description,Category,IsFixedKey" };
            foreach (var kvp in data.OrderBy(x => x.Key))
            {
                lines.Add($"{kvp.Value.Id},{kvp.Value.Description},{kvp.Value.Category},{kvp.Value.IsFixedKey.ToString().ToLower()}");
            }
            return string.Join("\n", lines);
        }

        #endregion

        #region GetAllKeys Bug Tests - New key via SetText not appearing in search (Single-file mode without KeyRepository)

        /// <summary>
        /// In single-file mode WITHOUT KeyRepository, SetText() works correctly
        /// because GetAllKeys() collects from _languageData.
        /// </summary>
        [Fact]
        public async Task SingleFileMode_NoKeyRepo_SetText_NewKey_IncludedInGetAllKeys()
        {
            // Arrange - Single-file mode WITHOUT KeyRepository
            var (context, dataSource, _) = await CreateTestSetup();
            var newKey = "Stage.0.MonsterName";

            // Act
            dataSource.SetText(newKey, "New Monster", LanguageCode.En);

            // Assert - In single-file mode without KeyRepository, this works
            var allKeys = dataSource.GetAllKeys().ToList();
            Assert.Contains(newKey, allKeys); // Works in this mode
        }

        #endregion

        #region GetAllKeys Bug Tests - WITH KeyRepository (like PetroHunter)

        /// <summary>
        /// FIXED: When SetText() is called for a non-existent key WITH KeyRepository present,
        /// the key is now properly tracked in _addedKeys and returned by GetAllKeys().
        ///
        /// This is the exact scenario in PetroHunter where:
        /// - KeyRepository is set (from LocalizationKeys.csv)
        /// - LocaleEditPopup calls SetText() for a new key (e.g., Stage.0.MonsterName)
        /// - Key is now added to _addedKeys
        /// - GetAllKeys() includes keys from _addedKeys
        ///
        /// Result: Key is saved, shows as modified, AND appears in LocalizationView search.
        /// </summary>
        [Fact]
        public async Task Fixed_WithKeyRepo_SetText_NewKey_NowIncludedInGetAllKeys()
        {
            // Arrange - WITH KeyRepository (like PetroHunter)
            var (context, dataSource, _) = await CreateTestSetupWithKeyRepository();
            var newKey = "Stage.0.MonsterName"; // Key NOT in KeyRepository

            // Verify key doesn't exist initially
            var initialKeys = dataSource.GetAllKeys().ToList();
            Assert.DoesNotContain(newKey, initialKeys);

            // Act - Set text for a non-existent key (simulates LocaleEditPopup editing a missing key)
            dataSource.SetText(newKey, "New Monster Name", LanguageCode.En);

            // Assert - The key now appears in GetAllKeys()
            var keysAfterSet = dataSource.GetAllKeys().ToList();

            // FIXED: Key IS in GetAllKeys() because SetText() now adds new keys to _addedKeys
            Assert.Contains(newKey, keysAfterSet);
        }

        /// <summary>
        /// FIXED: Value is saved to context AND GetAllKeys() includes it.
        /// No more data inconsistency.
        /// </summary>
        [Fact]
        public async Task Fixed_WithKeyRepo_SetText_NewKey_ValueSavedAndKeyListed()
        {
            // Arrange - WITH KeyRepository
            var (context, dataSource, _) = await CreateTestSetupWithKeyRepository();
            var newKey = "Stage.0.MonsterName";
            var newValue = "New Monster Name";

            // Act
            dataSource.SetText(newKey, newValue, LanguageCode.En);

            // Assert - Value IS in context
            var storedValue = context.GetText(newKey, LanguageCode.En);
            Assert.Equal(newValue, storedValue);

            // Assert - Key IS now in GetAllKeys() (FIXED!)
            var allKeys = dataSource.GetAllKeys().ToList();
            Assert.Contains(newKey, allKeys);
        }

        /// <summary>
        /// FIXED: Modifications ARE tracked AND key appears in GetAllKeys().
        /// Tab shows "(1)" AND key appears in search.
        /// </summary>
        [Fact]
        public async Task Fixed_WithKeyRepo_SetText_NewKey_ModificationTrackedAndKeyListed()
        {
            // Arrange - WITH KeyRepository
            var (context, dataSource, _) = await CreateTestSetupWithKeyRepository();
            var newKey = "Stage.0.MonsterName";

            // Act
            dataSource.SetText(newKey, "New Monster", LanguageCode.En);

            // Assert - Modification IS tracked (tab shows "(1)")
            Assert.True(dataSource.HasModifications, "Modification should be tracked");

            // Assert - Key IS now in GetAllKeys() (FIXED!)
            var allKeys = dataSource.GetAllKeys().ToList();
            Assert.Contains(newKey, allKeys);
        }

        /// <summary>
        /// Shows that AddKey() correctly adds to GetAllKeys(), but SetText() doesn't.
        /// This demonstrates the fix approach: SetText() should use AddKey() logic for new keys.
        /// </summary>
        [Fact]
        public async Task Comparison_WithKeyRepo_AddKey_IncludesKeyInGetAllKeys()
        {
            // Arrange - WITH KeyRepository
            var (context, dataSource, _) = await CreateTestSetupWithKeyRepository();
            var newKey = "Stage.0.MonsterName";

            // Act - Use AddKey() instead of SetText()
            dataSource.AddKey(newKey, "Description", "Category");

            // Assert - Key IS in GetAllKeys() when using AddKey()
            var allKeys = dataSource.GetAllKeys().ToList();
            Assert.Contains(newKey, allKeys); // AddKey() works correctly
        }

        /// <summary>
        /// FIXED: SetText for new key, then search - key IS found.
        /// The user's bug is now fixed.
        /// </summary>
        [Fact]
        public async Task Fixed_WithKeyRepo_FullScenario_SetTextThenSearch_KeyFound()
        {
            // Arrange - WITH KeyRepository (like PetroHunter)
            var (context, dataSource, _) = await CreateTestSetupWithKeyRepository();
            var searchKey = "Stage.0.MonsterName";
            var searchTerm = "stage.0.monster"; // User searches this

            // Act - Set text for non-existent key (like LocaleEditPopup does)
            dataSource.SetText(searchKey, "New Monster", LanguageCode.En);

            // Simulate LocalizationView search
            var allKeys = dataSource.GetAllKeys().ToList();
            var searchResults = allKeys
                .Where(k => k.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // Assert - FIXED: Search returns the key
            Assert.Single(searchResults);
            Assert.Equal(searchKey, searchResults[0]);

            // User now sees: "Shown: 1" in LocalizationView with the key visible!
        }

        /// <summary>
        /// Expected behavior after fix: SetText for new key should make it appear in GetAllKeys()
        /// </summary>
        [Fact]
        public async Task Expected_WithKeyRepo_SetText_NewKey_ShouldAppearInGetAllKeys()
        {
            // Arrange - WITH KeyRepository
            var (context, dataSource, _) = await CreateTestSetupWithKeyRepository();
            var newKey = "Stage.0.MonsterName";

            // Act
            dataSource.SetText(newKey, "New Monster", LanguageCode.En);

            // Assert - This is what SHOULD happen after the fix
            var allKeys = dataSource.GetAllKeys().ToList();

            // Currently FAILS, should PASS after fix:
            Assert.Contains(newKey, allKeys);
        }

        /// <summary>
        /// After Save and RefreshBaseline, the new key should still be in GetAllKeys()
        /// because it was added to _keyRepository during save.
        /// </summary>
        [Fact]
        public async Task Fixed_WithKeyRepo_SetText_NewKey_PersistsAfterSave()
        {
            // Arrange - WITH KeyRepository
            var (context, dataSource, _) = await CreateTestSetupWithKeyRepository();
            var newKey = "Stage.0.MonsterName";
            var newValue = "New Monster";

            // Act - Set text and save
            dataSource.SetText(newKey, newValue, LanguageCode.En);

            // Verify key is in GetAllKeys before save
            var keysBeforeSave = dataSource.GetAllKeys().ToList();
            Assert.Contains(newKey, keysBeforeSave);

            // Save (this calls RefreshBaseline which clears _addedKeys)
            await dataSource.SaveAsync();

            // Assert - Key should STILL be in GetAllKeys after save
            // because SaveInternalAsync adds new keys to _keyRepository
            var keysAfterSave = dataSource.GetAllKeys().ToList();
            Assert.Contains(newKey, keysAfterSave);

            // Also verify the value is preserved
            var savedValue = context.GetText(newKey, LanguageCode.En);
            Assert.Equal(newValue, savedValue);
        }

        /// <summary>
        /// Verifies that searching for a new key works after save.
        /// This is the complete user scenario.
        /// </summary>
        [Fact]
        public async Task Fixed_WithKeyRepo_FullScenario_SearchWorksAfterSave()
        {
            // Arrange - WITH KeyRepository
            var (context, dataSource, _) = await CreateTestSetupWithKeyRepository();
            var searchKey = "Stage.0.MonsterName";
            var searchTerm = "stage.0.monster";

            // Act - Set text, save, then search
            dataSource.SetText(searchKey, "New Monster", LanguageCode.En);
            await dataSource.SaveAsync();

            // Simulate LocalizationView search
            var allKeys = dataSource.GetAllKeys().ToList();
            var searchResults = allKeys
                .Where(k => k.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // Assert - Search should find the key even after save
            Assert.Single(searchResults);
            Assert.Equal(searchKey, searchResults[0]);
        }

        #endregion
    }
}
