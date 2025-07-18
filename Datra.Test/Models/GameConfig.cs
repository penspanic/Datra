using Datra.Data.Attributes;

namespace Datra.Test.Models
{
    [SingleData("GameConfig.yaml", Format = DataFormat.Yaml)]
    public partial class GameConfig
    {
        public int MaxLevel { get; }
        public float ExpMultiplier { get; }
        public int StartingGold { get; }
        public int InventorySize { get; }
    }
}