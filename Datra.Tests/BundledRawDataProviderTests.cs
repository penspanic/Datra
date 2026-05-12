using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Bundles;
using Datra.Providers;
using Datra.SampleData.Generated;
using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
    public class BundledRawDataProviderTests : IDisposable
    {
        private readonly string _testDirectory;

        public BundledRawDataProviderTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "DatraBundleTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }

        [Fact]
        public void FromDirectory_IncludesDatraTextFormatsAndComputesStableHash()
        {
            Write("Items.yaml", "- Id: sword\n  Name: Sword\n");
            Write("Config.json", "{\"MaxLevel\":10}");
            Write("Tables/Rows.csv", "Id,Name\n1,A\n");
            Write("Notes.txt", "ignored");

            var first = DatraBundleBuilder.FromDirectory(_testDirectory);
            var second = DatraBundleBuilder.FromDirectory(_testDirectory);

            Assert.Equal(DatraRawBundle.CurrentSchemaVersion, first.SchemaVersion);
            Assert.Equal(first.ContentHash, second.ContentHash);
            Assert.Equal(64, first.ContentHash.Length);
            Assert.True(first.Files.ContainsKey("Items.yaml"));
            Assert.True(first.Files.ContainsKey("Config.json"));
            Assert.True(first.Files.ContainsKey("Tables/Rows.csv"));
            Assert.False(first.Files.ContainsKey("Notes.txt"));
        }

        [Fact]
        public async Task Provider_LoadsSingleFilesFromBundle()
        {
            var bundle = new DatraRawBundle
            {
                Files = new Dictionary<string, string>
                {
                    ["Units.yaml"] = "- Id: unit.sprinkler\n",
                    ["nested/GameRules.json"] = "{\"TickRate\":10}",
                },
            };

            var provider = new BundledRawDataProvider(bundle);

            Assert.True(provider.Exists("Units.yaml"));
            Assert.True(provider.Exists("nested\\GameRules.json"));
            Assert.Equal("- Id: unit.sprinkler\n", await provider.LoadTextAsync("/Units.yaml"));
            Assert.Equal("bundle://nested/GameRules.json", provider.ResolveFilePath("nested/GameRules.json"));
        }

        [Fact]
        public async Task Provider_LoadMultipleTextAsync_ReturnsDirectChildrenOnly()
        {
            var provider = new BundledRawDataProvider(new DatraRawBundle
            {
                Files = new Dictionary<string, string>
                {
                    ["units/sprinkler.json"] = "{\"Id\":\"sprinkler\"}",
                    ["units/cannon.json"] = "{\"Id\":\"cannon\"}",
                    ["units/readme.txt"] = "ignored",
                    ["units/nested/trailer.json"] = "{\"Id\":\"trailer\"}",
                },
            });

            var files = await provider.LoadMultipleTextAsync("units", "*.json");
            var listed = await provider.ListFilesAsync("units", "*.json");

            Assert.Equal(new[] { "cannon.json", "sprinkler.json" }, files.Keys.OrderBy(k => k).ToArray());
            Assert.Equal(new[] { "cannon.json", "sprinkler.json" }, listed);
        }

        [Fact]
        public async Task Provider_IsReadOnlyAndReportsMissingFiles()
        {
            var provider = new BundledRawDataProvider(new DatraRawBundle());

            await Assert.ThrowsAsync<FileNotFoundException>(() => provider.LoadTextAsync("missing.yaml"));
            await Assert.ThrowsAsync<NotSupportedException>(() => provider.SaveTextAsync("x.yaml", ""));
            await Assert.ThrowsAsync<NotSupportedException>(() => provider.DeleteAsync("x.yaml"));
        }

        [Fact]
        public async Task GeneratedDataContext_LoadsSampleDataFromSingleBundle()
        {
            var bundle = DatraBundleBuilder.FromDirectory(TestDataHelper.FindDataPath());
            var provider = new BundledRawDataProvider(bundle);
            var context = new GameDataContext(provider, new DataSerializerFactory());

            await context.LoadAllAsync();

            Assert.NotEmpty(context.Item.LoadedItems);
            Assert.NotNull(context.Item.TryGetLoaded(1001));
            Assert.NotNull(context.GameConfig.Current);
            Assert.True(context.GameConfig.Current!.MaxLevel > 0);
        }

        [Theory]
        [InlineData("")]
        [InlineData("../Items.yaml")]
        [InlineData("safe/../../Items.yaml")]
        public void NormalizePath_RejectsUnsafePaths(string path)
        {
            Assert.Throws<ArgumentException>(() => DatraBundleBuilder.NormalizePath(path));
        }

        private void Write(string relativePath, string contents)
        {
            var fullPath = Path.Combine(_testDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);
        }
    }
}
