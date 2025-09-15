#if ENABLE_ADDRESSABLES
using System.IO;
using System.Threading.Tasks;
using Datra.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Datra.Unity.Addressables.Runtime.Providers
{
    public class AddressableRawDataProvider : IRawDataProvider
    {
        private readonly string _basePath;

        public AddressableRawDataProvider(string basePath = null)
        {
            _basePath = basePath;
        }

        public async Task<string> LoadTextAsync(string path)
        {
            path = string.IsNullOrEmpty(_basePath) ? path : Path.Combine(_basePath, path);
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

        public Task SaveTextAsync(string path, string content)
        {
            throw new System.NotImplementedException("Addressables does not support runtime save operations. Use AssetDatabaseRawDataProvider in editor for saving.");
        }

        public bool Exists(string path)
        {
            // Check if the address exists by attempting to load its location
            path = string.IsNullOrEmpty(_basePath) ? path : Path.Combine(_basePath, path);
            var handle = global::UnityEngine.AddressableAssets.Addressables.LoadResourceLocationsAsync(path);
            var result = handle.WaitForCompletion();
            var exists = result != null && result.Count > 0;
            global::UnityEngine.AddressableAssets.Addressables.Release(handle);
            return exists;
        }

        public string ResolveFilePath(string path)
        {
            path = string.IsNullOrEmpty(_basePath) ? path : Path.Combine(_basePath, path);
            // In Addressables, we return a virtual path since there's no real file system path at runtime
            return $"Addressables/{path}";
        }
    }
}
#endif