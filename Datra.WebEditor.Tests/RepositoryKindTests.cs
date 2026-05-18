#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Datra.DataTypes;
using Datra.Editor.DataSources;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.SampleData.Models;
using Datra.WebEditor.Services;
using Xunit;

namespace Datra.WebEditor.Tests;

/// <summary>
/// Verifies <see cref="DatraEditorHostService"/> wraps all three Datra repository kinds —
/// Table, Single, Asset — when reflecting over a real generated DataContext.
/// </summary>
public class RepositoryKindTests
{
    [Fact]
    public async Task GameDataContext_surfaces_table_single_and_asset_repos()
    {
        using var fixture = new GameDataFixture();
        var host = new DatraEditorHostService(new DataChangedNotifier());
        await host.InitializeAsync(fixture.Context);

        var kinds = host.DataTypes
            .GroupBy(t => t.RepositoryKind)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.True(kinds.GetValueOrDefault(RepositoryKind.Table) > 0, "expected ≥1 table types");
        Assert.True(kinds.GetValueOrDefault(RepositoryKind.Single) > 0, "expected ≥1 single types");
        Assert.True(kinds.GetValueOrDefault(RepositoryKind.Asset) > 0, "expected ≥1 asset types");
    }

    [Fact]
    public async Task Single_repository_edit_round_trips_to_disk()
    {
        using var fixture = new GameDataFixture();
        var host = new DatraEditorHostService(new DataChangedNotifier());
        await host.InitializeAsync(fixture.Context);

        var dataType = typeof(GameConfigData);
        var source = host.GetDataSource(dataType)!;

        // The single-source backing model: one item, fixed key.
        Assert.IsType<EditableSingleDataSource<GameConfigData>>(source);
        Assert.Equal(1, source.Count);

        var working = ((IEditableDataSource<string, GameConfigData>)source)
            .GetWorkingCopy(EditableSingleDataSource<GameConfigData>.SingleKey);
        working.GameName = "Test Edit";
        working.MaxLevel = 99;
        source.TrackPropertyChange(EditableSingleDataSource<GameConfigData>.SingleKey,
            nameof(GameConfigData.GameName), "Test Edit", out _);
        source.TrackPropertyChange(EditableSingleDataSource<GameConfigData>.SingleKey,
            nameof(GameConfigData.MaxLevel), 99, out _);

        Assert.True(host.HasUnsavedChanges(dataType));
        var saved = await host.SaveAsync(dataType);
        Assert.True(saved);
        Assert.False(host.HasUnsavedChanges(dataType));

        var json = fixture.ReadFile("GameConfig.json");
        Assert.Contains("Test Edit", json);
        Assert.Contains("99", json);
    }

    [Fact]
    public async Task Asset_repository_surfaces_loaded_assets()
    {
        using var fixture = new GameDataFixture();
        var host = new DatraEditorHostService(new DataChangedNotifier());
        await host.InitializeAsync(fixture.Context);

        var dataType = typeof(ScriptAssetData);
        var source = host.GetDataSource(dataType)!;
        Assert.IsType<EditableAssetDataSource<ScriptAssetData>>(source);
        var assetSource = Assert.IsAssignableFrom<IEditableAssetDataSource>(source);
        Assert.Equal(typeof(ScriptAssetData), assetSource.AssetDataType);
        Assert.True(source.Count >= 1, "expected scripts folder to have at least one asset");

        var asset = source.EnumerateItems().FirstOrDefault();
        Assert.NotNull(asset);
        Assert.IsType<Asset<ScriptAssetData>>(asset);
        Assert.IsType<ScriptAssetData>(assetSource.GetAssetData(asset));
        Assert.NotEqual(AssetId.Empty, assetSource.GetAssetId(asset));
        Assert.False(string.IsNullOrWhiteSpace(assetSource.GetAssetFilePath(asset)));
    }

    [Fact]
    public async Task Asset_repository_edit_marks_dirty_and_save_succeeds()
    {
        // NOTE: This stops at host.SaveAsync returning true. Verifying that the modified bytes
        // hit the underlying file additionally exercises Datra core's EditableAssetRepository
        // save chain — that's covered by Datra.Editor.Tests, not the WebEditor layer.
        using var fixture = new GameDataFixture();
        var host = new DatraEditorHostService(new DataChangedNotifier());
        await host.InitializeAsync(fixture.Context);

        var dataType = typeof(ScriptAssetData);
        var source = host.GetDataSource(dataType)!;
        var typed = (IEditableDataSource<AssetId, Asset<ScriptAssetData>>)source;

        var firstId = typed.EnumerateItems().First().Key;
        var working = typed.GetWorkingCopy(firstId);
        working.Data.Name = "renamed-by-test";
        typed.MarkModified(firstId);

        Assert.True(host.HasUnsavedChanges(dataType));

        var saved = await host.SaveAsync(dataType);
        Assert.True(saved);
        Assert.False(host.HasUnsavedChanges(dataType));
    }
}
