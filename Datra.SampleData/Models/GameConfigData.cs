using Datra.Attributes;
using Datra.DataTypes;

namespace Datra.SampleData.Models
{
    public enum GameMode
    {
        Easy,
        Normal,
        Hard,
        Expert
    }

    public enum RewardType
    {
        Gold,
        Experience,
        Item,
        Skill,
        Achievement
    }

    [SingleData("GameConfig.json", Format = DataFormat.Json)]
    public partial class GameConfigData
    {
        public string GameName { get; set; }
        public int MaxLevel { get; set; }
        public float ExpMultiplier { get; set; }
        public GameMode DefaultMode { get; set; }
        public GameMode[] AvailableModes { get; set; }
        public RewardType[] EnabledRewards { get; set; }
        public StringDataRef<CharacterData> DefaultCharacter { get; set; }
        public IntDataRef<ItemData> StartingItem { get; set; }
        public StringDataRef<CharacterData>[] UnlockableCharacters { get; set; }
        public IntDataRef<ItemData>[] StartingItems { get; set; }
    }
}