using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Datra.DataTypes;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Services;
using Datra.Serializers;
using Datra.SampleData.Generated;

namespace Datra.Tests
{
    public class LocalizationTests
    {
        private class MockLocalizationContext : ILocalizationContext
        {
            private readonly Dictionary<string, string> _texts = new Dictionary<string, string>();
            public string CurrentLanguage { get; private set; }
            
            public MockLocalizationContext()
            {
                // Initialize with test data
                _texts["Button_Start"] = "Start";
                _texts["Button_Exit"] = "Exit";
                _texts["Message_Welcome"] = "Welcome!";
                _texts["Character_Hero_Name"] = "Hero";
                _texts["Character_Hero_Desc"] = "A brave warrior";
                CurrentLanguage = "en";
            }
            
            public string GetText(string key)
            {
                return _texts.TryGetValue(key, out var text) ? text : $"[{key}]";
            }
            
            public Task LoadLanguageAsync(LanguageCode languageCode)
            {
                CurrentLanguage = languageCode.ToIsoCode();
                
                // Simulate loading different languages
                if (languageCode == LanguageCode.Ko)
                {
                    _texts["Button_Start"] = "시작";
                    _texts["Button_Exit"] = "종료";
                    _texts["Message_Welcome"] = "환영합니다!";
                    _texts["Character_Hero_Name"] = "용사";
                    _texts["Character_Hero_Desc"] = "용감한 전사";
                }
                else if (languageCode == LanguageCode.Ja)
                {
                    _texts["Button_Start"] = "スタート";
                    _texts["Button_Exit"] = "終了";
                    _texts["Message_Welcome"] = "ようこそ！";
                    _texts["Character_Hero_Name"] = "勇者";
                    _texts["Character_Hero_Desc"] = "勇敢な戦士";
                }
                else
                {
                    // Default to English
                    _texts["Button_Start"] = "Start";
                    _texts["Button_Exit"] = "Exit";
                    _texts["Message_Welcome"] = "Welcome!";
                    _texts["Character_Hero_Name"] = "Hero";
                    _texts["Character_Hero_Desc"] = "A brave warrior";
                }
                
                return Task.CompletedTask;
            }
            
            public Task LoadLanguageAsync(string languageCode)
            {
                var code = LanguageCodeExtensions.TryParse(languageCode);
                if (code.HasValue)
                {
                    return LoadLanguageAsync(code.Value);
                }
                throw new ArgumentException($"Invalid language code: {languageCode}");
            }
            
            public bool HasKey(string key)
            {
                return _texts.ContainsKey(key);
            }
        }
        
        [Fact]
        public void LocaleRef_Creation_Works()
        {
            // Arrange & Act
            LocaleRef localeRef = "Button_Start";
            
            // Assert
            Assert.Equal("Button_Start", localeRef.Key);
            Assert.True(localeRef.HasValue);
        }
        
        [Fact]
        public void LocaleRef_EmptyKey_HasNoValue()
        {
            // Arrange & Act
            LocaleRef localeRef = "";
            LocaleRef nullRef = null;
            
            // Assert
            Assert.False(localeRef.HasValue);
            Assert.False(nullRef.HasValue);
        }
        
        [Fact]
        public void LocaleRef_Evaluate_WithContext_ReturnsLocalizedText()
        {
            // Arrange
            var context = new MockLocalizationContext();
            LocaleRef localeRef = "Button_Start";
            
            // Act
            var result = localeRef.Evaluate(context);
            
            // Assert
            Assert.Equal("Start", result);
        }
        
        [Fact]
        public async Task LocaleRef_Evaluate_AfterLanguageChange_ReturnsCorrectText()
        {
            // Arrange
            var context = new MockLocalizationContext();
            LocaleRef localeRef = "Button_Start";
            
            // Act & Assert - English
            var englishResult = localeRef.Evaluate(context);
            Assert.Equal("Start", englishResult);
            
            // Act & Assert - Korean
            await context.LoadLanguageAsync(LanguageCode.Ko);
            var koreanResult = localeRef.Evaluate(context);
            Assert.Equal("시작", koreanResult);
            
            // Act & Assert - Japanese
            await context.LoadLanguageAsync(LanguageCode.Ja);
            var japaneseResult = localeRef.Evaluate(context);
            Assert.Equal("スタート", japaneseResult);
        }
        
        [Fact]
        public void LocaleRef_Evaluate_MissingKey_ReturnsFallback()
        {
            // Arrange
            var context = new MockLocalizationContext();
            LocaleRef localeRef = "NonExistent_Key";
            
            // Act
            var result = localeRef.Evaluate(context);
            
            // Assert
            Assert.Equal("[NonExistent_Key]", result);
        }
        
        [Fact]
        public void LocaleRef_Equality_Works()
        {
            // Arrange
            LocaleRef ref1 = "Button_Start";
            LocaleRef ref2 = "Button_Start";
            LocaleRef ref3 = "Button_Exit";
            
            // Assert
            Assert.Equal(ref1, ref2);
            Assert.NotEqual(ref1, ref3);
            Assert.True(ref1 == ref2);
            Assert.True(ref1 != ref3);
        }
        
        [Fact]
        public void LocaleRef_NullContext_ThrowsException()
        {
            // Arrange
            LocaleRef localeRef = "Button_Start";
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => localeRef.Evaluate((ILocalizationContext)null));
            Assert.Throws<ArgumentNullException>(() => localeRef.Evaluate((ILocalizationService)null));
        }
    }
}