namespace Datra.Configuration
{
    /// <summary>
    /// Configuration options for Datra data handling
    /// </summary>
    public record DatraConfiguration
    {
        /// <summary>
        /// Default configuration instance
        /// </summary>
        public static DatraConfiguration CreateDefault() => new();

        /// <summary>
        /// The delimiter used to separate array elements in CSV files
        /// Default is '|'
        /// </summary>
        public char CsvArrayDelimiter { get; set; } = '|';
        
        /// <summary>
        /// The delimiter used to separate fields in CSV files
        /// Default is ','
        /// </summary>
        public char CsvFieldDelimiter { get; } = ',';
    }
}