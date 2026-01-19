using System;
using Datra.Interfaces;

namespace Datra.Logging
{
    /// <summary>
    /// Simple implementation of ISerializationLogger that writes to console without colors.
    /// WebAssembly compatible (no Console.ForegroundColor usage).
    /// </summary>
    public class SimpleSerializationLogger : ISerializationLogger
    {
        private readonly bool _enableVerboseLogging;

        public SimpleSerializationLogger(bool enableVerboseLogging = false)
        {
            _enableVerboseLogging = enableVerboseLogging;
        }

        public void LogParsingError(SerializationErrorContext context, Exception? exception = null)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [ERROR] Parsing failed: {context}");

            if (exception != null && _enableVerboseLogging)
            {
                Console.WriteLine($"  Exception: {exception.GetType().Name}: {exception.Message}");
                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    Console.WriteLine($"  Stack trace: {exception.StackTrace}");
                }
            }
        }

        public void LogTypeConversionError(SerializationErrorContext context)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [ERROR] Type conversion failed: {context}");
        }

        public void LogValidationError(SerializationErrorContext context)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [ERROR] Validation failed: {context}");
        }

        public void LogWarning(string message, SerializationErrorContext? context = null)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            if (context != null)
            {
                Console.WriteLine($"[{timestamp}] [WARNING] {message}: {context}");
            }
            else
            {
                Console.WriteLine($"[{timestamp}] [WARNING] {message}");
            }
        }

        public void LogInfo(string message)
        {
            if (_enableVerboseLogging)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.WriteLine($"[{timestamp}] [INFO] {message}");
            }
        }

        public void LogDeserializationStart(string fileName, string format)
        {
            if (_enableVerboseLogging)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.WriteLine($"[{timestamp}] [DESERIALIZE] Starting {format} deserialization: {fileName}");
            }
        }

        public void LogDeserializationComplete(string fileName, int recordCount, int errorCount)
        {
            if (_enableVerboseLogging || errorCount > 0)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                if (errorCount > 0)
                {
                    Console.WriteLine($"[{timestamp}] [DESERIALIZE] Completed with errors: {fileName}");
                    Console.WriteLine($"  Records: {recordCount} successful, {errorCount} errors");
                }
                else if (_enableVerboseLogging)
                {
                    Console.WriteLine($"[{timestamp}] [DESERIALIZE] Completed successfully: {fileName}");
                    Console.WriteLine($"  Records: {recordCount}");
                }
            }
        }

        public void LogSerializationStart(string fileName, string format)
        {
            if (_enableVerboseLogging)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.WriteLine($"[{timestamp}] [SERIALIZE] Starting {format} serialization: {fileName}");
            }
        }

        public void LogSerializationComplete(string fileName, int recordCount)
        {
            if (_enableVerboseLogging)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.WriteLine($"[{timestamp}] [SERIALIZE] Completed: {fileName}");
                Console.WriteLine($"  Records: {recordCount}");
            }
        }
    }
}
