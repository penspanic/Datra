#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;
using Datra.SampleData2.Models;
using Datra.WebEditor.Services;
using Xunit;

namespace Datra.WebEditor.Tests;

public class HostServiceTests
{
    [Fact]
    public async Task InitializeAsync_discovers_ITableRepository_properties()
    {
        using var shop = new ShopFixture();
        var host = new DatraEditorHostService(new DataChangedNotifier());

        await host.InitializeAsync(shop.Context);

        Assert.Single(host.DataTypes);
        var shopType = host.DataTypes[0];
        Assert.Equal(typeof(ShopItemData), shopType.DataType);
        Assert.NotNull(host.GetDataSource(typeof(ShopItemData)));
        Assert.False(host.HasAnyUnsavedChanges());
    }

    [Fact]
    public async Task Edited_row_marks_type_dirty()
    {
        using var shop = new ShopFixture();
        var host = new DatraEditorHostService(new DataChangedNotifier());
        await host.InitializeAsync(shop.Context);

        var source = host.GetDataSource(typeof(ShopItemData))!;
        Assert.False(host.HasUnsavedChanges(typeof(ShopItemData)));

        source.TrackPropertyChange("potion_hp_small", nameof(ShopItemData.Price), 999, out var modified);

        Assert.True(modified);
        Assert.True(host.HasUnsavedChanges(typeof(ShopItemData)));
    }

    [Fact]
    public async Task SaveAsync_writes_changes_to_disk_and_clears_dirty()
    {
        using var shop = new ShopFixture();
        var host = new DatraEditorHostService(new DataChangedNotifier());
        await host.InitializeAsync(shop.Context);

        var source = host.GetDataSource(typeof(ShopItemData))!;

        // Mutate the working copy of one row so SaveAsync has something to persist.
        var working = ((IEditableDataSource<string, ShopItemData>)source).GetWorkingCopy("potion_hp_small");
        working.Price = 4242;
        source.TrackPropertyChange("potion_hp_small", nameof(ShopItemData.Price), 4242, out _);

        var saved = await host.SaveAsync(typeof(ShopItemData));

        Assert.True(saved);
        Assert.False(host.HasUnsavedChanges(typeof(ShopItemData)));

        var csv = shop.ReadFile("ShopItems.csv");
        Assert.Contains("potion_hp_small", csv);
        Assert.Contains("4242", csv);
    }

    [Fact]
    public async Task SaveAsync_publishes_DataChangedEvent()
    {
        using var shop = new ShopFixture();
        var notifier = new DataChangedNotifier();
        var host = new DatraEditorHostService(notifier);
        await host.InitializeAsync(shop.Context);

        DataChangedEvent? captured = null;
        notifier.Changed += evt =>
        {
            captured = evt;
            return Task.CompletedTask;
        };

        var source = host.GetDataSource(typeof(ShopItemData))!;
        source.TrackPropertyChange("potion_hp_small", nameof(ShopItemData.Price), 5000, out _);

        await host.SaveAsync(typeof(ShopItemData));

        Assert.NotNull(captured);
        Assert.Equal(typeof(ShopItemData), captured!.DataType);
        Assert.Equal(DataChangeKind.Saved, captured.Kind);
    }

    [Fact]
    public async Task ReloadAsync_drops_pending_edits_and_fires_notifier()
    {
        using var shop = new ShopFixture();
        var notifier = new DataChangedNotifier();
        var host = new DatraEditorHostService(notifier);
        await host.InitializeAsync(shop.Context);

        var source = host.GetDataSource(typeof(ShopItemData))!;
        source.TrackPropertyChange("potion_hp_small", nameof(ShopItemData.Price), 1, out _);
        Assert.True(host.HasUnsavedChanges(typeof(ShopItemData)));

        var seen = 0;
        notifier.Changed += _ => { seen++; return Task.CompletedTask; };

        var reloaded = await host.ReloadAsync(typeof(ShopItemData));

        Assert.True(reloaded);
        Assert.False(host.HasUnsavedChanges(typeof(ShopItemData)));
        Assert.Equal(1, seen);
    }

    [Fact]
    public async Task AddItem_extension_creates_a_new_row()
    {
        using var shop = new ShopFixture();
        var host = new DatraEditorHostService(new DataChangedNotifier());
        await host.InitializeAsync(shop.Context);

        var source = host.GetDataSource(typeof(ShopItemData))!;
        var keyTypes = source.GetKeyValueTypes();

        Assert.NotNull(keyTypes);
        Assert.Equal(typeof(string), keyTypes!.Value.Key);
        Assert.Equal(typeof(ShopItemData), keyTypes.Value.Data);

        var value = (ShopItemData)DatraEditorHostServiceExtensions.CreateDefaultValue(typeof(ShopItemData))!;
        value.Name = "Test Brew";
        value.Price = 7;
        DatraEditorHostServiceExtensions.StampKey(value, "test_brew");
        source.AddItem("test_brew", value);

        Assert.True(source.ContainsKey("test_brew"));
        Assert.Equal(ItemState.Added, source.GetItemState("test_brew"));
        Assert.True(host.HasUnsavedChanges(typeof(ShopItemData)));
    }

    [Theory]
    [InlineData("hello", typeof(string), "hello")]
    [InlineData("42", typeof(int), 42)]
    [InlineData("9000000000", typeof(long), 9000000000L)]
    public void TryParseKey_handles_common_key_types(string raw, System.Type keyType, object expected)
    {
        var (ok, value) = DatraEditorHostServiceExtensions.TryParseKey(keyType, raw);
        Assert.True(ok);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryParseKey_rejects_invalid_input()
    {
        var (ok, _) = DatraEditorHostServiceExtensions.TryParseKey(typeof(int), "not-a-number");
        Assert.False(ok);
    }
}
