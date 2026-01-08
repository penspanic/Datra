using System.Collections;
using System.Linq;
using Datra.Localization;
using Datra.SampleData.Generated;
using Datra.Unity.Editor.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Integration tests for localization features including
    /// GetAvailableLanguages() and LanguageInfo functionality.
    /// </summary>
    public class LocalizationIntegrationTests
    {
        private const string SampleDataBasePath = "Packages/com.penspanic.datra.sampledata/Resources";

        #region GetAvailableLanguages Integration Tests

        [UnityTest]
        public IEnumerator LocalizationContext_GetAvailableLanguages_ReturnsLanguagesWithFiles()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message}");
            }

            var availableLanguages = context.Localization.GetAvailableLanguages().ToList();

            // Assert - Should have at least one language
            Assert.Greater(availableLanguages.Count, 0, "Should detect at least one language file");
            Debug.Log($"Available languages: {string.Join(", ", availableLanguages.Select(l => l.ToIsoCode()))}");
        }

        [UnityTest]
        public IEnumerator LocalizationContext_GetAvailableLanguages_WithLanguageInfo_ReturnsMetadata()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message}");
            }

            var availableLanguages = context.Localization.GetAvailableLanguages().ToList();
            var languageInfos = availableLanguages.Select(l => l.GetLanguageInfo()).ToList();

            // Assert - Each language should have valid metadata
            foreach (var info in languageInfos)
            {
                Assert.IsFalse(string.IsNullOrEmpty(info.IsoCode), $"IsoCode should not be empty for {info.Code}");
                Assert.IsFalse(string.IsNullOrEmpty(info.NativeName), $"NativeName should not be empty for {info.Code}");
                Assert.IsFalse(string.IsNullOrEmpty(info.EnglishName), $"EnglishName should not be empty for {info.Code}");

                Debug.Log($"Language: {info.Code} | ISO: {info.IsoCode} | Native: {info.NativeName} | English: {info.EnglishName}");
            }
        }

        [UnityTest]
        public IEnumerator LocalizationContext_GetAvailableLanguageIsoCodes_ReturnsIsoCodes()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message}");
            }

            var isoCodes = context.Localization.GetAvailableLanguageIsoCodes().ToList();

            // Assert
            Assert.Greater(isoCodes.Count, 0, "Should have at least one ISO code");

            foreach (var code in isoCodes)
            {
                Assert.IsFalse(string.IsNullOrEmpty(code), "ISO code should not be empty");
                Debug.Log($"Available ISO code: {code}");
            }
        }

        #endregion

        #region Language Switch with LanguageInfo Tests

        [UnityTest]
        public IEnumerator LocalizationContext_AfterLanguageSwitch_GetAvailableLanguages_StillWorks()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Load data
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message}");
            }

            var initialLanguages = context.Localization.GetAvailableLanguages().ToList();
            var initialCount = initialLanguages.Count;

            // Act - Switch language if multiple available
            if (initialCount > 1)
            {
                var otherLanguage = initialLanguages.First(l => l != context.Localization.CurrentLanguageCode);
                var switchTask = context.Localization.LoadLanguageAsync(otherLanguage);
                while (!switchTask.IsCompleted)
                {
                    yield return null;
                }
            }

            var afterSwitchLanguages = context.Localization.GetAvailableLanguages().ToList();

            // Assert - Available languages should remain the same
            Assert.AreEqual(initialCount, afterSwitchLanguages.Count,
                "Available languages count should not change after switching");
        }

        #endregion

        #region LanguageInfo Display Tests

        [Test]
        public void LanguageInfo_CanBeUsedForUIDisplay()
        {
            // This test verifies that LanguageInfo can be used for UI purposes
            var testLanguages = new[] { LanguageCode.En, LanguageCode.Ko, LanguageCode.Ja, LanguageCode.ZhTW };

            foreach (var langCode in testLanguages)
            {
                var info = langCode.GetLanguageInfo();

                // Simulate UI label generation
                var uiLabel = $"{info.NativeName}"; // e.g., "한국어"
                var uiLabelWithEnglish = $"{info.NativeName} ({info.EnglishName})"; // e.g., "한국어 (Korean)"

                Assert.IsFalse(string.IsNullOrEmpty(uiLabel));
                Assert.IsFalse(string.IsNullOrEmpty(uiLabelWithEnglish));

                Debug.Log($"UI Label: {uiLabel} | Full: {uiLabelWithEnglish}");
            }
        }

        [Test]
        public void GetAllLanguageInfos_CanBeUsedForLanguageSelector()
        {
            // This test simulates building a language selector dropdown
            var allLanguageInfos = LanguageCodeExtensions.GetAllLanguageInfos().ToList();

            Assert.AreEqual(20, allLanguageInfos.Count, "Should have 20 supported languages");

            // Simulate dropdown options
            foreach (var info in allLanguageInfos)
            {
                var dropdownOption = new
                {
                    Value = info.Code,
                    DisplayText = info.NativeName,
                    Tooltip = $"{info.EnglishName} ({info.IsoCode})"
                };

                Assert.IsNotNull(dropdownOption.Value);
                Assert.IsFalse(string.IsNullOrEmpty(dropdownOption.DisplayText));
            }
        }

        #endregion
    }
}
