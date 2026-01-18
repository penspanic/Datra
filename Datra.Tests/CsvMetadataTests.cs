#pragma warning disable DATRA001 // Allow setting properties on data classes in tests

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Configuration;
using Datra.SampleData.Generated;
using Datra.SampleData.Models;
using Xunit;

namespace Datra.Tests
{
    public class CsvMetadataTests
    {
        private readonly GameDataContext _context;
        private readonly string _testDataPath;

        public CsvMetadataTests()
        {
            _context = TestDataHelper.CreateGameDataContext();
            _context.LoadAllAsync().Wait();
            _testDataPath = TestDataHelper.FindDataPath();
        }
        [Fact]
        public void Should_PreserveMetadataColumns_WhenReadingCsv()
        {
            // Act - Load MetadataTestData from CSV
            var testData = _context.MetadataTest.TryGetLoaded("test_001");
            if (testData == null)
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

            // Check that multiple '~' columns exist (they are now numbered ~1, ~2, ~3, ~4)
            var tildeColumns = testData.CsvMetadata.Where(kvp =>
                kvp.Value.columnName.StartsWith("~") &&
                char.IsDigit(kvp.Value.columnName.LastOrDefault())).ToList();
            Assert.Equal(4, tildeColumns.Count); // Should have 4 numbered '~' columns

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
            var originalData = _context.MetadataTest.TryGetLoaded("test_001");
            if (originalData == null)
            {
                Assert.Fail("MetadataTestData not found");
                return;
            }

            // Act - Serialize back to CSV
            var dataDict = new Dictionary<string, MetadataTestData>();
            foreach (var kvp in _context.MetadataTest.LoadedItems)
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

            // Check that ~ columns are numbered (now ~1, ~2, ~3, ~4)
            var headers = headerLine.Split(',');
            Assert.Equal("~1", headers[1]);
            Assert.Equal("~2", headers[4]);
            Assert.Equal("~3", headers[7]);
            Assert.Equal("~4", headers[9]);

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
            Assert.NotEmpty(originalData.LoadedItems);

            // Check original data first - should have 10 total columns (4 regular + 6 metadata)
            foreach (var kvp in originalData.LoadedItems)
            {
                var metadataKeys = string.Join(", ", kvp.Value.CsvMetadata.Keys.OrderBy(k => k));
                var expectedCount = 10; // Total columns in CSV: Id, ~, Name, ~Comment, ~, Value, ~Note, ~, Description, ~
                if (kvp.Value.CsvMetadata.Count != expectedCount)
                {
                    Assert.True(false, $"Original data {kvp.Key} should have {expectedCount} total columns in CsvMetadata, but has {kvp.Value.CsvMetadata.Count}: [{metadataKeys}]");
                }

                // Check that we have both metadata and regular columns
                // The '~' columns should be renamed to ~1, ~2, ~3, ~4
                var metadataColumns = kvp.Value.CsvMetadata.Keys.Where(k => k.StartsWith("~")).Count();
                var regularColumns = kvp.Value.CsvMetadata.Keys.Where(k => !k.StartsWith("~")).Count();
                Assert.Equal(6, metadataColumns); // ~Comment, ~Note, and 4 '~' columns (now ~1, ~2, ~3, ~4)
                Assert.Equal(4, regularColumns);  // Id, Name, Value, Description

                // Verify that numbered ~ columns exist
                Assert.True(kvp.Value.CsvMetadata.ContainsKey("~1"), "Should have ~1 column");
                Assert.True(kvp.Value.CsvMetadata.ContainsKey("~2"), "Should have ~2 column");
                Assert.True(kvp.Value.CsvMetadata.ContainsKey("~3"), "Should have ~3 column");
                Assert.True(kvp.Value.CsvMetadata.ContainsKey("~4"), "Should have ~4 column");
            }

            // Act - Serialize and deserialize
            var dataDict = new Dictionary<string, MetadataTestData>();
            foreach (var kvp in originalData.LoadedItems)
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
            foreach (var kvp in originalData.LoadedItems)
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
        [Fact]
        public async Task Should_AddNewProperties_WhenClassHasNewFields()
        {
            // Arrange - Get test data and create modified dictionary
            var originalData = _context.NewPropertyTest;
            Assert.NotEmpty(originalData.LoadedItems);

            // Store original CSV content for restoration
            var csvPath = Path.Combine(_testDataPath, "NewPropertyTestData.csv");
            var originalCsvContent = await File.ReadAllTextAsync(csvPath);

            try
            {
                // Create a new dictionary with modified data
                var modifiedData = new Dictionary<string, Datra.SampleData.Models.NewPropertyTestData>();
                foreach (var kvp in originalData.LoadedItems)
                {
                    // Create new instance with modified values
                    var newData = new Datra.SampleData.Models.NewPropertyTestData
                    {
                        Id = kvp.Value.Id,
                        Name = kvp.Value.Name,
                        Level = kvp.Value.Level,
                        // Set new property values
                        Category = "Fighter",
                        Health = 100,
                        Attack = 50
                    };
                    modifiedData[kvp.Key] = newData;
                }

                // Act - Serialize to CSV
                var csvContent = NewPropertyTestDataSerializer.SerializeCsv(modifiedData);

                // Assert - Check that new columns were added
                var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var headerLine = lines[0];

                // Should have original columns plus new ones
                Assert.Contains("Id", headerLine);
                Assert.Contains("Name", headerLine);
                Assert.Contains("Level", headerLine);
                Assert.Contains("Health", headerLine);
                Assert.Contains("Attack", headerLine);
                Assert.Contains("Category", headerLine);

                // Check column order - new properties should be inserted in class definition order
                var headers = headerLine.Split(',');
                var idIndex = Array.IndexOf(headers, "Id");
                var categoryIndex = Array.IndexOf(headers, "Category");
                var nameIndex = Array.IndexOf(headers, "Name");
                var healthIndex = Array.IndexOf(headers, "Health");
                var levelIndex = Array.IndexOf(headers, "Level");
                var attackIndex = Array.IndexOf(headers, "Attack");

                // Properties should be in exact class order:
                // Id -> Category (new) -> Name -> Health (new) -> Level -> Attack (new)
                Assert.Equal(0, idIndex);
                Assert.Equal(1, categoryIndex);  // New property inserted between Id and Name
                Assert.Equal(2, nameIndex);
                Assert.Equal(3, healthIndex);    // New property inserted between Name and Level
                Assert.Equal(4, levelIndex);
                Assert.Equal(5, attackIndex);    // New property added after Level

                // Verify the exact order
                Assert.True(idIndex < categoryIndex, "Id should come before Category");
                Assert.True(categoryIndex < nameIndex, "Category should come before Name");
                Assert.True(nameIndex < healthIndex, "Name should come before Health");
                Assert.True(healthIndex < levelIndex, "Health should come before Level");
                Assert.True(levelIndex < attackIndex, "Level should come before Attack");

                // Check that data values are present
                var dataLine = lines[1];
                Assert.Contains("100", dataLine); // Health value
                Assert.Contains("50", dataLine);  // Attack value
                Assert.Contains("Fighter", dataLine); // Category value

                // Act - Save to file and reload
                await File.WriteAllTextAsync(csvPath, csvContent);
                var reloadedData = NewPropertyTestDataSerializer.DeserializeCsv(csvContent);

                // Assert - Check that reloaded data has all properties
                var firstItem = reloadedData.Values.First();
                Assert.Equal(100, firstItem.Health);
                Assert.Equal(50, firstItem.Attack);
                Assert.Equal("Fighter", firstItem.Category);

                // Round-trip test: serialize again and check consistency
                var secondCsvContent = NewPropertyTestDataSerializer.SerializeCsv(reloadedData);
                Assert.Equal(csvContent, secondCsvContent);
            }
            finally
            {
                // Restore original CSV content
                await File.WriteAllTextAsync(csvPath, originalCsvContent);
            }
        }
    }
}