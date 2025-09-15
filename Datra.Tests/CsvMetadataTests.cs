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
            Assert.Equal(10, testData.CsvMetadata.Count); // Total columns: Id, ~, Name, ~Comment, ~, Value, ~Note, ~, Description, ~

            // Debug: Print all keys
            var allKeys = string.Join(", ", testData.CsvMetadata.Keys);

            // Check metadata columns - they might have index suffixes if duplicated
            var hasComment = testData.CsvMetadata.Any(kvp => kvp.Value.columnName == "~Comment");
            var hasNote = testData.CsvMetadata.Any(kvp => kvp.Value.columnName == "~Note");
            Assert.True(hasComment, $"~Comment not found. Keys: {allKeys}");
            Assert.True(hasNote, $"~Note not found. Keys: {allKeys}");

            // Check that multiple '~' columns exist (they will have index suffixes like ~_1, ~_4, etc.)
            var tildeColumns = testData.CsvMetadata.Where(kvp => kvp.Value.columnName == "~").ToList();
            Assert.Equal(4, tildeColumns.Count); // Should have 4 '~' columns at indices 1, 4, 7, 9

            // Find the actual entries for ~Comment and ~Note
            var commentEntry = testData.CsvMetadata.FirstOrDefault(kvp => kvp.Value.columnName == "~Comment");
            var noteEntry = testData.CsvMetadata.FirstOrDefault(kvp => kvp.Value.columnName == "~Note");

            Assert.NotEqual(default, commentEntry);
            Assert.NotEqual(default, noteEntry);

            Assert.Equal("This is a comment", commentEntry.Value.value);
            Assert.Equal("Important note", noteEntry.Value.value);
            Assert.Equal(3, commentEntry.Value.columnIndex);
            Assert.Equal(6, noteEntry.Value.columnIndex);

            // Verify the '~' columns have their values preserved
            var tildeAt1 = tildeColumns.FirstOrDefault(kvp => kvp.Value.columnIndex == 1);
            var tildeAt4 = tildeColumns.FirstOrDefault(kvp => kvp.Value.columnIndex == 4);
            var tildeAt7 = tildeColumns.FirstOrDefault(kvp => kvp.Value.columnIndex == 7);
            var tildeAt9 = tildeColumns.FirstOrDefault(kvp => kvp.Value.columnIndex == 9);

            Assert.NotEqual(default, tildeAt1);
            Assert.NotEqual(default, tildeAt4);
            Assert.NotEqual(default, tildeAt7);
            Assert.NotEqual(default, tildeAt9);

            Assert.Equal("memo1", tildeAt1.Value.value);
            Assert.Equal("memo2", tildeAt4.Value.value);
            Assert.Equal("memo3", tildeAt7.Value.value);
            Assert.Equal("memo4", tildeAt9.Value.value);

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

            // Check that ~ columns are in correct positions
            var headers = headerLine.Split(',');
            Assert.Equal("~", headers[1]);
            Assert.Equal("~", headers[4]);
            Assert.Equal("~", headers[7]);
            Assert.Equal("~", headers[9]);

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

            // Check original data first - should have 10 total columns (4 regular + 6 metadata)
            foreach (var kvp in originalData)
            {
                var metadataKeys = string.Join(", ", kvp.Value.CsvMetadata.Keys.OrderBy(k => k));
                var expectedCount = 10; // Total columns in CSV: Id, ~, Name, ~Comment, ~, Value, ~Note, ~, Description, ~
                if (kvp.Value.CsvMetadata.Count != expectedCount)
                {
                    Assert.True(false, $"Original data {kvp.Key} should have {expectedCount} total columns in CsvMetadata, but has {kvp.Value.CsvMetadata.Count}: [{metadataKeys}]");
                }

                // Check that we have both metadata and regular columns
                var metadataColumns = kvp.Value.CsvMetadata.Keys.Where(k => k.StartsWith("~")).Count();
                var regularColumns = kvp.Value.CsvMetadata.Keys.Where(k => !k.StartsWith("~")).Count();
                Assert.Equal(6, metadataColumns); // ~Comment, ~Note, and 4 '~' columns
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
            Assert.True(metadataColumnCount == 6, $"CSV header should have 6 metadata columns, but has {metadataColumnCount}");

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
                    // Find matching entry by column index since keys might differ
                    var matchingEntry = deserialized.CsvMetadata.FirstOrDefault(kvp => kvp.Value.columnIndex == metaKvp.Value.columnIndex);
                    Assert.NotEqual(default, matchingEntry);
                    Assert.Equal(metaKvp.Value.columnName, matchingEntry.Value.columnName);
                    Assert.Equal(metaKvp.Value.value, matchingEntry.Value.value);
                }
            }
        }
    }
}