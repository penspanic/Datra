using System.Linq;
using Xunit;
using Datra.Localization;

namespace Datra.Tests
{
    public class LanguageInfoTests
    {
        #region LanguageInfo Struct Tests

        [Fact]
        public void LanguageInfo_Constructor_SetsAllProperties()
        {
            // Arrange & Act
            var info = new LanguageInfo(LanguageCode.Ko, "ko", "한국어", "Korean");

            // Assert
            Assert.Equal(LanguageCode.Ko, info.Code);
            Assert.Equal("ko", info.IsoCode);
            Assert.Equal("한국어", info.NativeName);
            Assert.Equal("Korean", info.EnglishName);
        }

        [Fact]
        public void LanguageInfo_ToString_ReturnsNativeNameWithIsoCode()
        {
            // Arrange
            var info = new LanguageInfo(LanguageCode.Ja, "ja", "日本語", "Japanese");

            // Act
            var result = info.ToString();

            // Assert
            Assert.Equal("日本語 (ja)", result);
        }

        #endregion

        #region GetLanguageInfo Tests

        [Theory]
        [InlineData(LanguageCode.En, "en", "English", "English")]
        [InlineData(LanguageCode.Ko, "ko", "한국어", "Korean")]
        [InlineData(LanguageCode.Ja, "ja", "日本語", "Japanese")]
        [InlineData(LanguageCode.ZhCN, "zh-CN", "简体中文", "Chinese (Simplified)")]
        [InlineData(LanguageCode.ZhTW, "zh-TW", "繁體中文", "Chinese (Traditional)")]
        [InlineData(LanguageCode.Es, "es", "Español", "Spanish")]
        [InlineData(LanguageCode.Fr, "fr", "Français", "French")]
        [InlineData(LanguageCode.De, "de", "Deutsch", "German")]
        [InlineData(LanguageCode.Ru, "ru", "Русский", "Russian")]
        [InlineData(LanguageCode.Ar, "ar", "العربية", "Arabic")]
        public void GetLanguageInfo_ReturnsCorrectInfo(
            LanguageCode code, string expectedIso, string expectedNative, string expectedEnglish)
        {
            // Act
            var info = code.GetLanguageInfo();

            // Assert
            Assert.Equal(code, info.Code);
            Assert.Equal(expectedIso, info.IsoCode);
            Assert.Equal(expectedNative, info.NativeName);
            Assert.Equal(expectedEnglish, info.EnglishName);
        }

        #endregion

        #region GetEnglishName Tests

        [Theory]
        [InlineData(LanguageCode.En, "English")]
        [InlineData(LanguageCode.Ko, "Korean")]
        [InlineData(LanguageCode.Ja, "Japanese")]
        [InlineData(LanguageCode.ZhCN, "Chinese (Simplified)")]
        [InlineData(LanguageCode.ZhTW, "Chinese (Traditional)")]
        [InlineData(LanguageCode.Es, "Spanish")]
        [InlineData(LanguageCode.Fr, "French")]
        [InlineData(LanguageCode.De, "German")]
        [InlineData(LanguageCode.It, "Italian")]
        [InlineData(LanguageCode.Pt, "Portuguese")]
        [InlineData(LanguageCode.Ru, "Russian")]
        [InlineData(LanguageCode.Ar, "Arabic")]
        [InlineData(LanguageCode.Hi, "Hindi")]
        [InlineData(LanguageCode.Th, "Thai")]
        [InlineData(LanguageCode.Vi, "Vietnamese")]
        public void GetEnglishName_ReturnsCorrectName(LanguageCode code, string expectedName)
        {
            // Act
            var result = code.GetEnglishName();

            // Assert
            Assert.Equal(expectedName, result);
        }

        #endregion

        #region GetDisplayName (Native Name) Tests

        [Theory]
        [InlineData(LanguageCode.En, "English")]
        [InlineData(LanguageCode.Ko, "한국어")]
        [InlineData(LanguageCode.Ja, "日本語")]
        [InlineData(LanguageCode.ZhCN, "简体中文")]
        [InlineData(LanguageCode.ZhTW, "繁體中文")]
        [InlineData(LanguageCode.Es, "Español")]
        [InlineData(LanguageCode.Fr, "Français")]
        [InlineData(LanguageCode.De, "Deutsch")]
        [InlineData(LanguageCode.Ru, "Русский")]
        [InlineData(LanguageCode.Ar, "العربية")]
        [InlineData(LanguageCode.Hi, "हिन्दी")]
        [InlineData(LanguageCode.Th, "ไทย")]
        [InlineData(LanguageCode.Vi, "Tiếng Việt")]
        public void GetDisplayName_ReturnsNativeName(LanguageCode code, string expectedName)
        {
            // Act
            var result = code.GetDisplayName();

            // Assert
            Assert.Equal(expectedName, result);
        }

        #endregion

        #region GetAllLanguages Tests

        [Fact]
        public void GetAllLanguages_ReturnsAllDefinedLanguages()
        {
            // Act
            var languages = LanguageCodeExtensions.GetAllLanguages().ToList();

            // Assert
            Assert.Equal(20, languages.Count);
            Assert.Contains(LanguageCode.En, languages);
            Assert.Contains(LanguageCode.Ko, languages);
            Assert.Contains(LanguageCode.Ja, languages);
            Assert.Contains(LanguageCode.ZhCN, languages);
            Assert.Contains(LanguageCode.ZhTW, languages);
            Assert.Contains(LanguageCode.Es, languages);
            Assert.Contains(LanguageCode.Fr, languages);
            Assert.Contains(LanguageCode.De, languages);
        }

        [Fact]
        public void GetAllLanguages_ReturnsDistinctValues()
        {
            // Act
            var languages = LanguageCodeExtensions.GetAllLanguages().ToList();
            var distinctLanguages = languages.Distinct().ToList();

            // Assert
            Assert.Equal(languages.Count, distinctLanguages.Count);
        }

        #endregion

        #region GetAllLanguageInfos Tests

        [Fact]
        public void GetAllLanguageInfos_ReturnsInfoForAllLanguages()
        {
            // Act
            var languageInfos = LanguageCodeExtensions.GetAllLanguageInfos().ToList();

            // Assert
            Assert.Equal(20, languageInfos.Count);

            // Verify each info has valid data
            foreach (var info in languageInfos)
            {
                Assert.False(string.IsNullOrEmpty(info.IsoCode));
                Assert.False(string.IsNullOrEmpty(info.NativeName));
                Assert.False(string.IsNullOrEmpty(info.EnglishName));
            }
        }

        [Fact]
        public void GetAllLanguageInfos_ContainsExpectedLanguages()
        {
            // Act
            var languageInfos = LanguageCodeExtensions.GetAllLanguageInfos().ToList();

            // Assert - Check specific languages exist with correct data
            var korean = languageInfos.FirstOrDefault(l => l.Code == LanguageCode.Ko);
            Assert.Equal("ko", korean.IsoCode);
            Assert.Equal("한국어", korean.NativeName);
            Assert.Equal("Korean", korean.EnglishName);

            var japanese = languageInfos.FirstOrDefault(l => l.Code == LanguageCode.Ja);
            Assert.Equal("ja", japanese.IsoCode);
            Assert.Equal("日本語", japanese.NativeName);
            Assert.Equal("Japanese", japanese.EnglishName);
        }

        #endregion

        #region ToIsoCode Tests

        [Theory]
        [InlineData(LanguageCode.En, "en")]
        [InlineData(LanguageCode.Ko, "ko")]
        [InlineData(LanguageCode.Ja, "ja")]
        [InlineData(LanguageCode.ZhCN, "zh-CN")]
        [InlineData(LanguageCode.ZhTW, "zh-TW")]
        public void ToIsoCode_ReturnsCorrectCode(LanguageCode code, string expectedIso)
        {
            // Act
            var result = code.ToIsoCode();

            // Assert
            Assert.Equal(expectedIso, result);
        }

        #endregion

        #region FromIsoCode Tests

        [Theory]
        [InlineData("en", LanguageCode.En)]
        [InlineData("ko", LanguageCode.Ko)]
        [InlineData("ja", LanguageCode.Ja)]
        [InlineData("zh-CN", LanguageCode.ZhCN)]
        [InlineData("zh-TW", LanguageCode.ZhTW)]
        [InlineData("EN", LanguageCode.En)] // Case insensitive
        [InlineData("Ko", LanguageCode.Ko)] // Case insensitive
        public void FromIsoCode_ReturnsCorrectLanguageCode(string isoCode, LanguageCode expected)
        {
            // Act
            var result = LanguageCodeExtensions.FromIsoCode(isoCode);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expected, result.Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("invalid")]
        [InlineData("xx")]
        public void FromIsoCode_InvalidCode_ReturnsNull(string? isoCode)
        {
            // Act
            var result = LanguageCodeExtensions.FromIsoCode(isoCode!);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region TryParse Tests

        [Theory]
        [InlineData("En", LanguageCode.En)]
        [InlineData("en", LanguageCode.En)]
        [InlineData("Ko", LanguageCode.Ko)]
        [InlineData("ko", LanguageCode.Ko)]
        [InlineData("ZhCN", LanguageCode.ZhCN)]
        [InlineData("zh-CN", LanguageCode.ZhCN)]
        public void TryParse_ValidInput_ReturnsLanguageCode(string input, LanguageCode expected)
        {
            // Act
            var result = LanguageCodeExtensions.TryParse(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expected, result.Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("invalid")]
        public void TryParse_InvalidInput_ReturnsNull(string? input)
        {
            // Act
            var result = LanguageCodeExtensions.TryParse(input!);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetFileName Tests

        [Theory]
        [InlineData(LanguageCode.En, ".csv", "en.csv")]
        [InlineData(LanguageCode.Ko, ".csv", "ko.csv")]
        [InlineData(LanguageCode.ZhCN, ".csv", "zh-CN.csv")]
        [InlineData(LanguageCode.En, ".json", "en.json")]
        [InlineData(LanguageCode.Ko, ".yaml", "ko.yaml")]
        public void GetFileName_ReturnsCorrectFileName(LanguageCode code, string extension, string expected)
        {
            // Act
            var result = code.GetFileName(extension);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetFileName_DefaultExtension_ReturnsCsv()
        {
            // Act
            var result = LanguageCode.En.GetFileName();

            // Assert
            Assert.Equal("en.csv", result);
        }

        #endregion
    }
}
