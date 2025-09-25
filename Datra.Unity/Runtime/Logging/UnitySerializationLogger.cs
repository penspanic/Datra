using System;
using Datra.Interfaces;
using UnityEngine;

namespace Datra.Unity.Logging
{
    /// <summary>
    /// Unity-specific implementation of ISerializationLogger that uses Unity's Debug.Log system
    /// </summary>
    public class UnitySerializationLogger : ISerializationLogger
    {
        private readonly bool _enableVerboseLogging;
        private readonly bool _useColoredOutput;
        private int _totalErrorCount;
        private string _currentFileName = string.Empty;

        /// <summary>
        /// Creates a new Unity serialization logger
        /// </summary>
        /// <param name="enableVerboseLogging">Enable verbose logging for info messages</param>
        /// <param name="useColoredOutput">Use rich text colors in console output</param>
        public UnitySerializationLogger(bool enableVerboseLogging = false, bool useColoredOutput = true)
        {
            _enableVerboseLogging = enableVerboseLogging;
            _useColoredOutput = useColoredOutput;
        }

        public void LogParsingError(SerializationErrorContext context, Exception exception = null)
        {
            _totalErrorCount++;
            var message = FormatErrorMessage("PARSE ERROR", context);
            if (exception != null)
            {
                message += $"\nException: {exception.Message}";
            }

            if (_useColoredOutput)
            {
                Debug.LogError($"<color=red>{message}</color>");
            }
            else
            {
                Debug.LogError(message);
            }
        }

        public void LogTypeConversionError(SerializationErrorContext context)
        {
            _totalErrorCount++;
            var message = FormatErrorMessage("TYPE CONVERSION ERROR", context);

            if (_useColoredOutput)
            {
                Debug.LogWarning($"<color=orange>{message}</color>");
            }
            else
            {
                Debug.LogWarning(message);
            }
        }

        public void LogValidationError(SerializationErrorContext context)
        {
            _totalErrorCount++;
            var message = FormatErrorMessage("VALIDATION ERROR", context);

            if (_useColoredOutput)
            {
                Debug.LogWarning($"<color=yellow>{message}</color>");
            }
            else
            {
                Debug.LogWarning(message);
            }
        }

        public void LogWarning(string message, SerializationErrorContext context = null)
        {
            var formattedMessage = context != null
                ? $"[WARNING] {message}\n{FormatContext(context)}"
                : $"[WARNING] {message}";

            if (_useColoredOutput)
            {
                Debug.LogWarning($"<color=yellow>{formattedMessage}</color>");
            }
            else
            {
                Debug.LogWarning(formattedMessage);
            }
        }

        public void LogInfo(string message)
        {
            if (!_enableVerboseLogging)
                return;

            if (_useColoredOutput)
            {
                Debug.Log($"<color=cyan>[INFO] {message}</color>");
            }
            else
            {
                Debug.Log($"[INFO] {message}");
            }
        }

        public void LogDeserializationStart(string fileName, string format)
        {
            _currentFileName = fileName;
            if (_enableVerboseLogging)
            {
                var message = $"[DESERIALIZE] Starting {format} deserialization of '{fileName}'";
                if (_useColoredOutput)
                {
                    Debug.Log($"<color=green>{message}</color>");
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }

        public void LogDeserializationComplete(string fileName, int recordCount, int errorCount)
        {
            var message = $"[DESERIALIZE] Completed '{fileName}': {recordCount} records loaded";

            if (errorCount > 0)
            {
                message += $", {errorCount} errors";
                if (_useColoredOutput)
                {
                    Debug.LogWarning($"<color=orange>{message}</color>");
                }
                else
                {
                    Debug.LogWarning(message);
                }
            }
            else if (_enableVerboseLogging)
            {
                if (_useColoredOutput)
                {
                    Debug.Log($"<color=green>{message}</color>");
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }

        public void LogSerializationStart(string fileName, string format)
        {
            _currentFileName = fileName;
            if (_enableVerboseLogging)
            {
                var message = $"[SERIALIZE] Starting {format} serialization to '{fileName}'";
                if (_useColoredOutput)
                {
                    Debug.Log($"<color=green>{message}</color>");
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }

        public void LogSerializationComplete(string fileName, int recordCount)
        {
            if (_enableVerboseLogging)
            {
                var message = $"[SERIALIZE] Completed '{fileName}': {recordCount} records written";
                if (_useColoredOutput)
                {
                    Debug.Log($"<color=green>{message}</color>");
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }

        /// <summary>
        /// Gets the total number of errors logged
        /// </summary>
        public int TotalErrorCount => _totalErrorCount;

        /// <summary>
        /// Resets the error count
        /// </summary>
        public void ResetErrorCount()
        {
            _totalErrorCount = 0;
        }

        private string FormatErrorMessage(string errorType, SerializationErrorContext context)
        {
            var message = $"[{errorType}] in {context.FileName}";

            if (context.LineNumber > 0)
            {
                message += $" (Line {context.LineNumber})";
            }

            if (!string.IsNullOrEmpty(context.RecordId))
            {
                message += $" [Record: {context.RecordId}]";
            }

            message += $"\n{context.Message}";
            message += $"\n{FormatContext(context)}";

            return message;
        }

        private string FormatContext(SerializationErrorContext context)
        {
            var details = "";

            if (!string.IsNullOrEmpty(context.PropertyName))
            {
                details += $"  Property: {context.PropertyName}\n";
            }

            if (!string.IsNullOrEmpty(context.ExpectedType))
            {
                details += $"  Expected Type: {context.ExpectedType}\n";
            }

            if (!string.IsNullOrEmpty(context.ActualValue))
            {
                details += $"  Actual Value: '{context.ActualValue}'\n";
            }

            return details.TrimEnd();
        }
    }

    /// <summary>
    /// Singleton instance for convenient access in Unity
    /// </summary>
    public static class UnityLogger
    {
        private static UnitySerializationLogger _instance;

        /// <summary>
        /// Gets the default Unity serialization logger instance
        /// </summary>
        public static UnitySerializationLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new UnitySerializationLogger(
                        enableVerboseLogging: Application.isEditor, // Verbose in editor only
                        useColoredOutput: true
                    );
                }
                return _instance;
            }
        }

        /// <summary>
        /// Creates a custom configured logger
        /// </summary>
        public static UnitySerializationLogger Create(bool enableVerboseLogging, bool useColoredOutput = true)
        {
            return new UnitySerializationLogger(enableVerboseLogging, useColoredOutput);
        }

        /// <summary>
        /// Resets the singleton instance
        /// </summary>
        public static void Reset()
        {
            _instance = null;
        }
    }
}