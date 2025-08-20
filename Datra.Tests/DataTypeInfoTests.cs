using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Generated;
using Xunit;

namespace Datra.Tests
{
    public class DataTypeInfoTests
    {
        private readonly GameDataContext _context;
        private readonly TestRawDataProvider _provider;
        
        public DataTypeInfoTests()
        {
            var basePath = TestDataHelper.FindDataPath();
            _provider = new TestRawDataProvider(basePath);
            _context = new GameDataContext(_provider);
        }
        
        [Fact]
        public void GetDataTypeInfos_ReturnsAllDataTypes()
        {
            // Act
            var dataTypeInfos = _context.GetDataTypeInfos();
            
            // Assert
            Assert.NotNull(dataTypeInfos);
            Assert.True(dataTypeInfos.Count > 0, "Should have at least one data type info");
            
            // Check that some expected types are present
            var hasItemData = dataTypeInfos.Any(info => info.PropertyName == "Item");
            var hasCharacterData = dataTypeInfos.Any(info => info.PropertyName == "Character");
            
            Assert.True(hasItemData, "Should have Item data type");
            Assert.True(hasCharacterData, "Should have Character data type");
        }
        
        [Fact]
        public async Task LoadAllAsync_UpdatesDataTypeInfos()
        {
            // Arrange - Get initial state
            var initialInfos = _context.GetDataTypeInfos();
            var initialInfo = initialInfos.FirstOrDefault(info => info.PropertyName == "Item");
            Assert.NotNull(initialInfo);
            Assert.False(initialInfo.IsLoaded, "Should not be loaded initially");
            Assert.Null(initialInfo.LoadedFilePath);
            
            // Act
            await _context.LoadAllAsync();
            
            // Assert - Check updated state
            var updatedInfos = _context.GetDataTypeInfos();
            var updatedInfo = updatedInfos.FirstOrDefault(info => info.PropertyName == "Item");
            
            Assert.NotNull(updatedInfo);
            Assert.True(updatedInfo.IsLoaded, "Should be loaded after LoadAllAsync");
            Assert.NotNull(updatedInfo.LoadedFilePath);
            Assert.NotEqual(updatedInfo.FilePath, updatedInfo.LoadedFilePath);
            
            // Verify that the loaded file path is absolute
            Assert.True(Path.IsPathRooted(updatedInfo.LoadedFilePath), 
                $"LoadedFilePath should be absolute: {updatedInfo.LoadedFilePath}");
        }
        
        [Fact]
        public async Task DataTypeInfo_AllPropertiesSet()
        {
            // Arrange & Act
            await _context.LoadAllAsync();
            var dataTypeInfos = _context.GetDataTypeInfos();
            
            // Assert
            foreach (var info in dataTypeInfos)
            {
                // Basic properties should be set
                Assert.False(string.IsNullOrEmpty(info.TypeName), 
                    $"TypeName should be set for {info.PropertyName}");
                Assert.NotNull(info.DataType);
                Assert.False(string.IsNullOrEmpty(info.PropertyName));
                Assert.False(string.IsNullOrEmpty(info.FilePath));
                
                // After loading, these should be set
                Assert.True(info.IsLoaded, $"{info.PropertyName} should be loaded");
                Assert.False(string.IsNullOrEmpty(info.LoadedFilePath), 
                    $"LoadedFilePath should be set for {info.PropertyName}");
                
                // TypeName should be fully qualified
                Assert.Contains(".", info.TypeName);
            }
        }
        
        [Fact]
        public void ResolveFilePath_ReturnsAbsolutePath()
        {
            // Act
            var resolvedPath = _provider.ResolveFilePath("Item.json");
            
            // Assert
            Assert.NotNull(resolvedPath);
            Assert.True(Path.IsPathRooted(resolvedPath), 
                $"Resolved path should be absolute: {resolvedPath}");
            Assert.Contains("Item.json", resolvedPath);
        }
        
        [Fact]
        public async Task FilePath_And_LoadedFilePath_AreDifferent()
        {
            // Arrange & Act
            await _context.LoadAllAsync();
            var dataTypeInfos = _context.GetDataTypeInfos();
            
            // Assert
            foreach (var info in dataTypeInfos)
            {
                // FilePath should be the configured relative path
                Assert.False(Path.IsPathRooted(info.FilePath), 
                    $"FilePath should be relative for {info.PropertyName}: {info.FilePath}");
                
                // LoadedFilePath should be the resolved absolute path
                Assert.True(Path.IsPathRooted(info.LoadedFilePath), 
                    $"LoadedFilePath should be absolute for {info.PropertyName}: {info.LoadedFilePath}");
                
                // They should be different
                Assert.NotEqual(info.FilePath, info.LoadedFilePath);
                
                // LoadedFilePath should contain the FilePath
                Assert.Contains(Path.GetFileName(info.FilePath), info.LoadedFilePath);
            }
        }
    }
}