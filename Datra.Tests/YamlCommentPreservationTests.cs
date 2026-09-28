#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datra.Editor.DataSources;
using Datra.Interfaces;
using Datra.Repositories;
using Datra.SampleData.Generated;
using Datra.SampleData.Models;
using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
    /// <summary>
    /// Saving a YAML table from the editor must keep the comments a designer wrote in it.
    /// Every test goes through the path the Unity Data Editor uses:
    /// EditableKeyValueDataSource / EditableSingleDataSource -> repository.SaveAsync -> IRawDataProvider.
    /// </summary>
    public class YamlCommentPreservationTests
    {
        #region Fixtures

        public class UpgradeRow : ITableData<string>
        {
            public string Id { get; set; } = string.Empty;
            public string? Name { get; set; } = string.Empty;
            public double Base { get; set; }
            public List<double> Levels { get; set; } = new List<double>();
            public int FirstCost { get; set; }
            public string RequiresUpgrade { get; set; } = string.Empty;
            public bool Implemented { get; set; }
        }

        public class EconomyConfig
        {
            public int StartingDeposit { get; set; }
            public double GoldenBottleChance { get; set; }
            public List<string> CenterCells { get; set; } = new List<string>();
            public List<Target> Targets { get; set; } = new List<Target>();
        }

        public class Target
        {
            public int Run { get; set; }
            public int Deposit { get; set; }
        }

        private const string UpgradesYaml =
            "# Upgrades: what the player can buy between runs.\n" +
            "# Costs grow by CostGrowth each level.\n" +
            "\n" +
            "# The first thing a new player buys.\n" +
            "- Id: push-strength\n" +
            "  Name: Push strength\n" +
            "  Base: 0.5          # metres per second at level 0\n" +
            "  Levels: [0.9, 1.3, 1.6]\n" +
            "  FirstCost: 50\n" +
            "  RequiresUpgrade: \"\"\n" +
            "  Implemented: true\n" +
            "\n" +
            "# --- Line upgrades ---\n" +
            "\n" +
            "- Id: bottle-count\n" +
            "  Name: Longer line\n" +
            "  Base: 3\n" +
            "  # one bottle per level, capped by the stage length\n" +
            "  Levels:\n" +
            "    - 4   # fits the short stage\n" +
            "    - 5\n" +
            "    - 6   # needs the long stage\n" +
            "  FirstCost: 80\n" +
            "  RequiresUpgrade: push-strength\n" +
            "  Implemented: true\n" +
            "\n" +
            "- Id: domes\n" +
            "  Name: Balance dome\n" +
            "  Base: 3\n" +
            "  Levels: [4, 5]\n" +
            "  FirstCost: 150   # tuned in playtest 3\n" +
            "  RequiresUpgrade: \"\"\n" +
            "# end of table\n";

        private const string EconomyYaml =
            "# Economy knobs. Money is in coins.\n" +
            "StartingDeposit: 0        # nothing in the jar on run 1\n" +
            "GoldenBottleChance: 0.02\n" +
            "CenterCells: [B2, B3]\n" +
            "\n" +
            "# Deposit goals per run\n" +
            "Targets:\n" +
            "  - Run: 1\n" +
            "    Deposit: 60\n" +
            "  # the first real wall\n" +
            "  - Run: 5\n" +
            "    Deposit: 250   # playtest 2\n" +
            "  - Run: 10\n" +
            "    Deposit: 600\n";

        private sealed class MemoryRawDataProvider : IRawDataProvider
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();

            public Task<string> LoadTextAsync(string path)
                => Files.TryGetValue(path, out var text)
                    ? Task.FromResult(text)
                    : throw new FileNotFoundException(path);

            public Task SaveTextAsync(string path, string content)
            {
                Files[path] = content;
                return Task.CompletedTask;
            }

            public bool Exists(string path) => Files.ContainsKey(path);

            public string ResolveFilePath(string path) => path;
        }

        private static async Task<(EditableKeyValueDataSource<string, UpgradeRow> source, MemoryRawDataProvider provider, KeyValueDataRepository<string, UpgradeRow> repository)>
            OpenTableAsync(string yaml, string path = "Upgrades.yaml")
        {
            var provider = new MemoryRawDataProvider();
            provider.Files[path] = yaml;
            var repository = new KeyValueDataRepository<string, UpgradeRow>(
                path,
                provider,
                new DataSerializerFactory(),
                (data, serializer) => serializer.DeserializeTable<string, UpgradeRow>(data),
                (table, serializer) => serializer.SerializeTable(table));
            await repository.InitializeAsync();
            var source = new EditableKeyValueDataSource<string, UpgradeRow>(repository);
            return (source, provider, repository);
        }

        private static async Task<string> SaveAsync(EditableKeyValueDataSource<string, UpgradeRow> source, MemoryRawDataProvider provider, string path = "Upgrades.yaml")
        {
            await source.SaveAsync();
            return provider.Files[path];
        }

        private static Dictionary<string, UpgradeRow> Parse(string yaml)
            => new YamlDataSerializer().DeserializeTable<string, UpgradeRow>(yaml);

        private static string Replace(string text, string oldValue, string newValue)
        {
            Assert.Contains(oldValue, text);
            Assert.Equal(1, CountOf(text, oldValue));
            return text.Replace(oldValue, newValue);
        }

        private static int CountOf(string text, string value)
        {
            var count = 0;
            for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + 1, StringComparison.Ordinal))
                count++;
            return count;
        }

        #endregion

        #region Changing values

        [Fact]
        public async Task ChangingOneValue_KeepsEveryCommentAndEveryOtherByte()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.TrackPropertyChange("domes", nameof(UpgradeRow.FirstCost), 175, out _);
            var saved = await SaveAsync(source, provider);

            Assert.Equal(Replace(UpgradesYaml, "FirstCost: 150   #", "FirstCost: 175   #"), saved);
        }

        [Fact]
        public async Task ChangingValueWithTrailingComment_KeepsTheComment()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.TrackPropertyChange("push-strength", nameof(UpgradeRow.Base), 0.75, out _);
            var saved = await SaveAsync(source, provider);

            Assert.Equal(Replace(UpgradesYaml, "Base: 0.5          #", "Base: 0.75          #"), saved);
        }

        [Fact]
        public async Task ChangingOneEntryOfFlowList_KeepsTheListFlow()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.TrackPropertyChange("push-strength", nameof(UpgradeRow.Levels), new List<double> { 0.9, 1.4, 1.6 }, out _);
            var saved = await SaveAsync(source, provider);

            Assert.Equal(Replace(UpgradesYaml, "Levels: [0.9, 1.3, 1.6]", "Levels: [0.9, 1.4, 1.6]"), saved);
        }

        [Fact]
        public async Task ChangingOneEntryOfBlockList_KeepsCommentsInsideTheList()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.TrackPropertyChange("bottle-count", nameof(UpgradeRow.Levels), new List<double> { 4, 5, 7 }, out _);
            var saved = await SaveAsync(source, provider);

            Assert.Equal(Replace(UpgradesYaml, "    - 6   # needs", "    - 7   # needs"), saved);
        }

        [Fact]
        public async Task AppendingToBlockList_AddsALineInTheListsStyle()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.TrackPropertyChange("bottle-count", nameof(UpgradeRow.Levels), new List<double> { 4, 5, 6, 8 }, out _);
            var saved = await SaveAsync(source, provider);

            Assert.Equal(Replace(UpgradesYaml, "    - 6   # needs the long stage\n", "    - 6   # needs the long stage\n    - 8\n"), saved);
        }

        [Fact]
        public async Task RemovingFromBlockList_TakesTheEntrysCommentWithIt()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.TrackPropertyChange("bottle-count", nameof(UpgradeRow.Levels), new List<double> { 5, 6 }, out _);
            var saved = await SaveAsync(source, provider);

            Assert.Equal(Replace(UpgradesYaml, "    - 4   # fits the short stage\n", ""), saved);
        }

        [Fact]
        public async Task ChangingQuotedString_KeepsTheQuotes()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.TrackPropertyChange("domes", nameof(UpgradeRow.RequiresUpgrade), "push-strength", out _);
            var saved = await SaveAsync(source, provider);

            var expected = UpgradesYaml.Replace(
                "  FirstCost: 150   # tuned in playtest 3\n  RequiresUpgrade: \"\"\n",
                "  FirstCost: 150   # tuned in playtest 3\n  RequiresUpgrade: \"push-strength\"\n");
            Assert.NotEqual(UpgradesYaml, expected);
            Assert.Equal(expected, saved);
        }

        [Fact]
        public async Task SettingAFieldTheFileLeftOut_InsertsItAfterItsNeighbour()
        {
            // domes has no "Implemented" line; it was false by default.
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.TrackPropertyChange("domes", nameof(UpgradeRow.Implemented), true, out _);
            var saved = await SaveAsync(source, provider);

            var expected = UpgradesYaml.Replace(
                "  FirstCost: 150   # tuned in playtest 3\n  RequiresUpgrade: \"\"\n",
                "  FirstCost: 150   # tuned in playtest 3\n  RequiresUpgrade: \"\"\n  Implemented: true\n");
            Assert.Equal(expected, saved);
        }

        [Fact]
        public async Task ClearingAField_RemovesItsLineAndTheCommentAboveIt()
        {
            // Null is omitted on save, so the field's line (and the comment directly above it) goes.
            var yaml = UpgradesYaml.Replace("  Name: Balance dome\n", "  # shown in the shop\n  Name: Balance dome\n");
            var (source, provider, _) = await OpenTableAsync(yaml);

            source.TrackPropertyChange("domes", nameof(UpgradeRow.Name), null, out _);
            var saved = await SaveAsync(source, provider);

            Assert.Equal(Replace(yaml, "  # shown in the shop\n  Name: Balance dome\n", ""), saved);
        }

        [Fact]
        public async Task SaveWithoutChanges_LeavesTheFileByteIdentical()
        {
            var (source, provider, repository) = await OpenTableAsync(UpgradesYaml);

            // Force-save path: the repository rewrites the file even though nothing changed.
            await repository.SaveAsync();
            Assert.Equal(UpgradesYaml, provider.Files["Upgrades.yaml"]);

            await source.SaveAsync();
            Assert.Equal(UpgradesYaml, provider.Files["Upgrades.yaml"]);
        }

        [Fact]
        public async Task SavingTwice_KeepsComments()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.TrackPropertyChange("domes", nameof(UpgradeRow.FirstCost), 175, out _);
            await source.SaveAsync();
            source.TrackPropertyChange("push-strength", nameof(UpgradeRow.FirstCost), 55, out _);
            var saved = await SaveAsync(source, provider);

            var expected = Replace(UpgradesYaml, "FirstCost: 150   #", "FirstCost: 175   #");
            expected = Replace(expected, "FirstCost: 50\n", "FirstCost: 55\n");
            Assert.Equal(expected, saved);
        }

        [Fact]
        public async Task CrlfFile_StaysCrlf()
        {
            var crlf = UpgradesYaml.Replace("\n", "\r\n");
            var (source, provider, _) = await OpenTableAsync(crlf);

            source.TrackPropertyChange("domes", nameof(UpgradeRow.FirstCost), 175, out _);
            var saved = await SaveAsync(source, provider);

            Assert.Equal(Replace(crlf, "FirstCost: 150   #", "FirstCost: 175   #"), saved);
        }

        #endregion

        #region Adding and deleting rows

        [Fact]
        public async Task AddingRow_AppendsItWithoutTouchingTheRest()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.Add("carpet", new UpgradeRow
            {
                Id = "carpet",
                Name = "Stair carpet",
                Base = 0,
                Levels = new List<double> { 1, 2 },
                FirstCost = 350,
            });
            var saved = await SaveAsync(source, provider);

            // Everything up to the end of the last row is untouched...
            var lastRowEnd = UpgradesYaml.IndexOf("# end of table", StringComparison.Ordinal);
            Assert.StartsWith(UpgradesYaml.Substring(0, lastRowEnd - 1), saved);
            // ...the new row follows it, separated the way the other rows are...
            var newRow = saved.Substring(lastRowEnd - 1);
            Assert.StartsWith("\n\n- Id: carpet\n  Name: Stair carpet\n", newRow);
            // ...and the footer comment is still last.
            Assert.EndsWith("\n# end of table\n", saved);

            var rows = Parse(saved);
            Assert.Equal(new[] { "push-strength", "bottle-count", "domes", "carpet" }, rows.Keys);
            Assert.Equal(new List<double> { 1, 2 }, rows["carpet"].Levels);
            Assert.Equal(350, rows["carpet"].FirstCost);
        }

        [Fact]
        public async Task DeletingRow_TakesItsOwnCommentsButKeepsTheFileHeader()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.Delete("push-strength");
            var saved = await SaveAsync(source, provider);

            var expected =
                "# Upgrades: what the player can buy between runs.\n" +
                "# Costs grow by CostGrowth each level.\n" +
                "\n" +
                UpgradesYaml.Substring(UpgradesYaml.IndexOf("# --- Line upgrades ---", StringComparison.Ordinal));
            Assert.Equal(expected, saved);
            Assert.DoesNotContain("The first thing a new player buys", saved);
        }

        [Fact]
        public async Task DeletingRow_RemovesTheCommentsInsideIt_AndKeepsTheSectionComment()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.Delete("bottle-count");
            var saved = await SaveAsync(source, provider);

            var start = UpgradesYaml.IndexOf("- Id: bottle-count", StringComparison.Ordinal);
            var end = UpgradesYaml.IndexOf("- Id: domes", StringComparison.Ordinal);
            var expected = UpgradesYaml.Remove(start, end - start);
            Assert.Equal(expected, saved);
            Assert.DoesNotContain("one bottle per level", saved);
            Assert.DoesNotContain("fits the short stage", saved);
            Assert.Contains("# --- Line upgrades ---", saved);
        }

        [Fact]
        public async Task DeletingLastRow_KeepsTheFooter()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.Delete("domes");
            var saved = await SaveAsync(source, provider);

            var start = UpgradesYaml.IndexOf("\n\n- Id: domes", StringComparison.Ordinal);
            var end = UpgradesYaml.IndexOf("\n# end of table", StringComparison.Ordinal);
            Assert.Equal(UpgradesYaml.Remove(start, end - start), saved);
        }

        [Fact]
        public async Task AddDeleteAndChangeInOneSave()
        {
            var (source, provider, _) = await OpenTableAsync(UpgradesYaml);

            source.Delete("push-strength");
            source.TrackPropertyChange("bottle-count", nameof(UpgradeRow.Base), 4.0, out _);
            source.Add("mat", new UpgradeRow { Id = "mat", Name = "Rubber mat", FirstCost = 90 });
            var saved = await SaveAsync(source, provider);

            Assert.Contains("# Upgrades: what the player can buy between runs.", saved);
            Assert.Contains("# --- Line upgrades ---", saved);
            Assert.Contains("  # one bottle per level, capped by the stage length\n", saved);
            Assert.Contains("    - 4   # fits the short stage\n", saved);
            Assert.Contains("  Base: 4\n  # one bottle per level", saved);
            Assert.Contains("  FirstCost: 150   # tuned in playtest 3\n", saved);
            Assert.DoesNotContain("The first thing a new player buys", saved);

            var rows = Parse(saved);
            Assert.Equal(new[] { "bottle-count", "domes", "mat" }, rows.Keys);
            Assert.Equal(4, rows["bottle-count"].Base);
        }

        #endregion

        #region Single data

        [Fact]
        public async Task SingleData_ChangingValues_KeepsComments()
        {
            var provider = new MemoryRawDataProvider();
            provider.Files["Economy.yaml"] = EconomyYaml;
            var repository = new SingleDataRepository<EconomyConfig>(
                "Economy.yaml",
                provider,
                new DataSerializerFactory(),
                (data, serializer) => serializer.DeserializeSingle<EconomyConfig>(data),
                (data, serializer) => serializer.SerializeSingle(data));
            await repository.InitializeAsync();
            var source = new EditableSingleDataSource<EconomyConfig>(repository);

            source.TrackPropertyChange(EditableSingleDataSource<EconomyConfig>.SingleKey, nameof(EconomyConfig.GoldenBottleChance), 0.03, out _);
            source.TrackPropertyChange(EditableSingleDataSource<EconomyConfig>.SingleKey, nameof(EconomyConfig.Targets), new List<Target>
            {
                new Target { Run = 1, Deposit = 60 },
                new Target { Run = 5, Deposit = 300 },
                new Target { Run = 10, Deposit = 600 },
            }, out _);
            await source.SaveAsync();

            var expected = Replace(EconomyYaml, "GoldenBottleChance: 0.02", "GoldenBottleChance: 0.03");
            expected = Replace(expected, "Deposit: 250   #", "Deposit: 300   #");
            Assert.Equal(expected, provider.Files["Economy.yaml"]);
        }

        #endregion

        #region Generated context

        [Fact]
        public async Task GeneratedContext_SavingPolymorphicYamlTable_KeepsComments()
        {
            // Skills.yaml is a sample table with a header comment and polymorphic ($type) effects,
            // loaded through the generated GameDataContext exactly as the Unity editor does.
            var directory = Path.Combine(Path.GetTempPath(), "datra-yaml-comments-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var original = File.ReadAllText(Path.Combine(TestDataHelper.FindDataPath(), "Skills.yaml"));
                var annotated = Replace(original, "  ManaCost: 30\n", "  ManaCost: 30   # cheap on purpose\n");
                File.WriteAllText(Path.Combine(directory, "Skills.yaml"), annotated);

                var context = new GameDataContext(new TestRawDataProvider(directory), new DataSerializerFactory(new[] { typeof(SkillEffect) }));
                await context.Skill.InitializeAsync();
                var source = new EditableKeyValueDataSource<string, SkillData>(context.Skill);

                source.TrackPropertyChange("skill_heal", nameof(SkillData.ManaCost), 35, out _);
                await source.SaveAsync();

                var saved = File.ReadAllText(Path.Combine(directory, "Skills.yaml"));
                Assert.Equal(Replace(annotated, "ManaCost: 30   #", "ManaCost: 35   #"), saved);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        #endregion

        #region Other formats and scale

        [Fact]
        public async Task JsonTable_IsStillWrittenByTheSerializerAsBefore()
        {
            const string json = "[{\"Id\":\"a\",\"Name\":\"A\",\"Base\":1,\"Levels\":[],\"FirstCost\":10,\"RequiresUpgrade\":\"\",\"Implemented\":false}]";
            var (source, provider, _) = await OpenTableAsync(json, "Upgrades.json");

            source.TrackPropertyChange("a", nameof(UpgradeRow.FirstCost), 20, out _);
            var saved = await SaveAsync(source, provider, "Upgrades.json");

            var serializer = new DataSerializerFactory().GetSerializer("Upgrades.json");
            var expected = serializer.SerializeTable(serializer.DeserializeTable<string, UpgradeRow>(json.Replace("\"FirstCost\":10", "\"FirstCost\":20")));
            Assert.Equal(expected, saved);
        }

        [Fact]
        public async Task LargeTable_ChangingOneRow_IsFastAndExact()
        {
            var sb = new StringBuilder("# generated table\n\n");
            for (var i = 0; i < 3000; i++)
            {
                sb.Append("# row ").Append(i).Append('\n');
                sb.Append("- Id: row-").Append(i).Append('\n');
                sb.Append("  Name: Row ").Append(i).Append("   # name\n");
                sb.Append("  Base: ").Append(i).Append('\n');
                sb.Append("  Levels: [1, 2, 3]\n");
                sb.Append("  FirstCost: ").Append(i * 10).Append('\n');
                sb.Append("  RequiresUpgrade: \"\"\n");
                sb.Append("  Implemented: true\n\n");
            }
            var yaml = sb.ToString();
            var (source, provider, _) = await OpenTableAsync(yaml);

            source.TrackPropertyChange("row-1500", nameof(UpgradeRow.FirstCost), 1, out _);
            var stopwatch = Stopwatch.StartNew();
            var saved = await SaveAsync(source, provider);
            stopwatch.Stop();

            Assert.Equal(Replace(yaml, "FirstCost: 15000\n", "FirstCost: 1\n"), saved);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Saving took {stopwatch.Elapsed}.");
        }

        #endregion
    }
}
