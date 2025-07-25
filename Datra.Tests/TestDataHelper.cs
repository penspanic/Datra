using System;
using System.IO;
using Datra.Configuration;
using Datra.Serializers;
using Datra.Tests.Models;

namespace Datra.Tests
{
    public static class TestDataHelper
    {
        /// <summary>
        /// Finds the Resources directory by searching up the directory tree
        /// </summary>
        public static string FindDataPath()
        {
            var currentDir = Directory.GetCurrentDirectory();
            
            while (currentDir != null)
            {
                var resourcesPath = Path.Combine(currentDir, "Resources");
                if (Directory.Exists(resourcesPath))
                {
                    return resourcesPath;
                }
                
                // Find Resources folder in Datra.Tests project directory
                var testProjectPath = Path.Combine(currentDir, "Datra.Tests", "Resources");
                if (Directory.Exists(testProjectPath))
                {
                    return testProjectPath;
                }
                
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }
            
            throw new DirectoryNotFoundException("Could not find Resources directory");
        }

        /// <summary>
        /// Creates a GameDataContext with the Resources folder data
        /// </summary>
        public static GameDataContext CreateGameDataContext()
        {
            var basePath = FindDataPath();
            var rawDataProvider = new TestRawDataProvider(basePath);
            var loaderFactory = new DataSerializerFactory();
            return new GameDataContext(rawDataProvider, loaderFactory);
        }
    }
}