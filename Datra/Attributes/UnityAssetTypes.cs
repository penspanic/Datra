namespace Datra.Attributes
{
    /// <summary>
    /// Predefined Unity asset type constants for use with AssetTypeAttribute.
    /// </summary>
    public static class UnityAssetTypes
    {
        // Basic Unity Types
        public const string GameObject = "Unity.GameObject";
        public const string ScriptableObject = "Unity.ScriptableObject";
        
        // Texture Types
        public const string Texture2D = "Unity.Texture2D";
        public const string Texture3D = "Unity.Texture3D";
        public const string Sprite = "Unity.Sprite";
        public const string RenderTexture = "Unity.RenderTexture";
        
        // Audio Types
        public const string AudioClip = "Unity.AudioClip";
        public const string AudioMixer = "Unity.AudioMixer";
        
        // Animation Types
        public const string AnimationClip = "Unity.AnimationClip";
        public const string AnimatorController = "Unity.AnimatorController";
        public const string Avatar = "Unity.Avatar";
        
        // Material and Shader Types
        public const string Material = "Unity.Material";
        public const string Shader = "Unity.Shader";
        public const string ShaderGraph = "Unity.ShaderGraph";
        
        // UI Types
        public const string Font = "Unity.Font";
        public const string TMP_FontAsset = "Unity.TMP_FontAsset";
        
        // Other Common Types
        public const string Mesh = "Unity.Mesh";
        public const string PhysicMaterial = "Unity.PhysicMaterial";
        public const string PhysicsMaterial2D = "Unity.PhysicsMaterial2D";
        public const string ComputeShader = "Unity.ComputeShader";
        public const string VideoClip = "Unity.VideoClip";
        
        // Scene and Prefab
        public const string Scene = "Unity.Scene";
        public const string Prefab = "Unity.Prefab"; // Alias for GameObject in prefab context
    }
}