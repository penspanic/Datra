using System.Linq;
using Datra.Localization;
using NUnit.Framework;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Tests for LanguageInfo struct and LanguageCodeExtensions.
    /// Verifies language metadata functionality works correctly in Unity.
    /// </summary>
    public class LanguageInfoTests
    {
        #region LanguageInfo Struct Tests

        [Test]
        public void LanguageInfo_Constructor_SetsAllProperties()
        {
            // Arrange & Act
            var info = new LanguageInfo(LanguageCode.Ko, "ko", "한국어", "Korean");

            // Assert
            Assert.AreEqual(LanguageCode.Ko, info.Code);
            Assert.AreEqual("ko", info.IsoCode);
            Assert.AreEqual("한국어", info.NativeName);
            Assert.AreEqual("Korean", info.EnglishName);
        }

        [Test]
        public void LanguageInfo_ToString_ReturnsNativeNameWithIsoCode()
        {
            // Arrange
            var info = new LanguageInfo(LanguageCode.Ja, "ja", "日本語", "Japanese");

            // Act
            var result = info.ToString();

            // Assert
            Assert.AreEqual("日本語 (ja)", result);
        }

        #endregion

        #region GetLanguageInfo Tests

        [Test]
        public void GetLanguageInfo_English_ReturnsCorrectInfo()
        {
            var info = LanguageCode.En.GetLanguageInfo();
            Assert.AreEqual(LanguageCode.En, info.Code);
            Assert.AreEqual("en", info.IsoCode);
            Assert.AreEqual("English", info.NativeName);
            Assert.AreEqual("English", info.EnglishName);
        }

        [Test]
        public void GetLanguageInfo_Korean_ReturnsCorrectInfo()
        {
            var info = LanguageCode.Ko.GetLanguageInfo();
            Assert.AreEqual(LanguageCode.Ko, info.Code);
            Assert.AreEqual("ko", info.IsoCode);
            Assert.AreEqual("한국어", info.NativeName);
            Assert.AreEqual("Korean", info.EnglishName);
        }

        [Test]
        public void GetLanguageInfo_Japanese_ReturnsCorrectInfo()
        {
            var info = LanguageCode.Ja.GetLanguageInfo();
            Assert.AreEqual(LanguageCode.Ja, info.Code);
            Assert.AreEqual("ja", info.IsoCode);
            Assert.AreEqual("日本語", info.NativeName);
            Assert.AreEqual("Japanese", info.EnglishName);
        }

        [Test]
        public void GetLanguageInfo_ChineseSimplified_ReturnsCorrectInfo()
        {
            var info = LanguageCode.ZhCN.GetLanguageInfo();
            Assert.AreEqual(LanguageCode.ZhCN, info.Code);
            Assert.AreEqual("zh-CN", info.IsoCode);
            Assert.AreEqual("简体中文", info.NativeName);
            Assert.AreEqual("Chinese (Simplified)", info.EnglishName);
        }

        [Test]
        public void GetLanguageInfo_ChineseTraditional_ReturnsCorrectInfo()
        {
            var info = LanguageCode.ZhTW.GetLanguageInfo();
            Assert.AreEqual(LanguageCode.ZhTW, info.Code);
            Assert.AreEqual("zh-TW", info.IsoCode);
            Assert.AreEqual("繁體中文", info.NativeName);
            Assert.AreEqual("Chinese (Traditional)", info.EnglishName);
        }

        #endregion

        #region GetEnglishName Tests

        [Test]
        public void GetEnglishName_ReturnsCorrectNames()
        {
            Assert.AreEqual("English", LanguageCode.En.GetEnglishName());
            Assert.AreEqual("Korean", LanguageCode.Ko.GetEnglishName());
            Assert.AreEqual("Japanese", LanguageCode.Ja.GetEnglishName());
            Assert.AreEqual("Spanish", LanguageCode.Es.GetEnglishName());
            Assert.AreEqual("French", LanguageCode.Fr.GetEnglishName());
            Assert.AreEqual("German", LanguageCode.De.GetEnglishName());
            Assert.AreEqual("Russian", LanguageCode.Ru.GetEnglishName());
            Assert.AreEqual("Arabic", LanguageCode.Ar.GetEnglishName());
        }

        #endregion

        #region GetDisplayName (Native Name) Tests

        [Test]
        public void GetDisplayName_ReturnsNativeNames()
        {
            Assert.AreEqual("English", LanguageCode.En.GetDisplayName());
            Assert.AreEqual("한국어", LanguageCode.Ko.GetDisplayName());
            Assert.AreEqual("日本語", LanguageCode.Ja.GetDisplayName());
            Assert.AreEqual("Español", LanguageCode.Es.GetDisplayName());
            Assert.AreEqual("Français", LanguageCode.Fr.GetDisplayName());
            Assert.AreEqual("Deutsch", LanguageCode.De.GetDisplayName());
            Assert.AreEqual("Русский", LanguageCode.Ru.GetDisplayName());
            Assert.AreEqual("العربية", LanguageCode.Ar.GetDisplayName());
        }

        #endregion

        #region GetAllLanguages Tests

        [Test]
        public void GetAllLanguages_Returns20Languages()
        {
            var languages = LanguageCodeExtensions.GetAllLanguages().ToList();
            Assert.AreEqual(20, languages.Count);
        }

        [Test]
        public void GetAllLanguages_ContainsExpectedLanguages()
        {
            var languages = LanguageCodeExtensions.GetAllLanguages().ToList();

            Assert.Contains(LanguageCode.En, languages);
            Assert.Contains(LanguageCode.Ko, languages);
            Assert.Contains(LanguageCode.Ja, languages);
            Assert.Contains(LanguageCode.ZhCN, languages);
            Assert.Contains(LanguageCode.ZhTW, languages);
            Assert.Contains(LanguageCode.Es, languages);
            Assert.Contains(LanguageCode.Fr, languages);
            Assert.Contains(LanguageCode.De, languages);
        }

        [Test]
        public void GetAllLanguages_ReturnsDistinctValues()
        {
            var languages = LanguageCodeExtensions.GetAllLanguages().ToList();
            var distinctLanguages = languages.Distinct().ToList();

            Assert.AreEqual(languages.Count, distinctLanguages.Count);
        }

        #endregion

        #region GetAllLanguageInfos Tests

        [Test]
        public void GetAllLanguageInfos_Returns20LanguageInfos()
        {
            var languageInfos = LanguageCodeExtensions.GetAllLanguageInfos().ToList();
            Assert.AreEqual(20, languageInfos.Count);
        }

        [Test]
        public void GetAllLanguageInfos_AllHaveValidData()
        {
            var languageInfos = LanguageCodeExtensions.GetAllLanguageInfos().ToList();

            foreach (var info in languageInfos)
            {
                Assert.IsFalse(string.IsNullOrEmpty(info.IsoCode), $"IsoCode should not be empty for {info.Code}");
                Assert.IsFalse(string.IsNullOrEmpty(info.NativeName), $"NativeName should not be empty for {info.Code}");
                Assert.IsFalse(string.IsNullOrEmpty(info.EnglishName), $"EnglishName should not be empty for {info.Code}");
            }
        }

        [Test]
        public void GetAllLanguageInfos_CanFindKorean()
        {
            var languageInfos = LanguageCodeExtensions.GetAllLanguageInfos().ToList();
            var korean = languageInfos.FirstOrDefault(l => l.Code == LanguageCode.Ko);

            Assert.AreEqual("ko", korean.IsoCode);
            Assert.AreEqual("한국어", korean.NativeName);
            Assert.AreEqual("Korean", korean.EnglishName);
        }

        #endregion

        #region ToIsoCode Tests

        [Test]
        public void ToIsoCode_ReturnsCorrectCodes()
        {
            Assert.AreEqual("en", LanguageCode.En.ToIsoCode());
            Assert.AreEqual("ko", LanguageCode.Ko.ToIsoCode());
            Assert.AreEqual("ja", LanguageCode.Ja.ToIsoCode());
            Assert.AreEqual("zh-CN", LanguageCode.ZhCN.ToIsoCode());
            Assert.AreEqual("zh-TW", LanguageCode.ZhTW.ToIsoCode());
        }

        #endregion

        #region FromIsoCode Tests

        [Test]
        public void FromIsoCode_ValidCodes_ReturnsLanguageCode()
        {
            Assert.AreEqual(LanguageCode.En, LanguageCodeExtensions.FromIsoCode("en"));
            Assert.AreEqual(LanguageCode.Ko, LanguageCodeExtensions.FromIsoCode("ko"));
            Assert.AreEqual(LanguageCode.Ja, LanguageCodeExtensions.FromIsoCode("ja"));
            Assert.AreEqual(LanguageCode.ZhCN, LanguageCodeExtensions.FromIsoCode("zh-CN"));
            Assert.AreEqual(LanguageCode.ZhTW, LanguageCodeExtensions.FromIsoCode("zh-TW"));
        }

        [Test]
        public void FromIsoCode_CaseInsensitive()
        {
            Assert.AreEqual(LanguageCode.En, LanguageCodeExtensions.FromIsoCode("EN"));
            Assert.AreEqual(LanguageCode.Ko, LanguageCodeExtensions.FromIsoCode("Ko"));
            Assert.AreEqual(LanguageCode.Ja, LanguageCodeExtensions.FromIsoCode("JA"));
        }

        [Test]
        public void FromIsoCode_InvalidCode_ReturnsNull()
        {
            Assert.IsNull(LanguageCodeExtensions.FromIsoCode(""));
            Assert.IsNull(LanguageCodeExtensions.FromIsoCode(null));
            Assert.IsNull(LanguageCodeExtensions.FromIsoCode("invalid"));
            Assert.IsNull(LanguageCodeExtensions.FromIsoCode("xx"));
        }

        #endregion

        #region TryParse Tests

        [Test]
        public void TryParse_EnumName_ReturnsLanguageCode()
        {
            Assert.AreEqual(LanguageCode.En, LanguageCodeExtensions.TryParse("En"));
            Assert.AreEqual(LanguageCode.Ko, LanguageCodeExtensions.TryParse("Ko"));
            Assert.AreEqual(LanguageCode.ZhCN, LanguageCodeExtensions.TryParse("ZhCN"));
        }

        [Test]
        public void TryParse_IsoCode_ReturnsLanguageCode()
        {
            Assert.AreEqual(LanguageCode.En, LanguageCodeExtensions.TryParse("en"));
            Assert.AreEqual(LanguageCode.Ko, LanguageCodeExtensions.TryParse("ko"));
            Assert.AreEqual(LanguageCode.ZhCN, LanguageCodeExtensions.TryParse("zh-CN"));
        }

        [Test]
        public void TryParse_InvalidInput_ReturnsNull()
        {
            Assert.IsNull(LanguageCodeExtensions.TryParse(""));
            Assert.IsNull(LanguageCodeExtensions.TryParse(null));
            Assert.IsNull(LanguageCodeExtensions.TryParse("   "));
            Assert.IsNull(LanguageCodeExtensions.TryParse("invalid"));
        }

        #endregion

        #region GetFileName Tests

        [Test]
        public void GetFileName_DefaultExtension_ReturnsCsv()
        {
            Assert.AreEqual("en.csv", LanguageCode.En.GetFileName());
            Assert.AreEqual("ko.csv", LanguageCode.Ko.GetFileName());
            Assert.AreEqual("zh-CN.csv", LanguageCode.ZhCN.GetFileName());
        }

        [Test]
        public void GetFileName_CustomExtension_ReturnsCorrectFileName()
        {
            Assert.AreEqual("en.json", LanguageCode.En.GetFileName(".json"));
            Assert.AreEqual("ko.yaml", LanguageCode.Ko.GetFileName(".yaml"));
        }

        #endregion
    }
}
