#if ENABLE_ADDRESSABLES
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datra.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Datra.Unity.Addressables.Runtime.Providers
{
    /// <summary>
    /// Addressables-based data provider for Unity runtime.
    /// Supports both single-file and multi-file (label-based) loading.
    /// </summary>
    public class AddressableRawDataProvider : IRawDataProvider
    {
        private readonly string _basePath;

        public AddressableRawDataProvider(string basePath = null)
        {
            _basePath = basePath;
        }

        public async Task<string> LoadTextAsync(string path)
        {
            path = CombinePath(_basePath, path);
            var handle = global::UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<TextAsset>(path);
            var textAsset = await handle.Task;

            if (textAsset == null)
            {
                global::UnityEngine.AddressableAssets.Addressables.Release(handle);
                throw new System.IO.FileNotFoundException($"Text asset not found at address: {path}");
            }

            var text = textAsset.text;
            global::UnityEngine.AddressableAssets.Addressables.Release(handle);
            return text;
        }

        /// <summary>
        /// Load multiple text files by Addressables label.
        /// The folderPathOrLabel parameter is treated as an Addressables label.
        /// The pattern parameter is ignored for Addressables (use labels instead).
        /// </summary>
        public async Task<Dictionary<string, string>> LoadMultipleTextAsync(string folderPathOrLabel, string pattern = "*.json")
        {
            var result = new Dictionary<string, string>();

            // In Addressables, folderPathOrLabel is treated as a label
            var label = folderPathOrLabel;
            if (!string.IsNullOrEmpty(_basePath))
            {
                // If basePath is set, prepend it (though typically labels don't use paths)
                label = CombinePath(_basePath, folderPathOrLabel);
            }

            // Load all assets with the specified label
            var handle = global::UnityEngine.AddressableAssets.Addressables.LoadAssetsAsync<TextAsset>(
                label,
                null // No callback per asset
            );

            var textAssets = await handle.Task;

            if (textAssets != null)
            {
                foreach (var textAsset in textAssets)
                {
                    if (textAsset != null)
                    {
                        // Use asset name as key
                        result[textAsset.name] = textAsset.text;
                    }
                }
            }

            global::UnityEngine.AddressableAssets.Addressables.Release(handle);
            return result;
        }

        public Task SaveTextAsync(string path, string content)
        {
            throw new System.NotImplementedException("Addressables does not support runtime save operations. Use AssetDatabaseRawDataProvider in editor for saving.");
        }

        public bool Exists(string path)
        {
            // Check if the address exists by attempting to load its location
            path = CombinePath(_basePath, path);
            var handle = global::UnityEngine.AddressableAssets.Addressables.LoadResourceLocationsAsync(path);
            var result = handle.WaitForCompletion();
            var exists = result != null && result.Count > 0;
            global::UnityEngine.AddressableAssets.Addressables.Release(handle);
            return exists;
        }

        public string ResolveFilePath(string path)
        {
            path = CombinePath(_basePath, path);
            // In Addressables, we return a virtual path since there's no real file system path at runtime
            return $"Addressables/{path}";
        }

        private string CombinePath(string basePath, string path)
        {
            if (string.IsNullOrEmpty(basePath))
                return path;

            if (string.IsNullOrEmpty(path))
                return basePath;

            // Remove trailing slash from basePath if present
            if (basePath.EndsWith("/"))
                basePath = basePath.Substring(0, basePath.Length - 1);

            // Remove leading slash from path if present
            if (path.StartsWith("/"))
                path = path.Substring(1);

            // Combine with forward slash
            return basePath + "/" + path;
        }
    }
}
#endif