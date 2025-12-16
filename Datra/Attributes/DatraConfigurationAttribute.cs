#nullable enable

using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Configures Datra data context generation for this assembly.
    /// Each assembly that contains data models must have this attribute with a unique context name.
    /// </summary>
    /// <example>
    /// // Simple usage - context name only
    /// [assembly: DatraConfiguration("GameData")]
    ///
    /// // With custom namespace
    /// [assembly: DatraConfiguration("GameData", Namespace = "MyGame.Data")]
    ///
    /// // With localization enabled
    /// [assembly: DatraConfiguration("GameData", EnableLocalization = true)]
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public class DatraConfigurationAttribute : Attribute
    {
        /// <summary>
        /// Creates a new DatraConfiguration with the specified context name.
        /// </summary>
        /// <param name="contextName">
        /// The name for the generated DataContext class.
        /// This will generate a class named "{contextName}Context" (e.g., "GameData" → "GameDataContext").
        /// Must be a valid C# identifier.
        /// </param>
        public DatraConfigurationAttribute(string contextName)
        {
            ContextName = contextName ?? throw new ArgumentNullException(nameof(contextName));
        }

        /// <summary>
        /// The name for the generated DataContext class.
        /// The generated class will be named "{ContextName}Context".
        /// </summary>
        public string ContextName { get; }

        /// <summary>
        /// Namespace for generated code.
        /// Default: "{AssemblyName}.Generated"
        /// </summary>
        public string? Namespace { get; set; }

        /// <summary>
        /// Enable localization support.
        /// Default: false
        /// </summary>
        public bool EnableLocalization { get; set; } = false;

        /// <summary>
        /// Path to the LocalizationKeyData file (usually a CSV file with all localization keys).
        /// Default: "Localizations/LocalizationKeys.csv"
        /// </summary>
        public string LocalizationKeyDataPath { get; set; } = "Localizations/LocalizationKeys.csv";

        /// <summary>
        /// Path to the LocalizationData files directory (language-specific CSV files).
        /// Files should be named using ISO 639-1 codes (e.g., en.csv, ko.csv, ja.csv).
        /// Default: "Localizations/"
        /// </summary>
        public string LocalizationDataPath { get; set; } = "Localizations/";

        /// <summary>
        /// Default language code to use for localization (ISO 639-1 code).
        /// Default: "en" (English)
        /// </summary>
        public string DefaultLanguage { get; set; } = "en";

        /// <summary>
        /// Enable debug logging in generated code.
        /// Default: false
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Emit generated files as physical files in the project for easier debugging.
        /// When false, files are only generated in obj/ folder.
        /// When true, files are written to the project directory.
        /// Default: false
        /// </summary>
        public bool EmitPhysicalFiles { get; set; } = false;

        /// <summary>
        /// Path for physical files when EmitPhysicalFiles is true.
        /// null or empty: Generate files next to their source models (models) and project root (context).
        /// "Generated/": Generate all files in the specified folder relative to project root.
        /// Default: null
        /// </summary>
        public string? PhysicalFilesPath { get; set; } = null;
    }
}