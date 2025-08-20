using System;
using System.IO;
using System.Threading.Tasks;
using Datra.Interfaces;

namespace Datra.Tests
{
    public class TestRawDataProvider : IRawDataProvider
    {
        private readonly string _basePath;
        
        public TestRawDataProvider(string basePath)
        {
            _basePath = basePath;
        }
        
        public async Task<string> LoadTextAsync(string path)
        {
            var fullPath = Path.Combine(_basePath, path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Data file not found: {fullPath}");
            }
            
            return await File.ReadAllTextAsync(fullPath);
        }
        
        public async Task SaveTextAsync(string path, string content)
        {
            var fullPath = Path.Combine(_basePath, path);
            var directory = Path.GetDirectoryName(fullPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await File.WriteAllTextAsync(fullPath, content);
        }
        
        public bool Exists(string path)
        {
            var fullPath = Path.Combine(_basePath, path);
            return File.Exists(fullPath);
        }
        
        public string ResolveFilePath(string path)
        {
            return Path.GetFullPath(Path.Combine(_basePath, path));
        }
    }
}