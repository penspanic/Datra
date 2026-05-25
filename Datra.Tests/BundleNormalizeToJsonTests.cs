using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Attributes;
using Datra.Bundles;
using Datra.Interfaces;
using Datra.Providers;
using Datra.SampleData.Generated;
using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
#pragma warning disable IL2026, IL3050
    public class BundleNormalizeToJsonTests
    {
        [Fact]
        public void NormalizeToJson_ConvertsYamlFiles_AndPreservesJsonFiles()
        {
            var bundle = new DatraRawBundle
            {
                Files =
                {
                    ["Units.yaml"] = "- Id: a\n  Hp: 10\n- Id: b\n  Hp: 20\n",
                    ["Item.json"]  = "[{\"Id\":\"x\",\"Price\":1}]",
                },
            };

            var normalized = DatraBundleBuilder.NormalizeToJson(bundle);

            Assert.Equal(2, normalized.Files.Count);
            Assert.True(normalized.Files["Units.yaml"].TrimStart().StartsWith("["),
                $"Expected JSON array, got: {normalized.Files["Units.yaml"]}");
            Assert.Contains("\"Id\"", normalized.Files["Units.yaml"]);
            // Original JSON pass-through verbatim.
            Assert.Equal("[{\"Id\":\"x\",\"Price\":1}]", normalized.Files["Item.json"]);
            // YAML path got a format override; JSON path did not.
            Assert.Equal(DataFormat.Json, normalized.FormatOverrides["Units.yaml"]);
            Assert.False(normalized.FormatOverrides.ContainsKey("Item.json"));
        }

        [Fact]
        public async Task BundledRawDataProvider_DispatchesJsonSerializer_ForNormalizedYamlPath()
        {
            // End-to-end: build bundle from the real Datra.SampleData Resources, normalize, load context.
            // If the format-override plumbing is wrong the YAML serializer would fail to parse JSON.
            var basePath = TestDataHelper.FindDataPath();
            var raw = DatraBundleBuilder.FromDirectory(basePath, patterns: new[] { "*.yaml", "*.yml" });

            var normalized = DatraBundleBuilder.NormalizeToJson(raw);

            // Sanity: every YAML file has a format override pointing at Json.
            Assert.NotEmpty(normalized.Files);
            foreach (var path in normalized.Files.Keys)
            {
                Assert.True(normalized.FormatOverrides.ContainsKey(path),
                    $"Missing format override for {path}");
                Assert.Equal(DataFormat.Json, normalized.FormatOverrides[path]);
            }

            // Provider exposes the format hint.
            var provider = new BundledRawDataProvider(normalized);
            Assert.IsAssignableFrom<IFormatAwareRawDataProvider>(provider);
            foreach (var path in normalized.Files.Keys)
            {
                Assert.Equal(DataFormat.Json, ((IFormatAwareRawDataProvider)provider).GetFormat(path));
            }

            await Task.CompletedTask;
        }
    }
#pragma warning restore IL2026, IL3050
}
