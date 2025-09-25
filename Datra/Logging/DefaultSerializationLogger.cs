using System;
using Datra.Interfaces;

namespace Datra.Logging
{
    /// <summary>
    /// Default implementation of ISerializationLogger that writes to console
    /// </summary>
    public class DefaultSerializationLogger : ISerializationLogger
    {
        private readonly bool _enableVerboseLogging;
        private int _currentErrorCount;
        private string _currentFileName;

        public DefaultSerializationLogger(bool enableVerboseLogging = false)
        {
            _enableVerboseLogging = enableVerboseLogging;
        }

        public void LogParsingError(SerializationErrorContext context, Exception exception = null)
        {
            _currentErrorCount++;
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{timestamp}] [ERROR] Parsing failed: {context}");

            if (exception != null && _enableVerboseLogging)
            {
                Console.WriteLine($"  Exception: {exception.GetType().Name}: {exception.Message}");
                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    Console.WriteLine($"  Stack trace: {exception.StackTrace}");
                }
            }

            Console.ResetColor();
        }

        public void LogTypeConversionError(SerializationErrorContext context)
        {
            _currentErrorCount++;
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{timestamp}] [ERROR] Type conversion failed: {context}");
            Console.ResetColor();
        }

        public void LogValidationError(SerializationErrorContext context)
        {
            _currentErrorCount++;
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{timestamp}] [ERROR] Validation failed: {context}");
            Console.ResetColor();
        }

        public void LogWarning(string message, SerializationErrorContext context = null)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

            Console.ForegroundColor = ConsoleColor.Yellow;
            if (context != null)
            {
                Console.WriteLine($"[{timestamp}] [WARNING] {message}: {context}");
            }
            else
            {
                Console.WriteLine($"[{timestamp}] [WARNING] {message}");
            }
            Console.ResetColor();
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
            _currentFileName = fileName;
            _currentErrorCount = 0;

            if (_enableVerboseLogging)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[{timestamp}] [DESERIALIZE] Starting {format} deserialization: {fileName}");
                Console.ResetColor();
            }
        }

        public void LogDeserializationComplete(string fileName, int recordCount, int errorCount)
        {
            if (_enableVerboseLogging || errorCount > 0)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                if (errorCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[{timestamp}] [DESERIALIZE] Completed with errors: {fileName}");
                    Console.WriteLine($"  Records: {recordCount} successful, {errorCount} errors");
                }
                else if (_enableVerboseLogging)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[{timestamp}] [DESERIALIZE] Completed successfully: {fileName}");
                    Console.WriteLine($"  Records: {recordCount}");
                }

                Console.ResetColor();
            }

            _currentFileName = null;
            _currentErrorCount = 0;
        }

        public void LogSerializationStart(string fileName, string format)
        {
            _currentFileName = fileName;

            if (_enableVerboseLogging)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[{timestamp}] [SERIALIZE] Starting {format} serialization: {fileName}");
                Console.ResetColor();
            }
        }

        public void LogSerializationComplete(string fileName, int recordCount)
        {
            if (_enableVerboseLogging)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{timestamp}] [SERIALIZE] Completed: {fileName}");
                Console.WriteLine($"  Records: {recordCount}");
                Console.ResetColor();
            }

            _currentFileName = null;
        }
    }
}