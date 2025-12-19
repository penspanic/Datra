using System;
using System.Collections.Generic;
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

        public async Task<Dictionary<string, string>> LoadMultipleTextAsync(string folderPath, string pattern = "*.json")
        {
            var fullPath = Path.Combine(_basePath, folderPath);
            var result = new Dictionary<string, string>();

            if (!Directory.Exists(fullPath))
            {
                return result;
            }

            var files = Directory.GetFiles(fullPath, pattern, SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var relativePath = Path.Combine(folderPath, Path.GetFileName(file));
                var content = await File.ReadAllTextAsync(file);
                result[file] = content;
            }

            return result;
        }
    }
}