#nullable disable
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datra.Editor.DataSources;
using Datra.Interfaces;
using Datra.Repositories;
using Datra.Serializers;
using Datra.Unity.Editor.Providers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// The Data Editor's save path inside Unity: AssetDatabaseRawDataProvider reads the
    /// file back through the AssetDatabase at save time, so this checks that comments
    /// survive that path, including a second save after the asset was re-imported.
    /// </summary>
    public class YamlCommentPreservationTests
    {
        public class UpgradeRow : ITableData<string>
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public List<double> Levels { get; set; } = new List<double>();
            public int FirstCost { get; set; }
        }

        private const string Folder = "Assets/DatraYamlCommentTest";
        private const string FileName = "Upgrades.yaml";

        private const string Yaml =
            "# Upgrades: what the player can buy between runs.\n" +
            "\n" +
            "# The first thing a new player buys.\n" +
            "- Id: push-strength\n" +
            "  Name: Push strength\n" +
            "  Levels: [0.9, 1.3, 1.6]\n" +
            "  FirstCost: 50   # cheap on purpose\n" +
            "\n" +
            "- Id: domes\n" +
            "  Name: Balance dome\n" +
            "  Levels:\n" +
            "    - 4   # fits the short stage\n" +
            "    - 5\n" +
            "  FirstCost: 150\n";

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, FileName), Yaml);
            AssetDatabase.ImportAsset(Folder + "/" + FileName);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [UnityTest]
        public IEnumerator SavingFromTheEditor_KeepsComments()
        {
            var repository = new KeyValueDataRepository<string, UpgradeRow>(
                FileName,
                new AssetDatabaseRawDataProvider(basePath: Folder),
                new DataSerializerFactory(),
                (data, serializer) => serializer.DeserializeTable<string, UpgradeRow>(data),
                (table, serializer) => serializer.SerializeTable(table));
            yield return Wait(repository.InitializeAsync());

            var source = new EditableKeyValueDataSource<string, UpgradeRow>(repository);
            source.TrackPropertyChange("push-strength", nameof(UpgradeRow.FirstCost), 55, out _);
            yield return Wait(source.SaveAsync());

            var expected = Yaml.Replace("FirstCost: 50   #", "FirstCost: 55   #");
            Assert.AreEqual(expected, File.ReadAllText(Path.Combine(Folder, FileName)));

            // Second save: the file is read back through the re-imported TextAsset.
            source.TrackPropertyChange("domes", nameof(UpgradeRow.Levels), new List<double> { 4, 6 }, out _);
            yield return Wait(source.SaveAsync());

            expected = expected.Replace("    - 5\n", "    - 6\n");
            Assert.AreEqual(expected, File.ReadAllText(Path.Combine(Folder, FileName)));
        }

        private static IEnumerator Wait(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                Assert.Fail(task.Exception?.ToString());
        }
    }
}
