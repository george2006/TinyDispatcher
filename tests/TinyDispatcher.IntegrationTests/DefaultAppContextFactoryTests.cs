#nullable enable

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using Xunit;

namespace TinyDispatcher.IntegrationTests;

public sealed class DefaultAppContextFactoryTests
{
    private sealed class InitA : IFeatureInitializer
    {
        public static int Calls;
        public void Initialize(IFeatureCollection features) => Calls++;
    }

    private sealed class InitB : IFeatureInitializer
    {
        public static int Calls;
        public void Initialize(IFeatureCollection features) => Calls++;
    }

    [Fact]
    public async Task DefaultAppContextFactory_invokes_all_feature_initializers()
    {
        InitA.Calls = 0;
        InitB.Calls = 0;

        var services = new ServiceCollection();
        services.AddScoped<IFeatureInitializer, InitA>();
        services.AddScoped<IFeatureInitializer, InitB>();
        services.AddScoped<IContextFactory<AppContext>, DefaultAppContextFactory>();

        using var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<IContextFactory<AppContext>>();
        _ = await factory.CreateAsync();

        Assert.Equal(1, InitA.Calls);
        Assert.Equal(1, InitB.Calls);
    }
}
