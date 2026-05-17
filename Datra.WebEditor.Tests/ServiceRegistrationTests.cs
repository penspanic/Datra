#nullable enable
using System.Threading.Tasks;
using Datra.SampleData2.Generated;
using Datra.SampleData2.Models;
using Datra.Serializers;
using Datra.WebEditor.Extensions;
using Datra.WebEditor.Handlers;
using Datra.WebEditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Datra.WebEditor.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public async Task AddDatraWebEditor_wires_full_object_graph()
    {
        using var shop = new ShopFixture();

        var services = new ServiceCollection();
        services.AddSingleton(shop.Provider);
        services.AddSingleton<DataSerializerFactory>();
        services.AddSingleton(sp =>
            new ShopContext(shop.Provider, sp.GetRequiredService<DataSerializerFactory>()));

        services.AddDatraWebEditor(opt => opt.DataContextType = typeof(ShopContext));

        await using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<BlazorFieldTypeRegistry>());
        Assert.NotNull(sp.GetService<IDataChangedNotifier>());
        Assert.NotNull(sp.GetService<DatraEditorHostService>());

        var bootstrapper = sp.GetRequiredService<DatraEditorBootstrapper>();
        await bootstrapper.EnsureInitialisedAsync();

        var host = sp.GetRequiredService<DatraEditorHostService>();
        Assert.Single(host.DataTypes);
        Assert.Equal(typeof(ShopItemData), host.DataTypes[0].DataType);
    }

    [Fact]
    public void AddDatraWebEditor_rejects_missing_DataContextType()
    {
        var services = new ServiceCollection();
        Assert.Throws<System.InvalidOperationException>(() =>
            services.AddDatraWebEditor(_ => { }));
    }

    [Fact]
    public void AddDatraWebEditor_rejects_non_IDataContext_type()
    {
        var services = new ServiceCollection();
        Assert.Throws<System.InvalidOperationException>(() =>
            services.AddDatraWebEditor(opt => opt.DataContextType = typeof(string)));
    }

    [Fact]
    public async Task EnsureInitialisedAsync_is_idempotent()
    {
        using var shop = new ShopFixture();

        var services = new ServiceCollection();
        services.AddSingleton<DataSerializerFactory>();
        services.AddSingleton(sp =>
            new ShopContext(shop.Provider, sp.GetRequiredService<DataSerializerFactory>()));
        services.AddDatraWebEditor(opt => opt.DataContextType = typeof(ShopContext));

        await using var sp = services.BuildServiceProvider();
        var bootstrapper = sp.GetRequiredService<DatraEditorBootstrapper>();

        await Task.WhenAll(
            bootstrapper.EnsureInitialisedAsync(),
            bootstrapper.EnsureInitialisedAsync(),
            bootstrapper.EnsureInitialisedAsync());

        var host = sp.GetRequiredService<DatraEditorHostService>();
        Assert.Single(host.DataTypes);
    }
}
