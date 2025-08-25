using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Configures various settings for Datra in the project
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public class DatraConfigurationAttribute : Attribute
    {
        /// <summary>
        /// Enable localization support
        /// Default: false
        /// </summary>
        public bool EnableLocalization { get; set; } = false;
        
        /// <summary>
        /// Path to the LocalizationKeyData file (usually a CSV file with all localization keys)
        /// Default: "Localizations/LocalizationKeys.csv"
        /// </summary>
        public string LocalizationKeyDataPath { get; set; } = "Localizations/LocalizationKeys.csv";
        
        /// <summary>
        /// Path to the LocalizationData files directory (language-specific CSV files)
        /// Files should be named using ISO 639-1 codes (e.g., en.csv, ko.csv, ja.csv)
        /// Default: "Localizations/"
        /// </summary>
        public string LocalizationDataPath { get; set; } = "Localizations/";
        
        /// <summary>
        /// Default language code to use for localization (ISO 639-1 code)
        /// Default: "en" (English)
        /// </summary>
        public string DefaultLanguage { get; set; } = "en";
        
        /// <summary>
        /// Generated DataContext class name
        /// Default: "GameDataContext"
        /// </summary>
        public string DataContextName { get; set; } = "GameDataContext";
        
        /// <summary>
        /// Namespace for generated code
        /// Default: "Datra.Generated"
        /// </summary>
        public string GeneratedNamespace { get; set; } = "Datra.Generated";
        
        /// <summary>
        /// Enable debug logging in generated code
        /// Default: false
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;
    }
}