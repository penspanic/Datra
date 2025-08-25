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
        /// Path to the LocalizationKeyData file (usually a CSV file with all localization keys)
        /// Default: "Localizations/LocalizationKeys.csv"
        /// </summary>
        public string LocalizationKeyDataPath { get; set; } = "Localizations/LocalizationKeys.csv";
        
        /// <summary>
        /// Path to the LocalizationData files directory (language-specific CSV files)
        /// Default: "Localizations/"
        /// </summary>
        public string LocalizationDataPath { get; set; } = "Localizations/";
        
        /// <summary>
        /// Default language code to use for localization
        /// Default: "English"
        /// </summary>
        public string DefaultLanguage { get; set; } = "English";
        
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