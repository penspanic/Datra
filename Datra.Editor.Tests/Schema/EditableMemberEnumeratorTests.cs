#nullable enable
using System.Linq;
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Editor.Schema;
using Datra.Interfaces;
using Xunit;

namespace Datra.Editor.Tests.Schema
{
    public class EditableMemberEnumeratorTests
    {
        // Test model — covers each filter rule the enumerator applies.
        public class Sample : ITableData<string>
        {
            // visible field with the same name as a property below (shadow case)
            // Note: using a different name to keep this model compilable; see ShadowSample.
            public string Id { get; set; } = "";

            public string Name { get; set; } = "";

            // get-only — mirrors the generated `Ref` property pattern; CanWrite = false.
            public string ReadOnly => "computed";

            [DatraIgnore]
            public string Hidden { get; set; } = "";
        }

        public class ShadowSample
        {
            // Public field shadowed by a same-named property (case-insensitive).
            public string Title = "";
            public string title { get; set; } = "";
            public string OnlyProperty { get; set; } = "";
        }

        // Simulates the generator output: a CsvMetadata property marked [DatraIgnore] and a get-only Ref.
        public class GeneratedLikeSample : ITableData<string>
        {
            public string Id { get; set; } = "";
            public string Description { get; set; } = "";

            [DatraIgnore]
            public object CsvMetadata { get; set; } = new();

            public StringDataRef<GeneratedLikeSample> Ref => new(this.Id);
        }

        [Fact]
        public void ForType_Includes_RegularProperties()
        {
            var names = EditableMemberEnumerator.ForType(typeof(Sample)).Select(p => p.Name).ToHashSet();
            Assert.Contains("Id", names);
            Assert.Contains("Name", names);
        }

        [Fact]
        public void ForType_Excludes_GetOnlyProperty()
        {
            var names = EditableMemberEnumerator.ForType(typeof(Sample)).Select(p => p.Name).ToHashSet();
            Assert.DoesNotContain("ReadOnly", names);
        }

        [Fact]
        public void ForType_Excludes_DatraIgnoreProperty()
        {
            var names = EditableMemberEnumerator.ForType(typeof(Sample)).Select(p => p.Name).ToHashSet();
            Assert.DoesNotContain("Hidden", names);
        }

        [Fact]
        public void ForType_Excludes_PropertyShadowedByField()
        {
            var names = EditableMemberEnumerator.ForType(typeof(ShadowSample)).Select(p => p.Name).ToHashSet();
            // "title" property is shadowed by "Title" field (case-insensitive).
            Assert.DoesNotContain("title", names);
            Assert.Contains("OnlyProperty", names);
        }

        [Fact]
        public void ForType_OnGeneratorLike_Excludes_CsvMetadata_And_RefGetOnly()
        {
            var names = EditableMemberEnumerator.ForType(typeof(GeneratedLikeSample)).Select(p => p.Name).ToHashSet();
            Assert.Contains("Id", names);
            Assert.Contains("Description", names);
            Assert.DoesNotContain("CsvMetadata", names); // [DatraIgnore]
            Assert.DoesNotContain("Ref", names);         // get-only (CanWrite=false)
        }

        [Fact]
        public void ForTableRow_Excludes_Id()
        {
            var names = EditableMemberEnumerator.ForTableRow(typeof(Sample)).Select(p => p.Name).ToHashSet();
            Assert.DoesNotContain("Id", names);
            Assert.Contains("Name", names);
        }
    }
}
