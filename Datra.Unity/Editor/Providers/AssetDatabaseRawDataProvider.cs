using System.IO;
using System.Threading.Tasks;
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
            path = string.IsNullOrEmpty(_basePath) ? path : Path.Combine(_basePath, path);
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
            
            throw new FileNotFoundException($"Could not find asset at path: {path}");
#else
            throw new System.NotSupportedException("AssetDatabaseDataProvider is only available in the Unity Editor");
#endif
        }

        public Task SaveTextAsync(string path, string content)
        {
#if UNITY_EDITOR
            // Ensure directory exists
            path = string.IsNullOrEmpty(_basePath) ? path : Path.Combine(_basePath, path);
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
    }
}