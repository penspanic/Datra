using System;

namespace Datra.Interfaces
{
    /// <summary>
    /// Interface for logging serialization and deserialization events, errors, and warnings
    /// </summary>
    public interface ISerializationLogger
    {
        /// <summary>
        /// Logs when a parsing error occurs during deserialization
        /// </summary>
        /// <param name="context">Context information about where the error occurred</param>
        /// <param name="exception">The exception that was thrown, if any</param>
        void LogParsingError(SerializationErrorContext context, Exception exception = null);

        /// <summary>
        /// Logs when a type conversion fails
        /// </summary>
        /// <param name="context">Context information about the conversion failure</param>
        void LogTypeConversionError(SerializationErrorContext context);

        /// <summary>
        /// Logs when a validation error occurs
        /// </summary>
        /// <param name="context">Context information about the validation failure</param>
        void LogValidationError(SerializationErrorContext context);

        /// <summary>
        /// Logs a warning that doesn't prevent serialization/deserialization
        /// </summary>
        /// <param name="message">Warning message</param>
        /// <param name="context">Optional context information</param>
        void LogWarning(string message, SerializationErrorContext context = null);

        /// <summary>
        /// Logs general information during serialization/deserialization
        /// </summary>
        /// <param name="message">Information message</param>
        void LogInfo(string message);

        /// <summary>
        /// Called when starting to deserialize a data file
        /// </summary>
        /// <param name="fileName">Name of the file being deserialized</param>
        /// <param name="format">Format of the file (CSV, JSON, YAML)</param>
        void LogDeserializationStart(string fileName, string format);

        /// <summary>
        /// Called when deserialization completes
        /// </summary>
        /// <param name="fileName">Name of the file that was deserialized</param>
        /// <param name="recordCount">Number of records successfully deserialized</param>
        /// <param name="errorCount">Number of errors encountered</param>
        void LogDeserializationComplete(string fileName, int recordCount, int errorCount);

        /// <summary>
        /// Called when starting to serialize data
        /// </summary>
        /// <param name="fileName">Name of the target file</param>
        /// <param name="format">Format of the file (CSV, JSON, YAML)</param>
        void LogSerializationStart(string fileName, string format);

        /// <summary>
        /// Called when serialization completes
        /// </summary>
        /// <param name="fileName">Name of the file that was serialized</param>
        /// <param name="recordCount">Number of records serialized</param>
        void LogSerializationComplete(string fileName, int recordCount);
    }

    /// <summary>
    /// Contains context information about where a serialization error occurred
    /// </summary>
    public class SerializationErrorContext
    {
        /// <summary>
        /// The file being processed when the error occurred
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The data format (CSV, JSON, YAML)
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Line number where the error occurred (1-based, if applicable)
        /// </summary>
        public int? LineNumber { get; set; }

        /// <summary>
        /// Column name or property name where the error occurred
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// The actual value that caused the error
        /// </summary>
        public string ActualValue { get; set; }

        /// <summary>
        /// The expected type or format
        /// </summary>
        public string ExpectedType { get; set; }

        /// <summary>
        /// Additional error message or description
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The ID of the record being processed (if applicable)
        /// </summary>
        public string RecordId { get; set; }

        /// <summary>
        /// Creates a formatted error message from the context
        /// </summary>
        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(FileName))
                parts.Add($"File: {FileName}");

            if (LineNumber.HasValue)
                parts.Add($"Line: {LineNumber}");

            if (!string.IsNullOrEmpty(RecordId))
                parts.Add($"Record ID: {RecordId}");

            if (!string.IsNullOrEmpty(PropertyName))
                parts.Add($"Property: {PropertyName}");

            if (!string.IsNullOrEmpty(ExpectedType))
                parts.Add($"Expected: {ExpectedType}");

            if (!string.IsNullOrEmpty(ActualValue))
                parts.Add($"Actual: {ActualValue}");

            if (!string.IsNullOrEmpty(Message))
                parts.Add($"Message: {Message}");

            return string.Join(", ", parts);
        }
    }
}