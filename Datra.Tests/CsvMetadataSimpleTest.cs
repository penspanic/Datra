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

                // Check the type - now includes columnName
                var expectedType = typeof(Dictionary<string, (int columnIndex, string columnName, string value)>);
                Assert.Equal(expectedType, csvMetadataProperty.PropertyType);
            }
        }

        [Fact]
        public void Manual_CSV_Parse_Test()
        {
            // This test manually parses CSV to verify our logic
            var csvContent = @"Id,~,Name,~Comment,~,Value,~Note,~,Description,~
test_001,memo1,Item1,This is a comment,memo2,100,Important note,memo3,First test item,memo4";

            var lines = csvContent.Split('\n');
            var headers = lines[0].Split(',');

            var metadataColumns = new List<(string name, int index)>();
            var regularColumns = new Dictionary<string, int>();

            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].StartsWith("~"))
                {
                    metadataColumns.Add((headers[i], i));
                    _output.WriteLine($"Found metadata column: {headers[i]} at index {i}");
                }
                else
                {
                    regularColumns[headers[i]] = i;
                }
            }

            // Now we have 6 metadata columns: ~Comment, ~Note, and 4 '~' columns
            Assert.Equal(6, metadataColumns.Count);
            Assert.Contains(metadataColumns, m => m.name == "~Comment" && m.index == 3);
            Assert.Contains(metadataColumns, m => m.name == "~Note" && m.index == 6);

            // Check that '~' columns exist at correct positions
            Assert.Contains(metadataColumns, m => m.name == "~" && m.index == 1);
            Assert.Contains(metadataColumns, m => m.name == "~" && m.index == 4);
            Assert.Contains(metadataColumns, m => m.name == "~" && m.index == 7);
            Assert.Contains(metadataColumns, m => m.name == "~" && m.index == 9);

            // Count how many '~' columns
            var tildeCount = headers.Count(h => h == "~");
            Assert.Equal(4, tildeCount);
        }
    }
}