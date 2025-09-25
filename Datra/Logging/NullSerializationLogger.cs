using System;
using Datra.Interfaces;

namespace Datra.Logging
{
    /// <summary>
    /// A no-op implementation of ISerializationLogger that discards all log messages
    /// </summary>
    public class NullSerializationLogger : ISerializationLogger
    {
        /// <summary>
        /// Singleton instance of NullSerializationLogger
        /// </summary>
        public static readonly NullSerializationLogger Instance = new NullSerializationLogger();

        private NullSerializationLogger() { }

        public void LogParsingError(SerializationErrorContext context, Exception exception = null) { }

        public void LogTypeConversionError(SerializationErrorContext context) { }

        public void LogValidationError(SerializationErrorContext context) { }

        public void LogWarning(string message, SerializationErrorContext context = null) { }

        public void LogInfo(string message) { }

        public void LogDeserializationStart(string fileName, string format) { }

        public void LogDeserializationComplete(string fileName, int recordCount, int errorCount) { }

        public void LogSerializationStart(string fileName, string format) { }

        public void LogSerializationComplete(string fileName, int recordCount) { }
    }
}