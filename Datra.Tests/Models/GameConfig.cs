using Datra.Data.Attributes;

namespace Datra.Tests.Models
{
    [SingleData("GameConfig.yaml", Format = DataFormat.Yaml)]
    public partial class GameConfig
    {
        public int MaxLevel { get; set; }
        public float ExpMultiplier { get; set; }
        public int StartingGold { get; set; }
        public int InventorySize { get; set; }
    }
}