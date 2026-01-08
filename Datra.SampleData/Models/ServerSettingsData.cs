using Datra.Attributes;
using Datra.DataTypes;

namespace Datra.SampleData.Models
{
    public enum ServerRegion
    {
        Asia,
        Europe,
        NorthAmerica,
        SouthAmerica,
        Oceania
    }

    /// <summary>
    /// Server settings stored in YAML format for testing YAML SingleData serialization.
    /// </summary>
    [SingleData("ServerSettings.yaml", Format = DataFormat.Yaml)]
    public partial class ServerSettingsData
    {
        public string ServerName { get; set; }
        public string Version { get; set; }
        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public bool MaintenanceMode { get; set; }
        public float TickRate { get; set; }
        public double SyncInterval { get; set; }
        public ServerRegion Region { get; set; }
        public ServerRegion[] AllowedRegions { get; set; }
        public string[] AdminIds { get; set; }
        public string WelcomeMessage { get; set; }

        // DataRef example in SingleData
        public StringDataRef<CharacterData> DefaultCharacter { get; set; }
    }
}
