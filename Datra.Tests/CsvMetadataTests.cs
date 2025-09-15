using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datra.Configuration;
using Datra.Generated;
using Datra.SampleData.Models;
using Xunit;

namespace Datra.Tests
{
    public class CsvMetadataTests
    {
        private readonly GameDataContext _context;

        public CsvMetadataTests()
        {
            _context = TestDataHelper.CreateGameDataContext();
            _context.LoadAllAsync().Wait();
        }
        [Fact]
        public void Should_PreserveMetadataColumns_WhenReadingCsv()
        {
            // Act - Load MetadataTestData from CSV
            if (!_context.MetadataTest.TryGetValue("test_001", out var testData))
            {
                Assert.Fail("MetadataTestData not found");
                return;
            }

            // Assert
            Assert.NotNull(testData);
            Assert.NotNull(testData.CsvMetadata);

            // Now CsvMetadata contains ALL columns (regular + metadata)
            Assert.Equal(6, testData.CsvMetadata.Count); // Total columns: Id, Name, ~Comment, Value, ~Note, Description

            // Check metadata columns
            Assert.True(testData.CsvMetadata.ContainsKey("~Comment"));
            Assert.True(testData.CsvMetadata.ContainsKey("~Note"));
            Assert.Equal("This is a comment", testData.CsvMetadata["~Comment"].value);
            Assert.Equal("Important note", testData.CsvMetadata["~Note"].value);
            Assert.Equal(2, testData.CsvMetadata["~Comment"].columnIndex);
            Assert.Equal(4, testData.CsvMetadata["~Note"].columnIndex);

            // Check regular columns are tracked (value is null, comes from properties)
            Assert.True(testData.CsvMetadata.ContainsKey("Id"));
            Assert.True(testData.CsvMetadata.ContainsKey("Name"));
            Assert.True(testData.CsvMetadata.ContainsKey("Value"));
            Assert.True(testData.CsvMetadata.ContainsKey("Description"));
            Assert.Null(testData.CsvMetadata["Id"].value);
            Assert.Equal(0, testData.CsvMetadata["Id"].columnIndex);
        }

        [Fact]
        public void Should_RestoreMetadataColumns_WhenSerializingToCsv()
        {
            // Arrange - Get loaded data
            if (!_context.MetadataTest.TryGetValue("test_001", out var originalData))
            {
                Assert.Fail("MetadataTestData not found");
                return;
            }

            // Act - Serialize back to CSV
            var dataDict = new Dictionary<string, MetadataTestData>();
            foreach (var kvp in _context.MetadataTest)
            {
                dataDict[kvp.Key] = kvp.Value;
            }
            var csvContent = MetadataTestDataSerializer.SerializeCsv(dataDict);

            // Assert - Check that metadata columns are preserved
            var lines = csvContent.Split('\n');
            var headerLine = lines[0];

            // Check header contains metadata columns
            Assert.Contains("~Comment", headerLine);
            Assert.Contains("~Note", headerLine);

            // Check data line contains metadata values
            var dataLine = lines[1];
            Assert.Contains("This is a comment", dataLine);
            Assert.Contains("Important note", dataLine);
        }

        [Fact]
        public void Should_PreserveColumnIndex_WhenRoundTripping()
        {
            // Arrange - Get all test data
            var originalData = _context.MetadataTest;
            Assert.NotEmpty(originalData);

            // Check original data first - should have 6 total columns (4 regular + 2 metadata)
            foreach (var kvp in originalData)
            {
                var metadataKeys = string.Join(", ", kvp.Value.CsvMetadata.Keys.OrderBy(k => k));
                var expectedCount = 6; // Total columns in CSV: Id, Name, ~Comment, Value, ~Note, Description
                if (kvp.Value.CsvMetadata.Count != expectedCount)
                {
                    Assert.True(false, $"Original data {kvp.Key} should have {expectedCount} total columns in CsvMetadata, but has {kvp.Value.CsvMetadata.Count}: [{metadataKeys}]");
                }

                // Check that we have both metadata and regular columns
                var metadataColumns = kvp.Value.CsvMetadata.Keys.Where(k => k.StartsWith("~")).Count();
                var regularColumns = kvp.Value.CsvMetadata.Keys.Where(k => !k.StartsWith("~")).Count();
                Assert.Equal(2, metadataColumns); // ~Comment, ~Note
                Assert.Equal(4, regularColumns);  // Id, Name, Value, Description
            }

            // Act - Serialize and deserialize
            var dataDict = new Dictionary<string, MetadataTestData>();
            foreach (var kvp in originalData)
            {
                dataDict[kvp.Key] = kvp.Value;
            }
            var csvContent = MetadataTestDataSerializer.SerializeCsv(dataDict);

            // Debug: Check CSV content
            var lines = csvContent.Split('\n');
            Assert.True(lines.Length >= 2, $"CSV should have at least 2 lines, but has {lines.Length}");
            var headerColumns = lines[0].Split(',');
            var metadataColumnCount = headerColumns.Count(h => h.StartsWith("~"));
            Assert.True(metadataColumnCount == 2, $"CSV header should have 2 metadata columns, but has {metadataColumnCount}");

            var deserializedData = MetadataTestDataSerializer.DeserializeCsv(csvContent);

            // Assert - Check that metadata is preserved after round trip
            foreach (var kvp in originalData)
            {
                var original = kvp.Value;
                var deserialized = deserializedData[kvp.Key];

                // Debug: Print metadata info
                if (original.CsvMetadata.Count != deserialized.CsvMetadata.Count)
                {
                    var originalKeys = string.Join(", ", original.CsvMetadata.Keys);
                    var deserializedKeys = string.Join(", ", deserialized.CsvMetadata.Keys);
                    Assert.True(false, $"Metadata count mismatch for {kvp.Key}. Original: {original.CsvMetadata.Count} ({originalKeys}), Deserialized: {deserialized.CsvMetadata.Count} ({deserializedKeys})");
                }

                Assert.Equal(original.CsvMetadata.Count, deserialized.CsvMetadata.Count);

                foreach (var metaKvp in original.CsvMetadata)
                {
                    Assert.True(deserialized.CsvMetadata.ContainsKey(metaKvp.Key));
                    Assert.Equal(metaKvp.Value.columnIndex, deserialized.CsvMetadata[metaKvp.Key].columnIndex);
                    Assert.Equal(metaKvp.Value.value, deserialized.CsvMetadata[metaKvp.Key].value);
                }
            }
        }
    }
}