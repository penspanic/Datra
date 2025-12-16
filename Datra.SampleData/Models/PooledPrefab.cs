using Datra.Attributes;

namespace Datra.SampleData.Models
{
    public struct PooledPrefab
    {
        [AssetType(UnityAssetTypes.GameObject)] [FolderPath("Assets/04.Prefabs/Skills/")]
        public string Path;

        public int InitialCount;
        public int MaxCount;

        public override string ToString()
        {
            return $"{Path} {InitialCount}/{MaxCount}";
        }

        public bool IsValid => !string.IsNullOrEmpty(Path);
    }
}