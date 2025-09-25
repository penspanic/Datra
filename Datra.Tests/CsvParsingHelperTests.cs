using System;
using Datra.Helpers;
using Xunit;

namespace Datra.Tests
{
    public class CsvParsingHelperTests
    {
        [Fact]
        public void ParseCsvLine_SimpleFields_ShouldParse()
        {
            // Arrange
            string line = "1,test,value,data";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line);

            // Assert
            Assert.Equal(4, result.Length);
            Assert.Equal("1", result[0]);
            Assert.Equal("test", result[1]);
            Assert.Equal("value", result[2]);
            Assert.Equal("data", result[3]);
        }

        [Fact]
        public void ParseCsvLine_QuotedFieldWithComma_ShouldParse()
        {
            // Arrange
            string line = "1,\"test,with,commas\",value";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line);

            // Assert
            Assert.Equal(3, result.Length);
            Assert.Equal("1", result[0]);
            Assert.Equal("test,with,commas", result[1]);
            Assert.Equal("value", result[2]);
        }

        [Fact]
        public void ParseCsvLine_QuotedFieldWithEscapedQuotes_ShouldParse()
        {
            // Arrange
            string line = "1,\"test \"\"quoted\"\" text\",value";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line);

            // Assert
            Assert.Equal(3, result.Length);
            Assert.Equal("1", result[0]);
            Assert.Equal("test \"quoted\" text", result[1]);
            Assert.Equal("value", result[2]);
        }

        [Fact]
        public void ParseCsvLine_ComplexKoreanData_ShouldParse()
        {
            // Arrange - actual data from the user's example
            // Testing with Korean text that may contain special characters
            string line = "3,Dialogue_1_3,,1,1,1,3,Name_Character_MrLee,미스터리,Desc_Dialogue_1_3,죄인아이언리드사칭범은,0,H_Dialogue_Illust_022,,,0,MrLee,story_illust_in_M,0,,,0";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line);

            // Assert
            Assert.Equal(22, result.Length);
            Assert.Equal("3", result[0]);
            Assert.Equal("Dialogue_1_3", result[1]);
            Assert.Equal("", result[2]);
            Assert.Equal("1", result[3]);
            Assert.Equal("죄인아이언리드사칭범은", result[10]); // Korean text
            Assert.Equal("0", result[11]);
        }

        [Fact]
        public void ParseCsvLine_KoreanTextWithCommaInQuotes_ShouldParse()
        {
            // Arrange - testing Korean text with embedded comma in quoted field
            // This tests the actual problematic pattern from the user's CSV
            string line = "21,Dialogue_2_5,,1,2,2,5,,,\"Desc_Dialogue_2_5,-탕!-\",1,H_Dialogue_Illust_035,,,0,MrLee,,0,,,0";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line);

            // Assert
            Assert.Equal(21, result.Length);
            Assert.Equal("21", result[0]);
            Assert.Equal("Dialogue_2_5", result[1]);
            Assert.Equal("Desc_Dialogue_2_5,-탕!-", result[9]); // Should preserve the comma inside quotes
        }

        [Fact]
        public void ParseCsvLine_EmptyFields_ShouldParse()
        {
            // Arrange
            string line = "1,,,,value";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line);

            // Assert
            Assert.Equal(5, result.Length);
            Assert.Equal("1", result[0]);
            Assert.Equal("", result[1]);
            Assert.Equal("", result[2]);
            Assert.Equal("", result[3]);
            Assert.Equal("value", result[4]);
        }

        [Fact]
        public void ParseCsvLine_MixedQuotedAndUnquoted_ShouldParse()
        {
            // Arrange
            string line = "1,normal,\"quoted,field\",another,\"also \"\"quoted\"\"\",end";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line);

            // Assert
            Assert.Equal(6, result.Length);
            Assert.Equal("1", result[0]);
            Assert.Equal("normal", result[1]);
            Assert.Equal("quoted,field", result[2]);
            Assert.Equal("another", result[3]);
            Assert.Equal("also \"quoted\"", result[4]);
            Assert.Equal("end", result[5]);
        }

        [Fact]
        public void ParseCsvLine_OnlyQuotedField_ShouldParse()
        {
            // Arrange
            string line = "\"single quoted field with, comma\"";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line);

            // Assert
            Assert.Single(result);
            Assert.Equal("single quoted field with, comma", result[0]);
        }

        [Fact]
        public void ParseCsvLine_ConsecutiveQuotedFields_ShouldParse()
        {
            // Arrange
            string line = "\"field1\",\"field2\",\"field3\"";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line);

            // Assert
            Assert.Equal(3, result.Length);
            Assert.Equal("field1", result[0]);
            Assert.Equal("field2", result[1]);
            Assert.Equal("field3", result[2]);
        }

        [Fact]
        public void ParseCsvLine_NewlineInQuotedField_ShouldParse()
        {
            // Arrange
            string line = "1,\"multi\nline\ntext\",3";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line);

            // Assert
            Assert.Equal(3, result.Length);
            Assert.Equal("1", result[0]);
            Assert.Equal("multi\nline\ntext", result[1]);
            Assert.Equal("3", result[2]);
        }

        [Fact]
        public void ParseCsvLine_CustomDelimiter_ShouldParse()
        {
            // Arrange
            string line = "1|test|value";

            // Act
            var result = CsvParsingHelper.ParseCsvLine(line, '|');

            // Assert
            Assert.Equal(3, result.Length);
            Assert.Equal("1", result[0]);
            Assert.Equal("test", result[1]);
            Assert.Equal("value", result[2]);
        }

        [Fact]
        public void EscapeCsvField_SimpleField_ShouldNotQuote()
        {
            // Act
            var result = CsvParsingHelper.EscapeCsvField("simple");

            // Assert
            Assert.Equal("simple", result);
        }

        [Fact]
        public void EscapeCsvField_FieldWithComma_ShouldQuote()
        {
            // Act
            var result = CsvParsingHelper.EscapeCsvField("field,with,comma");

            // Assert
            Assert.Equal("\"field,with,comma\"", result);
        }

        [Fact]
        public void EscapeCsvField_FieldWithQuotes_ShouldEscapeAndQuote()
        {
            // Act
            var result = CsvParsingHelper.EscapeCsvField("field with \"quotes\"");

            // Assert
            Assert.Equal("\"field with \"\"quotes\"\"\"", result);
        }

        [Fact]
        public void EscapeCsvField_FieldWithNewline_ShouldQuote()
        {
            // Act
            var result = CsvParsingHelper.EscapeCsvField("field\nwith\nnewlines");

            // Assert
            Assert.Equal("\"field\nwith\nnewlines\"", result);
        }

        [Fact]
        public void JoinCsvFields_SimpleFields_ShouldJoin()
        {
            // Arrange
            var fields = new[] { "1", "test", "value" };

            // Act
            var result = CsvParsingHelper.JoinCsvFields(fields);

            // Assert
            Assert.Equal("1,test,value", result);
        }

        [Fact]
        public void JoinCsvFields_FieldsWithSpecialChars_ShouldEscapeAndJoin()
        {
            // Arrange
            var fields = new[] { "1", "field,with,comma", "field with \"quotes\"", "normal" };

            // Act
            var result = CsvParsingHelper.JoinCsvFields(fields);

            // Assert
            Assert.Equal("1,\"field,with,comma\",\"field with \"\"quotes\"\"\",normal", result);
        }

        [Fact]
        public void ParseAndJoin_RoundTrip_ShouldBeEqual()
        {
            // Arrange
            var original = "1,\"field,with,comma\",\"field with \"\"quotes\"\"\",normal,\"multi\nline\"";

            // Act
            var parsed = CsvParsingHelper.ParseCsvLine(original);
            var rejoined = CsvParsingHelper.JoinCsvFields(parsed);

            // Assert
            // Parse again to verify they produce the same result
            var reparsed = CsvParsingHelper.ParseCsvLine(rejoined);
            Assert.Equal(parsed.Length, reparsed.Length);
            for (int i = 0; i < parsed.Length; i++)
            {
                Assert.Equal(parsed[i], reparsed[i]);
            }
        }

        [Fact]
        public void ParseCsvLine_EmptyString_ShouldReturnEmptyArray()
        {
            // Act
            var result = CsvParsingHelper.ParseCsvLine("");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ParseCsvLine_Null_ShouldReturnEmptyArray()
        {
            // Act
            var result = CsvParsingHelper.ParseCsvLine(null);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void JoinCsvFields_EmptyArray_ShouldReturnEmptyString()
        {
            // Act
            var result = CsvParsingHelper.JoinCsvFields(new string[0]);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void JoinCsvFields_Null_ShouldReturnEmptyString()
        {
            // Act
            var result = CsvParsingHelper.JoinCsvFields(null);

            // Assert
            Assert.Equal("", result);
        }
    }
}