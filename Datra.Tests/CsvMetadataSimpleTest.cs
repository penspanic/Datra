using System;
using System.Collections.Generic;
using Datra.SampleData.Models;
using Xunit;
using Xunit.Abstractions;

namespace Datra.Tests
{
    public class CsvMetadataSimpleTest
    {
        private readonly ITestOutputHelper _output;

        public CsvMetadataSimpleTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Should_Have_CsvMetadata_Property()
        {
            // Arrange
            var testData = new MetadataTestData();

            // Act & Assert
            Assert.NotNull(testData);

            // Check if CsvMetadata property exists
            var csvMetadataProperty = typeof(MetadataTestData).GetProperty("CsvMetadata");

            if (csvMetadataProperty == null)
            {
                _output.WriteLine("CsvMetadata property not found on MetadataTestData");
                Assert.True(false, "CsvMetadata property not found on MetadataTestData - Source Generator may not have run with updated code");
            }
            else
            {
                _output.WriteLine($"CsvMetadata property found: {csvMetadataProperty.PropertyType.Name}");

                // Check the type
                var expectedType = typeof(Dictionary<string, (int columnIndex, string value)>);
                Assert.Equal(expectedType, csvMetadataProperty.PropertyType);
            }
        }

        [Fact]
        public void Manual_CSV_Parse_Test()
        {
            // This test manually parses CSV to verify our logic
            var csvContent = @"Id,Name,~Comment,Value,~Note,Description
test_001,Item1,This is a comment,100,Important note,First test item";

            var lines = csvContent.Split('\n');
            var headers = lines[0].Split(',');

            var metadataColumns = new Dictionary<string, int>();
            var regularColumns = new Dictionary<string, int>();

            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].StartsWith("~"))
                {
                    metadataColumns[headers[i]] = i;
                    _output.WriteLine($"Found metadata column: {headers[i]} at index {i}");
                }
                else
                {
                    regularColumns[headers[i]] = i;
                }
            }

            Assert.Equal(2, metadataColumns.Count);
            Assert.True(metadataColumns.ContainsKey("~Comment"));
            Assert.True(metadataColumns.ContainsKey("~Note"));
            Assert.Equal(2, metadataColumns["~Comment"]);
            Assert.Equal(4, metadataColumns["~Note"]);
        }
    }
}