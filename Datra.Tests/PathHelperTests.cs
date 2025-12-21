using Datra.Editor.Utilities;
using Xunit;

namespace Datra.Tests
{
    public class PathHelperTests
    {
        #region IsAbsolutePath Tests

        [Fact]
        public void IsAbsolutePath_NullPath_ReturnsFalse()
        {
            Assert.False(PathHelper.IsAbsolutePath(null));
        }

        [Fact]
        public void IsAbsolutePath_EmptyPath_ReturnsFalse()
        {
            Assert.False(PathHelper.IsAbsolutePath(""));
        }

        [Fact]
        public void IsAbsolutePath_RelativePath_ReturnsFalse()
        {
            Assert.False(PathHelper.IsAbsolutePath("folder/file.txt"));
        }

        [Fact]
        public void IsAbsolutePath_RelativePathWithDot_ReturnsFalse()
        {
            Assert.False(PathHelper.IsAbsolutePath("./folder/file.txt"));
        }

        [Fact]
        public void IsAbsolutePath_RelativePathWithDoubleDot_ReturnsFalse()
        {
            Assert.False(PathHelper.IsAbsolutePath("../folder/file.txt"));
        }

        [Fact]
        public void IsAbsolutePath_UnixAbsolutePath_ReturnsTrue()
        {
            Assert.True(PathHelper.IsAbsolutePath("/Users/test/file.txt"));
        }

        [Fact]
        public void IsAbsolutePath_UnixRootPath_ReturnsTrue()
        {
            Assert.True(PathHelper.IsAbsolutePath("/"));
        }

        [Fact]
        public void IsAbsolutePath_WindowsAbsolutePath_ReturnsTrue()
        {
            Assert.True(PathHelper.IsAbsolutePath("C:\\Users\\test\\file.txt"));
        }

        [Fact]
        public void IsAbsolutePath_WindowsAbsolutePathForwardSlash_ReturnsTrue()
        {
            Assert.True(PathHelper.IsAbsolutePath("C:/Users/test/file.txt"));
        }

        [Fact]
        public void IsAbsolutePath_WindowsDriveLetter_ReturnsTrue()
        {
            Assert.True(PathHelper.IsAbsolutePath("D:"));
        }

        [Fact]
        public void IsAbsolutePath_LowercaseDriveLetter_ReturnsTrue()
        {
            Assert.True(PathHelper.IsAbsolutePath("c:\\folder"));
        }

        [Fact]
        public void IsAbsolutePath_PackagesPath_ReturnsFalse()
        {
            Assert.False(PathHelper.IsAbsolutePath("Packages/com.example/file.txt"));
        }

        [Fact]
        public void IsAbsolutePath_AssetsPath_ReturnsFalse()
        {
            Assert.False(PathHelper.IsAbsolutePath("Assets/Scripts/file.cs"));
        }

        #endregion

        #region CombinePath Tests

        [Fact]
        public void CombinePath_NullBasePath_ReturnsPath()
        {
            var result = PathHelper.CombinePath(null, "folder/file.txt");
            Assert.Equal("folder/file.txt", result);
        }

        [Fact]
        public void CombinePath_EmptyBasePath_ReturnsPath()
        {
            var result = PathHelper.CombinePath("", "folder/file.txt");
            Assert.Equal("folder/file.txt", result);
        }

        [Fact]
        public void CombinePath_NullPath_ReturnsBasePath()
        {
            var result = PathHelper.CombinePath("base", null);
            Assert.Equal("base", result);
        }

        [Fact]
        public void CombinePath_EmptyPath_ReturnsBasePath()
        {
            var result = PathHelper.CombinePath("base", "");
            Assert.Equal("base", result);
        }

        [Fact]
        public void CombinePath_BothNull_ReturnsEmpty()
        {
            var result = PathHelper.CombinePath(null, null);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void CombinePath_RelativePaths_CombinesWithSlash()
        {
            var result = PathHelper.CombinePath("base/folder", "sub/file.txt");
            Assert.Equal("base/folder/sub/file.txt", result);
        }

        [Fact]
        public void CombinePath_BasePathWithTrailingSlash_RemovesExtraSlash()
        {
            var result = PathHelper.CombinePath("base/folder/", "sub/file.txt");
            Assert.Equal("base/folder/sub/file.txt", result);
        }

        [Fact]
        public void CombinePath_PathWithLeadingSlash_ButRelative_RemovesLeadingSlash()
        {
            // A path like "/sub/file.txt" starting with / is actually absolute on Unix
            // So it should be returned as-is
            var result = PathHelper.CombinePath("base", "/absolute/path.txt");
            Assert.Equal("/absolute/path.txt", result);
        }

        [Fact]
        public void CombinePath_AbsoluteUnixPath_ReturnsPathAsIs()
        {
            var result = PathHelper.CombinePath("Packages/com.example", "/Users/test/file.txt");
            Assert.Equal("/Users/test/file.txt", result);
        }

        [Fact]
        public void CombinePath_AbsoluteWindowsPath_ReturnsPathAsIs()
        {
            var result = PathHelper.CombinePath("Packages/com.example", "C:\\Users\\test\\file.txt");
            Assert.Equal("C:\\Users\\test\\file.txt", result);
        }

        [Fact]
        public void CombinePath_UnityPackagesPath_CombinesCorrectly()
        {
            var result = PathHelper.CombinePath("Packages/com.example/Resources", "Scripts/test.json");
            Assert.Equal("Packages/com.example/Resources/Scripts/test.json", result);
        }

        [Fact]
        public void CombinePath_BasePathWithBackslash_RemovesTrailing()
        {
            var result = PathHelper.CombinePath("base\\folder\\", "file.txt");
            Assert.Equal("base\\folder/file.txt", result);
        }

        #endregion

        #region Bug Reproduction Tests

        /// <summary>
        /// This test reproduces the original bug where absolute paths from LoadMultipleTextAsync
        /// were incorrectly combined with basePath in SaveTextAsync
        /// </summary>
        [Fact]
        public void CombinePath_AbsoluteMetaFilePath_NotCombinedWithBasePath()
        {
            // Simulates the bug scenario:
            // basePath = "Packages/com.penspanic.datra.sampledata/Resources"
            // Returned from LoadMultipleTextAsync: "/Users/pp/dev/Datra/SampleData/Resources/Scripts/hello.json"
            // Meta path: "/Users/pp/dev/Datra/SampleData/Resources/Scripts/hello.json.datrameta"

            var basePath = "Packages/com.penspanic.datra.sampledata/Resources";
            var absoluteMetaPath = "/Users/pp/dev/Datra/SampleData/Resources/Scripts/hello.json.datrameta";

            var result = PathHelper.CombinePath(basePath, absoluteMetaPath);

            // The bug would produce:
            // "Packages/com.penspanic.datra.sampledata/Resources/Users/pp/dev/..."
            // The fix should return the absolute path as-is:
            Assert.Equal(absoluteMetaPath, result);
        }

        [Fact]
        public void CombinePath_WindowsAbsoluteMetaFilePath_NotCombinedWithBasePath()
        {
            var basePath = "Packages/com.penspanic.datra.sampledata/Resources";
            var absoluteMetaPath = "C:\\Users\\dev\\Datra\\SampleData\\Resources\\Scripts\\hello.json.datrameta";

            var result = PathHelper.CombinePath(basePath, absoluteMetaPath);

            Assert.Equal(absoluteMetaPath, result);
        }

        [Fact]
        public void IsAbsolutePath_MetaFilePath_DetectedCorrectly()
        {
            // Unix meta file path
            Assert.True(PathHelper.IsAbsolutePath("/Users/pp/dev/Datra/file.json.datrameta"));

            // Windows meta file path
            Assert.True(PathHelper.IsAbsolutePath("C:\\Users\\dev\\file.json.datrameta"));

            // Relative meta file path
            Assert.False(PathHelper.IsAbsolutePath("Resources/Scripts/file.json.datrameta"));
        }

        #endregion
    }
}
