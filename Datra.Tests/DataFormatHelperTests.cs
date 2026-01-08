using System;
using Datra.Attributes;
using Datra.Utilities;
using Xunit;

namespace Datra.Tests
{
    public class DataFormatHelperTests
    {
        #region DetectFormat Tests

        [Theory]
        [InlineData("data.json", DataFormat.Json)]
        [InlineData("data.JSON", DataFormat.Json)]
        [InlineData("path/to/data.json", DataFormat.Json)]
        [InlineData("data.yaml", DataFormat.Yaml)]
        [InlineData("data.YAML", DataFormat.Yaml)]
        [InlineData("data.yml", DataFormat.Yaml)]
        [InlineData("data.YML", DataFormat.Yaml)]
        [InlineData("data.csv", DataFormat.Csv)]
        [InlineData("data.CSV", DataFormat.Csv)]
        public void DetectFormat_ValidExtension_ReturnsCorrectFormat(string filePath, DataFormat expected)
        {
            var result = DataFormatHelper.DetectFormat(filePath);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("data.txt")]
        [InlineData("data.xml")]
        [InlineData("data")]
        [InlineData("")]
        public void DetectFormat_UnsupportedExtension_ThrowsNotSupportedException(string filePath)
        {
            Assert.Throws<NotSupportedException>(() => DataFormatHelper.DetectFormat(filePath));
        }

        #endregion

        #region TryDetectFormat Tests

        [Theory]
        [InlineData("data.json", true, DataFormat.Json)]
        [InlineData("data.yaml", true, DataFormat.Yaml)]
        [InlineData("data.yml", true, DataFormat.Yaml)]
        [InlineData("data.csv", true, DataFormat.Csv)]
        [InlineData("data.txt", false, DataFormat.Auto)]
        [InlineData("data", false, DataFormat.Auto)]
        public void TryDetectFormat_ReturnsExpectedResult(string filePath, bool expectedSuccess, DataFormat expectedFormat)
        {
            var success = DataFormatHelper.TryDetectFormat(filePath, out var format);
            Assert.Equal(expectedSuccess, success);
            Assert.Equal(expectedFormat, format);
        }

        #endregion

        #region GetExtensionFromPattern Tests

        [Theory]
        [InlineData("*.json", ".json")]
        [InlineData("*.yaml", ".yaml")]
        [InlineData("*.yml", ".yml")]
        [InlineData("*.csv", ".csv")]
        [InlineData("*.JSON", ".json")]
        [InlineData("data*.json", ".json")]
        [InlineData("prefix.*.json", ".json")]
        public void GetExtensionFromPattern_ValidPattern_ReturnsExtension(string pattern, string expected)
        {
            var result = DataFormatHelper.GetExtensionFromPattern(pattern);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, ".json")]
        [InlineData("", ".json")]
        [InlineData("noextension", ".json")]
        public void GetExtensionFromPattern_InvalidPattern_ReturnsDefaultJson(string? pattern, string expected)
        {
            var result = DataFormatHelper.GetExtensionFromPattern(pattern);
            Assert.Equal(expected, result);
        }

        #endregion

        #region IsXxxExtension Tests

        [Theory]
        [InlineData(".json", true)]
        [InlineData(".JSON", true)]
        [InlineData(".yaml", false)]
        [InlineData(".csv", false)]
        [InlineData(null, false)]
        [InlineData("", false)]
        public void IsJsonExtension_ReturnsExpectedResult(string? extension, bool expected)
        {
            Assert.Equal(expected, DataFormatHelper.IsJsonExtension(extension));
        }

        [Theory]
        [InlineData(".yaml", true)]
        [InlineData(".YAML", true)]
        [InlineData(".yml", true)]
        [InlineData(".YML", true)]
        [InlineData(".json", false)]
        [InlineData(".csv", false)]
        [InlineData(null, false)]
        public void IsYamlExtension_ReturnsExpectedResult(string? extension, bool expected)
        {
            Assert.Equal(expected, DataFormatHelper.IsYamlExtension(extension));
        }

        [Theory]
        [InlineData(".csv", true)]
        [InlineData(".CSV", true)]
        [InlineData(".json", false)]
        [InlineData(".yaml", false)]
        [InlineData(null, false)]
        public void IsCsvExtension_ReturnsExpectedResult(string? extension, bool expected)
        {
            Assert.Equal(expected, DataFormatHelper.IsCsvExtension(extension));
        }

        #endregion

        #region IsXxxFile Tests

        [Theory]
        [InlineData("data.json", true)]
        [InlineData("path/to/data.JSON", true)]
        [InlineData("data.yaml", false)]
        [InlineData(null, false)]
        public void IsJsonFile_ReturnsExpectedResult(string? filePath, bool expected)
        {
            Assert.Equal(expected, DataFormatHelper.IsJsonFile(filePath));
        }

        [Theory]
        [InlineData("data.yaml", true)]
        [InlineData("data.yml", true)]
        [InlineData("path/to/data.YAML", true)]
        [InlineData("data.json", false)]
        [InlineData(null, false)]
        public void IsYamlFile_ReturnsExpectedResult(string? filePath, bool expected)
        {
            Assert.Equal(expected, DataFormatHelper.IsYamlFile(filePath));
        }

        [Theory]
        [InlineData("data.csv", true)]
        [InlineData("path/to/data.CSV", true)]
        [InlineData("data.json", false)]
        [InlineData(null, false)]
        public void IsCsvFile_ReturnsExpectedResult(string? filePath, bool expected)
        {
            Assert.Equal(expected, DataFormatHelper.IsCsvFile(filePath));
        }

        #endregion
    }
}
