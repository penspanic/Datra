using Xunit;
using Datra.Generators.Builders;

namespace Datra.Tests
{
    public class CodeBuilderTests
    {
        #region IsCsvFormat Tests

        [Theory]
        [InlineData("Csv", "data.csv", true)]
        [InlineData("Csv", "data.json", true)]  // Explicit Csv format overrides extension
        [InlineData("Csv", "", true)]
        [InlineData("Json", "data.csv", false)]
        [InlineData("Json", "data.json", false)]
        [InlineData("Yaml", "data.yaml", false)]
        public void IsCsvFormat_ExplicitFormat_ReturnsCorrectly(string format, string filePath, bool expected)
        {
            var result = CodeBuilder.IsCsvFormat(format, filePath);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Auto", "data.csv", true)]
        [InlineData("Auto", "Data/Monster.csv", true)]
        [InlineData("Auto", "Epos/Monster.CSV", true)]  // Case insensitive
        [InlineData("Auto", "path/to/file.Csv", true)]
        [InlineData("Auto", "data.json", false)]
        [InlineData("Auto", "data.yaml", false)]
        [InlineData("Auto", "", false)]
        [InlineData("Auto", null, false)]
        public void IsCsvFormat_AutoFormat_DetectsFromFileExtension(string format, string filePath, bool expected)
        {
            var result = CodeBuilder.IsCsvFormat(format, filePath);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Datra.Attributes.DataFormat.Csv", "anything", true)]
        [InlineData("Datra.Attributes.DataFormat.Auto", "file.csv", true)]
        [InlineData("Datra.Attributes.DataFormat.Auto", "file.json", false)]
        [InlineData("Datra.Attributes.DataFormat.Json", "file.csv", false)]
        public void IsCsvFormat_FullyQualifiedFormat_HandlesCorrectly(string format, string filePath, bool expected)
        {
            var result = CodeBuilder.IsCsvFormat(format, filePath);
            Assert.Equal(expected, result);
        }

        #endregion

        #region GetDataFormat Tests

        [Theory]
        [InlineData("Csv", "Csv")]
        [InlineData("Json", "Json")]
        [InlineData("Auto", "Auto")]
        [InlineData("Datra.Attributes.DataFormat.Csv", "Csv")]
        [InlineData("Datra.Attributes.DataFormat.Json", "Json")]
        [InlineData("Datra.Attributes.DataFormat.Auto", "Auto")]
        public void GetDataFormat_ReturnsSimpleFormat(string input, string expected)
        {
            var result = CodeBuilder.GetDataFormat(input);
            Assert.Equal(expected, result);
        }

        #endregion

        #region GetSimpleTypeName Tests

        [Theory]
        [InlineData("global::MyNamespace.MyClass", "MyClass")]
        [InlineData("MyNamespace.MyClass", "MyClass")]
        [InlineData("MyClass", "MyClass")]
        [InlineData("global::A.B.C.MyClass", "MyClass")]
        public void GetSimpleTypeName_ExtractsClassName(string fullName, string expected)
        {
            var result = CodeBuilder.GetSimpleTypeName(fullName);
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
