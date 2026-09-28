#nullable enable
using System.Collections.Generic;
using System.Linq;
using Datra.Interfaces;
using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
    /// <summary>
    /// Cases for <see cref="YamlCommentPreserver"/> that the repository API cannot reach
    /// (reordering) or that are about when it must give up.
    /// </summary>
    public class YamlCommentPreserverTests
    {
        public class Row : ITableData<string>
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public List<string> Tags { get; set; } = new List<string>();
            public Stats Stats { get; set; } = new Stats();
            public string Notes { get; set; } = string.Empty;
        }

        public class Stats
        {
            public int Hp { get; set; }
            public int Speed { get; set; }
        }

        private static readonly YamlDataSerializer Serializer = new YamlDataSerializer();

        private static Dictionary<string, Row> Load(string yaml) => Serializer.DeserializeTable<string, Row>(yaml);

        private static string Save(string original, Dictionary<string, Row> table)
            => YamlCommentPreserver.Reconcile(
                original,
                Serializer.SerializeTable(table),
                text => Serializer.DeserializeTable<string, Row>(text),
                t => Serializer.SerializeTable(t));

        private const string Yaml =
            "# header\n" +
            "\n" +
            "# about a\n" +
            "- Id: a\n" +
            "  Name: Alpha\n" +
            "  Tags: [x, y]\n" +
            "  Stats:\n" +
            "    Hp: 10     # base hp\n" +
            "    # speed is tiles per second\n" +
            "    Speed: 2\n" +
            "\n" +
            "# about b\n" +
            "- Id: b\n" +
            "  Name: Beta   # placeholder name\n" +
            "  Tags:\n" +
            "    - one      # first\n" +
            "    - two      # second\n" +
            "    - three    # third\n" +
            "\n" +
            "# about c\n" +
            "- Id: c\n" +
            "  Name: Gamma\n";

        [Fact]
        public void ReorderingRows_MovesEachRowWithItsComments()
        {
            var table = Load(Yaml);
            var reordered = new Dictionary<string, Row> { ["c"] = table["c"], ["a"] = table["a"], ["b"] = table["b"] };

            var saved = Save(Yaml, reordered);

            var expected =
                "# header\n" +
                "\n" +
                "# about c\n" +
                "- Id: c\n" +
                "  Name: Gamma\n" +
                "# about a\n" +
                "- Id: a\n" +
                "  Name: Alpha\n" +
                "  Tags: [x, y]\n" +
                "  Stats:\n" +
                "    Hp: 10     # base hp\n" +
                "    # speed is tiles per second\n" +
                "    Speed: 2\n" +
                "\n" +
                "# about b\n" +
                "- Id: b\n" +
                "  Name: Beta   # placeholder name\n" +
                "  Tags:\n" +
                "    - one      # first\n" +
                "    - two      # second\n" +
                "    - three    # third\n";
            Assert.Equal(expected, saved);
        }

        [Fact]
        public void RemovingMiddleOfPlainList_KeepsTheOtherEntriesComments()
        {
            var table = Load(Yaml);
            table["b"].Tags = new List<string> { "one", "three" };

            var saved = Save(Yaml, table);

            Assert.Equal(Yaml.Replace("    - two      # second\n", ""), saved);
        }

        [Fact]
        public void ChangingNestedObjectField_KeepsCommentsInTheNestedObject()
        {
            var table = Load(Yaml);
            table["a"].Stats.Speed = 3;

            var saved = Save(Yaml, table);

            Assert.Equal(Yaml.Replace("    Speed: 2\n", "    Speed: 3\n"), saved);
        }

        [Fact]
        public void ChangingToMultiLineText_WritesABlockScalarUnderTheField()
        {
            var table = Load(Yaml);
            table["c"].Notes = "line one\nline two";

            var saved = Save(Yaml, table);

            Assert.StartsWith(Yaml, saved);
            Assert.Equal("line one\nline two", Load(saved)["c"].Notes);
        }

        [Fact]
        public void ChangingLiteralBlockText_KeepsTheCommentsAroundIt()
        {
            var yaml = Yaml.Replace(
                "  Name: Gamma\n",
                "  Name: Gamma\n  # designer notes\n  Notes: |\n    first line\n    second line\n  # after notes\n");
            var table = Load(yaml);
            table["c"].Notes = "only line";

            var saved = Save(yaml, table);

            Assert.Equal(yaml.Replace("Notes: |\n    first line\n    second line\n", "Notes: only line\n"), saved);
        }

        [Fact]
        public void FillingAnEmptyField_WritesTheValueAfterTheColon()
        {
            var yaml = Yaml.Replace("  Name: Gamma\n", "  Name:     # to be named\n");
            var table = Load(yaml);
            table["c"].Name = "Gamma";

            var saved = Save(yaml, table);

            Assert.Equal(yaml.Replace("  Name:     #", "  Name: Gamma     #"), saved);
        }

        [Fact]
        public void FieldsTheTypeDoesNotKnow_AreKeptWhenAnotherFieldChanges()
        {
            var yaml = Yaml.Replace("  Name: Gamma\n", "  Name: Gamma\n  Legacy: keep me\n");
            var table = Load(yaml);
            table["c"].Name = "Gamma 2";

            var saved = Save(yaml, table);

            Assert.Equal(yaml.Replace("Name: Gamma\n", "Name: Gamma 2\n"), saved);
        }

        [Fact]
        public void EmptyTableWithHeader_AddingARowKeepsTheHeader()
        {
            const string yaml = "# nothing yet\n[]\n";
            var table = Load(yaml);
            table["a"] = new Row { Id = "a", Name = "Alpha" };

            var saved = Save(yaml, table);

            Assert.StartsWith("# nothing yet\n- Id: a\n", saved);
            Assert.Equal("Alpha", Load(saved)["a"].Name);
        }

        [Fact]
        public void DeletingEveryRow_KeepsTheHeader()
        {
            var saved = Save(Yaml, new Dictionary<string, Row>());

            Assert.Equal("# header\n\n[]\n", saved);
            Assert.Empty(Load(saved));
        }

        [Fact]
        public void DocumentStartMarker_IsKept()
        {
            var yaml = "---\n" + Yaml;
            var table = Load(yaml);
            table["c"].Name = "Gamma 2";

            var saved = Save(yaml, table);

            Assert.Equal(yaml.Replace("Name: Gamma\n", "Name: Gamma 2\n"), saved);
        }

        [Fact]
        public void AnchorsAndAliases_ChangingTheAnchoredValue_KeepsTheAliasUserCorrect()
        {
            const string yaml =
                "# shared tags\n" +
                "- Id: a\n" +
                "  Tags: &shared [x, y]\n" +
                "- Id: b\n" +
                "  Tags: *shared\n";
            var table = Load(yaml);
            table["a"].Tags = new List<string> { "z" };

            var saved = Save(yaml, table);

            // The anchored value cannot be patched in place without leaving *shared
            // dangling; both rows are rewritten and the data stays right.
            var reloaded = Load(saved);
            Assert.Equal(new List<string> { "z" }, reloaded["a"].Tags);
            Assert.Equal(new List<string> { "x", "y" }, reloaded["b"].Tags);
            Assert.StartsWith("# shared tags\n", saved);
        }

        [Fact]
        public void Merge_ReturnsNullForMultipleDocuments()
        {
            var canonical = Serializer.SerializeTable(Load(Yaml));
            Assert.Null(YamlCommentPreserver.Merge(Yaml + "---\n- Id: z\n", canonical, canonical));
        }

        [Fact]
        public void Reconcile_WithoutOriginal_ReturnsTheSerializerOutput()
        {
            var canonical = Serializer.SerializeTable(Load(Yaml));
            Assert.Same(canonical, YamlCommentPreserver.Reconcile<Dictionary<string, Row>>(null, canonical, Load, t => Serializer.SerializeTable(t)));
            Assert.Same(canonical, YamlCommentPreserver.Reconcile<Dictionary<string, Row>>("  \n", canonical, Load, t => Serializer.SerializeTable(t)));
        }

        [Fact]
        public void Reconcile_WithUnreadableOriginal_ReturnsTheSerializerOutput()
        {
            var canonical = Serializer.SerializeTable(Load(Yaml));
            var saved = YamlCommentPreserver.Reconcile<Dictionary<string, Row>>("- Id: [unclosed\n", canonical, Load, t => Serializer.SerializeTable(t));
            Assert.Equal(canonical, saved);
        }
    }
}
