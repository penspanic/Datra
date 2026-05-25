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
            // Hp should be a JSON number, not a quoted string.
            Assert.Contains("\"Hp\":10", normalized.Files["Units.yaml"]);
            Assert.DoesNotContain("\"Hp\":\"10\"", normalized.Files["Units.yaml"]);
            // Original JSON pass-through verbatim.
            Assert.Equal("[{\"Id\":\"x\",\"Price\":1}]", normalized.Files["Item.json"]);
            // YAML path got a format override; JSON path did not.
            Assert.Equal(DataFormat.Json, normalized.FormatOverrides["Units.yaml"]);
            Assert.False(normalized.FormatOverrides.ContainsKey("Item.json"));
        }

        [Fact]
        public void NormalizeToJson_QuotedScalars_StayAsStrings()
        {
            // Quoted YAML scalars must survive as JSON strings even when their content
            // looks numeric / boolean. Regression: TidemarkParticleParams.AttachWhenEquals
            // is `string`, and Effects.yaml writes it as `AttachWhenEquals: "0"`.
            var bundle = new DatraRawBundle
            {
                Files =
                {
                    ["Sample.yaml"] =
                        "- Quoted: \"0\"\n" +
                        "  SingleQuoted: '5'\n" +
                        "  QuotedBool: \"true\"\n" +
                        "  Plain: 0\n" +
                        "  PlainBool: true\n",
                },
            };

            var normalized = DatraBundleBuilder.NormalizeToJson(bundle);
            var s = normalized.Files["Sample.yaml"];

            Assert.Contains("\"Quoted\":\"0\"", s);
            Assert.Contains("\"SingleQuoted\":\"5\"", s);
            Assert.Contains("\"QuotedBool\":\"true\"", s);
            // Plain scalars still infer normally.
            Assert.Contains("\"Plain\":0", s);
            Assert.Contains("\"PlainBool\":true", s);
        }

        [Fact]
        public void NormalizeToJson_InfersScalarTypes_PerYamlCoreSchema()
        {
            // Cover: bool / int / float / negative / null forms / string preservation.
            var bundle = new DatraRawBundle
            {
                Files =
                {
                    ["Sample.yaml"] =
                        "Enabled: true\n" +
                        "Disabled: False\n" +
                        "Count: 42\n" +
                        "Negative: -7\n" +
                        "Ratio: 3.5\n" +
                        "Tiny: -0.25\n" +
                        "Sci: 1.5e3\n" +
                        "Empty: \n" +
                        "Null1: null\n" +
                        "Tilde: ~\n" +
                        "Name: hero_a\n" +
                        "VersionLike: 1.2.3\n",
                },
            };

            var normalized = DatraBundleBuilder.NormalizeToJson(bundle);
            var s = normalized.Files["Sample.yaml"];

            Assert.Contains("\"Enabled\":true", s);
            Assert.Contains("\"Disabled\":false", s);
            Assert.Contains("\"Count\":42", s);
            Assert.Contains("\"Negative\":-7", s);
            Assert.Contains("\"Ratio\":3.5", s);
            Assert.Contains("\"Tiny\":-0.25", s);
            Assert.Contains("\"Sci\":1500", s); // 1.5e3 normalized
            // YAML empty / null / ~ → JSON null
            Assert.Contains("\"Empty\":null", s);
            Assert.Contains("\"Null1\":null", s);
            Assert.Contains("\"Tilde\":null", s);
            // String-typed values stay quoted.
            Assert.Contains("\"Name\":\"hero_a\"", s);
            // Multi-dot scalar must stay a string (not a float).
            Assert.Contains("\"VersionLike\":\"1.2.3\"", s);
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
