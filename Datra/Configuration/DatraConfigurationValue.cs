namespace Datra.Configuration
{
    /// <summary>
    /// Holds the configuration values for Datra, either from DatraConfigurationAttribute or defaults
    /// </summary>
    public class DatraConfigurationValue
    {
        /// <summary>
        /// Enable localization support
        /// </summary>
        public bool EnableLocalization { get; }
        
        /// <summary>
        /// Path to the LocalizationKeyData file (usually a CSV file with all localization keys)
        /// </summary>
        public string LocalizationKeyDataPath { get; }
        
        /// <summary>
        /// Path to the LocalizationData files directory (language-specific CSV files)
        /// </summary>
        public string LocalizationDataPath { get; }
        
        /// <summary>
        /// Default language code to use for localization (ISO 639-1 code)
        /// </summary>
        public string DefaultLanguage { get; }
        
        /// <summary>
        /// Generated DataContext class name
        /// </summary>
        public string DataContextName { get; }
        
        /// <summary>
        /// Namespace for generated code
        /// </summary>
        public string GeneratedNamespace { get; }
        
        /// <summary>
        /// Enable debug logging in generated code
        /// </summary>
        public bool EnableDebugLogging { get; }
        
        /// <summary>
        /// The delimiter used to separate array elements in CSV files
        /// Default is '|'
        /// </summary>
        public char CsvArrayDelimiter { get; }
        
        /// <summary>
        /// The delimiter used to separate fields in CSV files
        /// Default is ','
        /// </summary>
        public char CsvFieldDelimiter { get; }

        /// <summary>
        /// Emit generated files as physical files in the project for easier debugging
        /// </summary>
        public bool EmitPhysicalFiles { get; }

        /// <summary>
        /// Path for physical files when EmitPhysicalFiles is true
        /// </summary>
        public string? PhysicalFilesPath { get; }

        public DatraConfigurationValue(
            bool enableLocalization = false,
            string localizationKeyDataPath = "Localizations/LocalizationKeys.csv",
            string localizationDataPath = "Localizations/",
            string defaultLanguage = "en",
            string dataContextName = "GameDataContext",
            string generatedNamespace = "Datra.Generated",
            bool enableDebugLogging = false,
            char csvArrayDelimiter = '|',
            char csvFieldDelimiter = ',',
            bool emitPhysicalFiles = false,
            string? physicalFilesPath = null)
        {
            EnableLocalization = enableLocalization;
            LocalizationKeyDataPath = localizationKeyDataPath;
            LocalizationDataPath = localizationDataPath;
            DefaultLanguage = defaultLanguage;
            DataContextName = dataContextName;
            GeneratedNamespace = generatedNamespace;
            EnableDebugLogging = enableDebugLogging;
            CsvArrayDelimiter = csvArrayDelimiter;
            CsvFieldDelimiter = csvFieldDelimiter;
            EmitPhysicalFiles = emitPhysicalFiles;
            PhysicalFilesPath = physicalFilesPath;
        }
        
        /// <summary>
        /// Creates a default configuration value
        /// </summary>
        public static DatraConfigurationValue CreateDefault()
        {
            return new DatraConfigurationValue();
        }
    }
}