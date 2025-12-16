using Datra.Attributes;

[assembly: DatraConfiguration("GameData",
    Namespace = "Datra.SampleData.Generated",
    EnableLocalization = true,
    LocalizationKeyDataPath = "Localizations/LocalizationKeys.csv",
    EmitPhysicalFiles = false
)]
