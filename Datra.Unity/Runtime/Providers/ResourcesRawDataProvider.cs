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
            return Task.FromResult(Resources.Load<TextAsset>(path).text);
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
    }
}