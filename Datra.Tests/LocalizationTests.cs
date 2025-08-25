using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Datra.DataTypes;
using Datra.Interfaces;
using Datra.Services;
using Datra.Serializers;
using Datra.Generated;

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
                CurrentLanguage = "English";
            }
            
            public string GetText(string key)
            {
                return _texts.TryGetValue(key, out var text) ? text : $"[{key}]";
            }
            
            public Task LoadLanguageAsync(string languageCode)
            {
                CurrentLanguage = languageCode;
                
                // Simulate loading different languages
                if (languageCode == "Korean")
                {
                    _texts["Button_Start"] = "시작";
                    _texts["Button_Exit"] = "종료";
                    _texts["Message_Welcome"] = "환영합니다!";
                    _texts["Character_Hero_Name"] = "용사";
                    _texts["Character_Hero_Desc"] = "용감한 전사";
                }
                else if (languageCode == "Japanese")
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
            await context.LoadLanguageAsync("Korean");
            var koreanResult = localeRef.Evaluate(context);
            Assert.Equal("시작", koreanResult);
            
            // Act & Assert - Japanese
            await context.LoadLanguageAsync("Japanese");
            var japaneseResult = localeRef.Evaluate(context);
            Assert.Equal("スタート", japaneseResult);
        }
        
        [Fact]
        public void LocaleRef_Evaluate_WithService_ReturnsLocalizedText()
        {
            // Arrange
            var context = new MockLocalizationContext();
            var service = new LocalizationService(context);
            LocaleRef localeRef = "Message_Welcome";
            
            // Act
            var result = localeRef.Evaluate(service);
            
            // Assert
            Assert.Equal("Welcome!", result);
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
        public async Task LocalizationService_SetLanguage_ChangesCurrentLanguage()
        {
            // Arrange
            var context = new MockLocalizationContext();
            var service = new LocalizationService(context);
            
            // Act & Assert
            Assert.Equal("English", service.CurrentLanguage);
            
            await service.SetLanguageAsync("Korean");
            Assert.Equal("Korean", service.CurrentLanguage);
            
            await service.SetLanguageAsync("Japanese");
            Assert.Equal("Japanese", service.CurrentLanguage);
        }
        
        [Fact]
        public async Task LocalizationService_Localize_ReturnsCorrectText()
        {
            // Arrange
            var context = new MockLocalizationContext();
            var service = new LocalizationService(context);
            
            // Act & Assert - English
            Assert.Equal("Start", service.Localize("Button_Start"));
            Assert.Equal("Exit", service.Localize("Button_Exit"));
            
            // Act & Assert - Korean
            await service.SetLanguageAsync("Korean");
            Assert.Equal("시작", service.Localize("Button_Start"));
            Assert.Equal("종료", service.Localize("Button_Exit"));
        }
        
        [Fact]
        public void LocalizationService_Localize_WithFormatting_Works()
        {
            // Arrange
            var context = new MockLocalizationContext();
            var service = new LocalizationService(context);
            
            // Add a formatted text
            context.GetType()
                .GetField("_texts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(context, new Dictionary<string, string>
                {
                    ["Score_Format"] = "Your score is {0} points!"
                });
            
            // Act
            var result = service.Localize("Score_Format", 100);
            
            // Assert
            Assert.Equal("Your score is 100 points!", result);
        }
        
        [Fact]
        public void LocalizationService_HasKey_ReturnsCorrectResult()
        {
            // Arrange
            var context = new MockLocalizationContext();
            var service = new LocalizationService(context);
            
            // Act & Assert
            Assert.True(service.HasKey("Button_Start"));
            Assert.True(service.HasKey("Message_Welcome"));
            Assert.False(service.HasKey("NonExistent_Key"));
            Assert.False(service.HasKey(""));
            Assert.False(service.HasKey(null));
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
        
        [Fact]
        public async Task LocalizationService_NullLanguageCode_ThrowsException()
        {
            // Arrange
            var context = new MockLocalizationContext();
            var service = new LocalizationService(context);
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SetLanguageAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SetLanguageAsync(""));
        }
        
        [Fact]
        public async Task LocalizationIntegration_WithCharacterData_Works()
        {
            // Arrange
            var context = new MockLocalizationContext();
            var service = new LocalizationService(context);
            
            // Simulate character data with LocaleRef
            var characterNameRef = new LocaleRef { Key = "Character_Hero_Name" };
            var characterDescRef = new LocaleRef { Key = "Character_Hero_Desc" };
            
            // Act & Assert - English
            Assert.Equal("Hero", characterNameRef.Evaluate(service));
            Assert.Equal("A brave warrior", characterDescRef.Evaluate(service));
            
            // Act & Assert - Korean
            await service.SetLanguageAsync("Korean");
            Assert.Equal("용사", characterNameRef.Evaluate(service));
            Assert.Equal("용감한 전사", characterDescRef.Evaluate(service));
            
            // Act & Assert - Japanese
            await service.SetLanguageAsync("Japanese");
            Assert.Equal("勇者", characterNameRef.Evaluate(service));
            Assert.Equal("勇敢な戦士", characterDescRef.Evaluate(service));
        }
    }
}