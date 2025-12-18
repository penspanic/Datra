#nullable enable
using Datra.Editor;
using Datra.Editor.Services;
using Xunit;

namespace Datra.Editor.Tests
{
    public class ChangeTrackingServiceTests
    {
        [Fact]
        public void RegisterFile_AddsFileToTracking()
        {
            var service = new ChangeTrackingService();
            var path = new DataFilePath("test.csv");

            service.RegisterFile(path, () => "content");

            Assert.True(service.IsTracking(path));
        }

        [Fact]
        public void UnregisterFile_RemovesFromTracking()
        {
            var service = new ChangeTrackingService();
            var path = new DataFilePath("test.csv");
            service.RegisterFile(path, () => "content");

            service.UnregisterFile(path);

            Assert.False(service.IsTracking(path));
        }

        [Fact]
        public void InitializeBaseline_MarksAsUnmodified()
        {
            var service = new ChangeTrackingService();
            var path = new DataFilePath("test.csv");
            var content = "initial content";

            service.InitializeBaseline(path, () => content);

            Assert.False(service.HasUnsavedChanges(path));
        }

        [Fact]
        public void HasUnsavedChanges_DetectsChanges()
        {
            var service = new ChangeTrackingService();
            var path = new DataFilePath("test.csv");
            var content = "initial content";

            service.InitializeBaseline(path, () => content);

            // Change content
            content = "modified content";

            Assert.True(service.HasUnsavedChanges(path));
        }

        [Fact]
        public void HasUnsavedChanges_ReturnsFalseWhenUnchanged()
        {
            var service = new ChangeTrackingService();
            var path = new DataFilePath("test.csv");
            var content = "content";

            service.InitializeBaseline(path, () => content);

            Assert.False(service.HasUnsavedChanges(path));
        }

        [Fact]
        public void HasAnyUnsavedChanges_ReturnsTrueWhenAnyModified()
        {
            var service = new ChangeTrackingService();
            var path1 = new DataFilePath("file1.csv");
            var path2 = new DataFilePath("file2.csv");
            var content1 = "content1";
            var content2 = "content2";

            service.InitializeBaseline(path1, () => content1);
            service.InitializeBaseline(path2, () => content2);

            // Modify only one
            content2 = "modified";

            Assert.True(service.HasAnyUnsavedChanges());
        }

        [Fact]
        public void HasAnyUnsavedChanges_ReturnsFalseWhenNoneModified()
        {
            var service = new ChangeTrackingService();
            var path1 = new DataFilePath("file1.csv");
            var path2 = new DataFilePath("file2.csv");

            service.InitializeBaseline(path1, () => "content1");
            service.InitializeBaseline(path2, () => "content2");

            Assert.False(service.HasAnyUnsavedChanges());
        }

        [Fact]
        public void GetModifiedFiles_ReturnsOnlyModified()
        {
            var service = new ChangeTrackingService();
            var path1 = new DataFilePath("file1.csv");
            var path2 = new DataFilePath("file2.csv");
            var content1 = "content1";
            var content2 = "content2";

            service.InitializeBaseline(path1, () => content1);
            service.InitializeBaseline(path2, () => content2);

            // Modify only path2
            content2 = "modified";

            var modified = service.GetModifiedFiles().ToList();

            Assert.Single(modified);
            Assert.Equal(path2, modified[0]);
        }

        [Fact]
        public void ResetChanges_ClearsModifiedState()
        {
            var service = new ChangeTrackingService();
            var path = new DataFilePath("test.csv");
            var content = "initial";

            service.InitializeBaseline(path, () => content);
            content = "modified";
            Assert.True(service.HasUnsavedChanges(path));

            service.ResetChanges(path);

            Assert.False(service.HasUnsavedChanges(path));
        }

        [Fact]
        public void OnModifiedStateChanged_FiresWhenStateChanges()
        {
            var service = new ChangeTrackingService();
            var path = new DataFilePath("test.csv");
            var content = "initial";
            var eventFired = false;
            DataFilePath? eventPath = null;
            bool? eventIsModified = null;

            service.OnModifiedStateChanged += (p, m) =>
            {
                eventFired = true;
                eventPath = p;
                eventIsModified = m;
            };

            service.InitializeBaseline(path, () => content);

            Assert.True(eventFired);
            Assert.Equal(path, eventPath);
            Assert.False(eventIsModified);
        }

        [Fact]
        public void InitializeAllBaselines_InitializesAllRegistered()
        {
            var service = new ChangeTrackingService();
            var path1 = new DataFilePath("file1.csv");
            var path2 = new DataFilePath("file2.csv");
            var content1 = "content1";
            var content2 = "content2";

            service.RegisterFile(path1, () => content1);
            service.RegisterFile(path2, () => content2);

            // Modify before initializing
            content1 = "modified1";
            content2 = "modified2";

            service.InitializeAllBaselines();

            // After initializing, should not be modified
            Assert.False(service.HasUnsavedChanges(path1));
            Assert.False(service.HasUnsavedChanges(path2));
        }

        [Fact]
        public void HasUnsavedChanges_ReturnsFalseForUnregisteredFile()
        {
            var service = new ChangeTrackingService();
            var path = new DataFilePath("nonexistent.csv");

            Assert.False(service.HasUnsavedChanges(path));
        }

        [Fact]
        public void IsTracking_ReturnsFalseForUnregisteredFile()
        {
            var service = new ChangeTrackingService();
            var path = new DataFilePath("nonexistent.csv");

            Assert.False(service.IsTracking(path));
        }
    }
}
