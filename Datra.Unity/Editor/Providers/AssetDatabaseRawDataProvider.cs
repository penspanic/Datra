using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Editor.Utilities;
using Datra.Interfaces;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Datra.Unity.Editor.Providers
{
    public class AssetDatabaseRawDataProvider : IRawDataProvider
    {
        private readonly string _basePath;

        public AssetDatabaseRawDataProvider(string basePath = null)
        {
            _basePath = basePath;
            // AssetDatabaseRawDataProvider does not require a base path
            // but you can set it if needed for consistency with other providers
        }

        public Task<string> LoadTextAsync(string path)
        {
#if UNITY_EDITOR
            var originalPath = path;
            path = PathHelper.CombinePath(_basePath, path);
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (textAsset != null)
            {
                return Task.FromResult(textAsset.text);
            }

            // Try loading as raw text file if not a TextAsset
            if (File.Exists(path))
            {
                return Task.FromResult(File.ReadAllText(path));
            }

            Debug.LogWarning($"[AssetDatabaseRawDataProvider] LoadTextAsync failed: originalPath={originalPath}, combinedPath={path}");
            throw new FileNotFoundException($"Could not find asset at path: {path}");
#else
            throw new System.NotSupportedException("AssetDatabaseDataProvider is only available in the Unity Editor");
#endif
        }

        public Task SaveTextAsync(string path, string content)
        {
#if UNITY_EDITOR
            // CombinePath handles absolute paths correctly (returns them as-is)
            path = PathHelper.CombinePath(_basePath, path);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write the file
            File.WriteAllText(path, content);

            // Import the asset to make it available in AssetDatabase
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();

            return Task.CompletedTask;
#else
            throw new System.NotSupportedException("AssetDatabaseDataProvider is only available in the Unity Editor");
#endif
        }

        public bool Exists(string path)
        {
#if UNITY_EDITOR
            // Check if it exists as an asset
            path = PathHelper.CombinePath(_basePath, path);
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset != null)
            {
                return true;
            }

            // Check if it exists as a file
            return File.Exists(path);
#else
            throw new System.NotSupportedException("AssetDatabaseDataProvider is only available in the Unity Editor");
#endif
        }

        public string ResolveFilePath(string path)
        {
#if UNITY_EDITOR
            // CombinePath handles absolute paths correctly (returns them as-is)
            path = PathHelper.CombinePath(_basePath, path);
            // Convert to absolute path if it starts with Assets/
            if (path.StartsWith("Assets/"))
            {
                return Path.GetFullPath(path);
            }
            return path;
#else
            throw new System.NotSupportedException("AssetDatabaseDataProvider is only available in the Unity Editor");
#endif
        }

        /// <summary>
        /// Load multiple text files from a directory.
        /// </summary>
        /// <param name="folderPath">Relative folder path from base path</param>
        /// <param name="pattern">Search pattern (e.g., "*.json")</param>
        /// <returns>Dictionary where key is file name (relative to folderPath), value is content</returns>
        public Task<Dictionary<string, string>> LoadMultipleTextAsync(string folderPath, string pattern = "*.json")
        {
#if UNITY_EDITOR
            var result = new Dictionary<string, string>();
            var fullPath = PathHelper.CombinePath(_basePath, folderPath);

            // Get absolute path for directory operations
            var absolutePath = fullPath.StartsWith("Assets/")
                ? Path.GetFullPath(fullPath)
                : fullPath;

            if (!Directory.Exists(absolutePath))
            {
                Debug.LogWarning($"[AssetDatabaseRawDataProvider] Directory does not exist: {absolutePath}");
                return Task.FromResult(result);
            }

            var files = Directory.GetFiles(absolutePath, pattern, SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                // Get file name for dictionary key (relative to folderPath)
                var fileName = Path.GetFileName(file);

                // Convert to Unity asset path format for loading
                var unityPath = file.Replace("\\", "/");
                if (unityPath.Contains("/Assets/"))
                {
                    var assetsIndex = unityPath.IndexOf("/Assets/");
                    unityPath = unityPath.Substring(assetsIndex + 1);
                }

                var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(unityPath);
                if (textAsset != null)
                {
                    result[fileName] = textAsset.text;
                }
                else if (File.Exists(file))
                {
                    result[fileName] = File.ReadAllText(file);
                }
            }

            return Task.FromResult(result);
#else
            throw new System.NotSupportedException("AssetDatabaseDataProvider is only available in the Unity Editor");
#endif
        }

        /// <summary>
        /// List files in a directory without loading their contents.
        /// </summary>
        /// <param name="folderPath">Relative folder path from base path</param>
        /// <param name="pattern">Search pattern (e.g., "*.json")</param>
        /// <returns>List of file names (relative to folderPath, not including folderPath)</returns>
        public Task<IReadOnlyList<string>> ListFilesAsync(string folderPath, string pattern = "*.json")
        {
#if UNITY_EDITOR
            var fullPath = PathHelper.CombinePath(_basePath, folderPath);

            // Get absolute path for directory operations
            var absolutePath = fullPath.StartsWith("Assets/")
                ? Path.GetFullPath(fullPath)
                : fullPath;

            if (!Directory.Exists(absolutePath))
            {
                return Task.FromResult<IReadOnlyList<string>>(new List<string>());
            }

            // Return file names only (relative to folderPath)
            var files = Directory.GetFiles(absolutePath, pattern, SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(files);
#else
            throw new System.NotSupportedException("AssetDatabaseDataProvider is only available in the Unity Editor");
#endif
        }

        public Task<bool> DeleteAsync(string path)
        {
#if UNITY_EDITOR
            path = PathHelper.CombinePath(_basePath, path);

            // Try to delete via AssetDatabase first (handles .meta files automatically)
            if (AssetDatabase.DeleteAsset(path))
            {
                return Task.FromResult(true);
            }

            // Fallback to file system deletion if not in AssetDatabase
            var absolutePath = path.StartsWith("Assets/")
                ? Path.GetFullPath(path)
                : path;

            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);

                // Also delete .meta file if it exists
                var metaPath = absolutePath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }

                AssetDatabase.Refresh();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
#else
            throw new System.NotSupportedException("AssetDatabaseDataProvider is only available in the Unity Editor");
#endif
        }
    }
}
