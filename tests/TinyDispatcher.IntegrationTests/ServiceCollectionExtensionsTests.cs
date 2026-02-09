#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;
using TinyDispatcher.Context;
using Xunit;

namespace TinyDispatcher.IntegrationTests;

public sealed class ServiceCollectionExtensionsTests
{
    private sealed class MyContext { }

    private sealed class MyContextFactory : IContextFactory<MyContext>
    {
        public ValueTask<MyContext> CreateAsync(CancellationToken ct = default)
            => ValueTask.FromResult(new MyContext());
    }

    [Fact]
    public void UseTinyDispatcher_with_custom_context_and_no_factory_throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.UseTinyDispatcher<MyContext>(_ => { }, contextFactory: null));

        Assert.Contains("context factory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UseTinyDispatcher_auto_registers_DefaultAppContextFactory_for_AppContext()
    {
        var services = new ServiceCollection();

        services.UseTinyDispatcher<AppContext>(_ => { }, contextFactory: null);

        using var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<IContextFactory<AppContext>>();
        var ctx = await factory.CreateAsync();

        Assert.NotNull(ctx);
    }

    [Fact]
    public async Task AddDispatcher_delegate_contextFactory_replaces_existing_registration()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IContextFactory<MyContext>, MyContextFactory>();

        var called = 0;

        services.AddDispatcher<MyContext>((sp, ct) =>
        {
            called++;
            return ValueTask.FromResult(new MyContext());
        });

        using var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<IContextFactory<MyContext>>();
        _ = await factory.CreateAsync();

        Assert.Equal(1, called);
    }
}
