using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.SampleData.Generated;
using Datra.Interfaces;
using Datra.Logging;
using Datra.SampleData.Models;
using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
    public class SerializationLoggerTests
    {
        private class TestLogger : ISerializationLogger
        {
            public List<string> ParsingErrors { get; } = new List<string>();
            public List<string> TypeConversionErrors { get; } = new List<string>();
            public List<string> ValidationErrors { get; } = new List<string>();
            public int ErrorCount { get; private set; }

            public void LogParsingError(SerializationErrorContext context, Exception? exception = null)
            {
                ErrorCount++;
                ParsingErrors.Add($"{context.PropertyName}: {context.ActualValue}");
            }

            public void LogTypeConversionError(SerializationErrorContext context)
            {
                ErrorCount++;
                TypeConversionErrors.Add($"{context.PropertyName}: {context.ActualValue} -> {context.ExpectedType}");
            }

            public void LogValidationError(SerializationErrorContext context)
            {
                ErrorCount++;
                ValidationErrors.Add(context.Message);
            }

            public void LogWarning(string message, SerializationErrorContext? context = null)
            {
            }

            public void LogInfo(string message)
            {
            }

            public void LogDeserializationStart(string fileName, string format)
            {
            }

            public void LogDeserializationComplete(string fileName, int recordCount, int errorCount)
            {
            }

            public void LogSerializationStart(string fileName, string format)
            {
            }

            public void LogSerializationComplete(string fileName, int recordCount)
            {
            }
        }

        [Fact]
        public async Task TestLoggingWithInvalidData()
        {
            // LoggingTest.csv contains various invalid data
            var testLogger = new TestLogger();
            var basePath = TestDataHelper.FindDataPath();
            var rawDataProvider = new TestRawDataProvider(basePath);
            var serializerFactory = new DataSerializerFactory();

            // Create context with logger
            var context = new GameDataContext(rawDataProvider, serializerFactory, null, testLogger);

            // Load data - should trigger logging for invalid data
            await context.LoadAllAsync();

            // Verify errors were logged
            Assert.True(testLogger.ErrorCount > 0, "Should have logged errors");
            Assert.NotEmpty(testLogger.TypeConversionErrors);

            // Check specific errors
            var allErrors = string.Join(" ", testLogger.TypeConversionErrors);
            Assert.Contains("Level", allErrors); // INVALID_INT for Level
            Assert.Contains("SkillType", allErrors); // InvalidSkill enum value
            Assert.Contains("Power", allErrors); // INVALID_FLOAT for Power
            Assert.Contains("Rarity", allErrors); // SuperRare invalid enum
            Assert.Contains("IsActive", allErrors); // NotBool for boolean
            Assert.Contains("Costs", allErrors); // abc in integer array
            Assert.Contains("AvailableTypes", allErrors); // Unknown and InvalidType in enum array

            // Should still load valid record (test_001)
            var loggingData = context.LoggingTest;
            Assert.NotNull(loggingData);
            Assert.True(loggingData.ContainsKey("test_001"));
            var validRecord = loggingData["test_001"];
            Assert.Equal("Fire Ball", validRecord.Name);
            Assert.Equal(5, validRecord.Level);
        }

        [Fact]
        public async Task TestNullLoggerDoesNotThrow()
        {
            var basePath = TestDataHelper.FindDataPath();
            var rawDataProvider = new TestRawDataProvider(basePath);
            var serializerFactory = new DataSerializerFactory();

            // Create context without logger (null)
            var context = new GameDataContext(rawDataProvider, serializerFactory, null, null);

            // Should not throw even with invalid data
            var exception = await Record.ExceptionAsync(() => context.LoadAllAsync());
            Assert.Null(exception);

            // Should still load some data
            Assert.NotNull(context.LoggingTest);
            Assert.True(context.LoggingTest.Count > 0);
        }

        [Fact]
        public async Task TestDefaultLoggerConsoleOutput()
        {
            // Capture console output
            var consoleOutput = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(consoleOutput);

            try
            {
                var logger = new DefaultSerializationLogger(enableVerboseLogging: true);
                var basePath = TestDataHelper.FindDataPath();
                var rawDataProvider = new TestRawDataProvider(basePath);
                var serializerFactory = new DataSerializerFactory();
                var context = new GameDataContext(rawDataProvider, serializerFactory, null, logger);

                await context.LoadAllAsync();

                var output = consoleOutput.ToString();

                // Check that logging output contains expected messages
                Assert.Contains("[DESERIALIZE]", output);
                Assert.Contains("LoggingTest.csv", output);
                Assert.Contains("[ERROR]", output);
                Assert.Contains("Type conversion failed", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void TestSerializationErrorContextToString()
        {
            var context = new SerializationErrorContext
            {
                FileName = "test.csv",
                LineNumber = 10,
                PropertyName = "Level",
                ActualValue = "ABC",
                ExpectedType = "int",
                RecordId = "test_123",
                Message = "Invalid format"
            };

            var result = context.ToString();

            Assert.Contains("test.csv", result);
            Assert.Contains("Line: 10", result);
            Assert.Contains("Property: Level", result);
            Assert.Contains("Expected: int", result);
            Assert.Contains("Actual: ABC", result);
            Assert.Contains("Record ID: test_123", result);
            Assert.Contains("Invalid format", result);
        }
    }
}