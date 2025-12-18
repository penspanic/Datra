#nullable enable
using Datra.Editor;
using Datra.Editor.Interfaces;
using Xunit;

namespace Datra.Editor.Tests
{
    public class DataFilePathTests
    {
        [Fact]
        public void Constructor_NormalizesBackslashes()
        {
            var path = new DataFilePath(@"folder\subfolder\file.csv");

            Assert.Equal("folder/subfolder/file.csv", path.ToString());
        }

        [Fact]
        public void Constructor_RemovesTrailingSlash()
        {
            var path = new DataFilePath("folder/subfolder/");

            Assert.Equal("folder/subfolder", path.ToString());
        }

        [Fact]
        public void FileName_ReturnsFileNameOnly()
        {
            var path = new DataFilePath("folder/subfolder/file.csv");

            Assert.Equal("file.csv", path.FileName);
        }

        [Fact]
        public void Extension_ReturnsExtensionWithDot()
        {
            var path = new DataFilePath("folder/file.csv");

            Assert.Equal(".csv", path.Extension);
        }

        [Fact]
        public void Directory_ReturnsDirectoryPath()
        {
            var path = new DataFilePath("folder/subfolder/file.csv");

            Assert.Equal("folder/subfolder", path.Directory);
        }

        [Fact]
        public void IsValid_ReturnsFalseForEmpty()
        {
            var path = DataFilePath.Empty;

            Assert.False(path.IsValid);
        }

        [Fact]
        public void IsValid_ReturnsTrueForValidPath()
        {
            var path = new DataFilePath("file.csv");

            Assert.True(path.IsValid);
        }

        [Fact]
        public void Equals_IsCaseInsensitive()
        {
            var path1 = new DataFilePath("Folder/File.CSV");
            var path2 = new DataFilePath("folder/file.csv");

            Assert.Equal(path1, path2);
        }

        [Fact]
        public void Equals_HandlesNormalizedPaths()
        {
            var path1 = new DataFilePath(@"folder\file.csv");
            var path2 = new DataFilePath("folder/file.csv");

            Assert.Equal(path1, path2);
        }

        [Fact]
        public void GetHashCode_SameForEqualPaths()
        {
            var path1 = new DataFilePath("Folder/File.csv");
            var path2 = new DataFilePath("folder/file.csv");

            Assert.Equal(path1.GetHashCode(), path2.GetHashCode());
        }

        [Fact]
        public void ImplicitConversion_FromString()
        {
            DataFilePath path = "folder/file.csv";

            Assert.Equal("folder/file.csv", path.ToString());
        }

        [Fact]
        public void ExplicitConversion_ToString()
        {
            var path = new DataFilePath("folder/file.csv");

            string result = (string)path;

            Assert.Equal("folder/file.csv", result);
        }

        [Fact]
        public void Combine_CreatesNewPath()
        {
            var basePath = new DataFilePath("folder");

            var combined = basePath.Combine("file.csv");

            Assert.Equal("folder/file.csv", combined.ToString());
        }

        [Fact]
        public void IsNullOrEmpty_ReturnsTrueForNull()
        {
            DataFilePath? path = null;

            Assert.True(DataFilePath.IsNullOrEmpty(path));
        }

        [Fact]
        public void IsNullOrEmpty_ReturnsTrueForEmpty()
        {
            DataFilePath? path = DataFilePath.Empty;

            Assert.True(DataFilePath.IsNullOrEmpty(path));
        }

        [Fact]
        public void IsNullOrEmpty_ReturnsFalseForValid()
        {
            DataFilePath? path = new DataFilePath("file.csv");

            Assert.False(DataFilePath.IsNullOrEmpty(path));
        }

        [Fact]
        public void EqualityOperators_Work()
        {
            var path1 = new DataFilePath("file.csv");
            var path2 = new DataFilePath("file.csv");
            var path3 = new DataFilePath("other.csv");

            Assert.True(path1 == path2);
            Assert.False(path1 != path2);
            Assert.True(path1 != path3);
            Assert.False(path1 == path3);
        }
    }
}
