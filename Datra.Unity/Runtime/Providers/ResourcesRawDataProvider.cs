using System.Threading.Tasks;
using Datra.Interfaces;
using UnityEngine;

namespace Datra.Unity.Runtime.Providers
{
    public class ResourcesRawDataProvider : IRawDataProvider
    {
        public Task<string> LoadTextAsync(string path)
        {
            // Remove extension if it exists, as Resources.Load does not require it
            path = path.IndexOf('.') > 0 ? path.Substring(0, path.LastIndexOf('.')) : path;
            var textAsset = Resources.Load<TextAsset>(path);
            if (textAsset == null)
                throw new System.IO.FileNotFoundException($"Text asset not found at path: {path}");

            return Task.FromResult(textAsset.text);
        }

        public Task SaveTextAsync(string path, string content)
        {
            throw new System.NotImplementedException();
        }

        public bool Exists(string path)
        {
            // Remove extension if it exists, as Resources.Load does not require it
            path = path.IndexOf('.') > 0 ? path.Substring(0, path.LastIndexOf('.')) : path;
            var textAsset = Resources.Load<TextAsset>(path);
            return textAsset != null;
        }
        
        public string ResolveFilePath(string path)
        {
            // In Resources, we return a virtual path since there's no real file system path at runtime
            return $"Resources/{path}";
        }
    }
}