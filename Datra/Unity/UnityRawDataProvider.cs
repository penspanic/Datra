#if UNITY_2020_3_OR_NEWER
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Datra.Interfaces;

namespace Datra.Unity
{
    /// <summary>
    /// RawDataProvider using Unity Resources system
    /// </summary>
    public class UnityRawDataProvider : IRawDataProvider
    {
        private readonly string _dataPath;
        
        /// <summary>
        /// Specify data path within Resources folder
        /// </summary>
        /// <param name="dataPath">Relative path within Resources folder (e.g. "Data")</param>
        public UnityRawDataProvider(string dataPath = "Data")
        {
            _dataPath = dataPath;
        }
        
        public async Task<string> LoadTextAsync(string filePath)
        {
            // Remove extension (Unity Resources loads without extension)
            var resourcePath = Path.Combine(_dataPath, Path.GetFileNameWithoutExtension(filePath));
            
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                throw new FileNotFoundException($"Resource not found: {resourcePath}");
            }
            
            return await Task.FromResult(textAsset.text);
        }
        
        public async Task SaveTextAsync(string filePath, string content)
        {
            // Unity Resources is read-only, so it only works in editor
#if UNITY_EDITOR
            var fullPath = Path.Combine(Application.dataPath, "Resources", _dataPath, filePath);
            var directory = Path.GetDirectoryName(fullPath);
            
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await File.WriteAllTextAsync(fullPath, content);
            
            // Refresh editor
            UnityEditor.AssetDatabase.Refresh();
#else
            throw new NotSupportedException("Writing to Resources is not supported at runtime.");
#endif
        }
        
        public bool Exists(string filePath)
        {
            var resourcePath = Path.Combine(_dataPath, Path.GetFileNameWithoutExtension(filePath));
            return Resources.Load<TextAsset>(resourcePath) != null;
        }
    }
}
#endif