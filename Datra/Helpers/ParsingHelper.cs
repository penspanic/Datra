using System;
using System.Globalization;
using Datra.Interfaces;

namespace Datra.Helpers
{
    /// <summary>
    /// Helper class for parsing values with logging support
    /// </summary>
    public static class ParsingHelper
    {
        /// <summary>
        /// Parse integer value with logging support
        /// </summary>
        public static int ParseInt(string value, int defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (int.TryParse(value, out var result))
                return result;

            LogParsingError(value, "int", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse float value with logging support
        /// </summary>
        public static float ParseFloat(string value, float defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                return result;

            LogParsingError(value, "float", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse double value with logging support
        /// </summary>
        public static double ParseDouble(string value, double defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                return result;

            LogParsingError(value, "double", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse boolean value with logging support
        /// </summary>
        public static bool ParseBool(string value, bool defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (bool.TryParse(value, out var result))
                return result;

            LogParsingError(value, "bool", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse enum value with logging support
        /// </summary>
        public static T ParseEnum<T>(string value, T defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName) where T : struct, Enum
        {
            if (Enum.TryParse<T>(value, true, out var result))
                return result;

            LogParsingError(value, typeof(T).Name, logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse long value with logging support
        /// </summary>
        public static long ParseLong(string value, long defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (long.TryParse(value, out var result))
                return result;

            LogParsingError(value, "long", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse short value with logging support
        /// </summary>
        public static short ParseShort(string value, short defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (short.TryParse(value, out var result))
                return result;

            LogParsingError(value, "short", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse byte value with logging support
        /// </summary>
        public static byte ParseByte(string value, byte defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (byte.TryParse(value, out var result))
                return result;

            LogParsingError(value, "byte", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse decimal value with logging support
        /// </summary>
        public static decimal ParseDecimal(string value, decimal defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;

            LogParsingError(value, "decimal", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse uint value with logging support
        /// </summary>
        public static uint ParseUInt(string value, uint defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (uint.TryParse(value, out var result))
                return result;

            LogParsingError(value, "uint", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse ulong value with logging support
        /// </summary>
        public static ulong ParseULong(string value, ulong defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (ulong.TryParse(value, out var result))
                return result;

            LogParsingError(value, "ulong", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse ushort value with logging support
        /// </summary>
        public static ushort ParseUShort(string value, ushort defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (ushort.TryParse(value, out var result))
                return result;

            LogParsingError(value, "ushort", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse sbyte value with logging support
        /// </summary>
        public static sbyte ParseSByte(string value, sbyte defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (sbyte.TryParse(value, out var result))
                return result;

            LogParsingError(value, "sbyte", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        /// <summary>
        /// Parse char value with logging support
        /// </summary>
        public static char ParseChar(string value, char defaultValue,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (!string.IsNullOrEmpty(value) && value.Length == 1)
                return value[0];

            if (char.TryParse(value, out var result))
                return result;

            LogParsingError(value, "char", logger, fileName, lineNumber, propertyName);
            return defaultValue;
        }

        private static void LogParsingError(string value, string expectedType,
            ISerializationLogger logger, string fileName, int lineNumber, string propertyName)
        {
            if (logger != null)
            {
                var context = new SerializationErrorContext
                {
                    FileName = fileName,
                    LineNumber = lineNumber,
                    PropertyName = propertyName,
                    ActualValue = value,
                    ExpectedType = expectedType,
                    Message = $"Failed to parse '{value}' as {expectedType}"
                };
                logger.LogTypeConversionError(context);
            }
        }
    }
}