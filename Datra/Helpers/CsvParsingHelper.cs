using System;
using System.Collections.Generic;
using System.Text;

namespace Datra.Helpers
{
    /// <summary>
    /// Helper class for parsing CSV lines with proper handling of quoted fields
    /// </summary>
    public static class CsvParsingHelper
    {
        /// <summary>
        /// Parse a CSV line with proper handling of quoted fields containing commas and escaped quotes
        /// </summary>
        /// <param name="line">The CSV line to parse</param>
        /// <param name="delimiter">The delimiter character (default is comma)</param>
        /// <returns>Array of field values</returns>
        public static string[] ParseCsvLine(string line, char delimiter = ',')
        {
            if (string.IsNullOrEmpty(line))
                return new string[0];

            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;
            bool wasInQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char currentChar = line[i];

                if (currentChar == '"')
                {
                    if (!inQuotes)
                    {
                        // Starting a quoted field
                        inQuotes = true;
                        wasInQuotes = true;
                    }
                    else if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote ("") - add a single quote
                        currentField.Append('"');
                        i++; // Skip the next quote
                    }
                    else
                    {
                        // Ending a quoted field
                        inQuotes = false;
                    }
                }
                else if (currentChar == delimiter && !inQuotes)
                {
                    // Field delimiter found outside quotes
                    fields.Add(wasInQuotes ? currentField.ToString() : currentField.ToString());
                    currentField.Clear();
                    wasInQuotes = false;
                }
                else
                {
                    // Regular character
                    currentField.Append(currentChar);
                }
            }

            // Add the last field
            fields.Add(wasInQuotes ? currentField.ToString() : currentField.ToString());

            return fields.ToArray();
        }

        /// <summary>
        /// Escape a CSV field value for proper serialization
        /// </summary>
        /// <param name="value">The value to escape</param>
        /// <param name="delimiter">The delimiter character</param>
        /// <param name="alwaysQuote">Whether to always quote the field</param>
        /// <returns>Properly escaped CSV field value</returns>
        public static string EscapeCsvField(string value, char delimiter = ',', bool alwaysQuote = false)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool needsQuoting = alwaysQuote ||
                               value.Contains(delimiter.ToString()) ||
                               value.Contains("\"") ||
                               value.Contains("\n") ||
                               value.Contains("\r");

            if (!needsQuoting)
                return value;

            // Escape quotes by doubling them
            string escaped = value.Replace("\"", "\"\"");

            // Wrap in quotes
            return $"\"{escaped}\"";
        }

        /// <summary>
        /// Join CSV fields with proper escaping
        /// </summary>
        /// <param name="fields">The fields to join</param>
        /// <param name="delimiter">The delimiter character</param>
        /// <returns>Properly formatted CSV line</returns>
        public static string JoinCsvFields(string[] fields, char delimiter = ',')
        {
            if (fields == null || fields.Length == 0)
                return string.Empty;

            var escapedFields = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                escapedFields[i] = EscapeCsvField(fields[i], delimiter);
            }

            return string.Join(delimiter.ToString(), escapedFields);
        }
    }
}