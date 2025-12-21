using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Specifies a folder path constraint for asset selection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class FolderPathAttribute : Attribute
    {
        /// <summary>
        /// The folder path to restrict asset selection. Example: "Assets/Prefabs/Characters/"
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Whether to include subfolders in the search
        /// </summary>
        public bool IncludeSubfolders { get; set; } = true;

        /// <summary>
        /// Optional: Search pattern for file names (e.g., "*.prefab", "Player_*")
        /// </summary>
        public string SearchPattern { get; set; } = "*";

        public FolderPathAttribute(string path)
        {
            Path = path;
        }
    }
}