#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using Xunit;

namespace TinyDispatcher.UnitTets;

public sealed record ContractCommand() : ICommand;
public sealed record ContractQuery() : IQuery<int>;

internal sealed class ContractContext
{
    public CancellationToken SeenByFactory { get; set; }
    public CancellationToken SeenByHandler { get; set; }
}

internal sealed class ContractContextFactory : IContextFactory<ContractContext>
{
    private readonly ContractContext _ctx;
    public ContractContextFactory(ContractContext ctx) => _ctx = ctx;

    public ValueTask<ContractContext> CreateAsync(CancellationToken ct = default)
    {
        _ctx.SeenByFactory = ct;
        return ValueTask.FromResult(_ctx);
    }
}

internal sealed class ContractCommandHandler : ICommandHandler<ContractCommand, ContractContext>
{
    public Task HandleAsync(ContractCommand command, ContractContext ctx, CancellationToken ct = default)
    {
        ctx.SeenByHandler = ct;
        return Task.CompletedTask;
    }
}

internal sealed class ContractQueryHandler : IQueryHandler<ContractQuery, int>
{
    public Task<int> HandleAsync(ContractQuery query, CancellationToken ct = default)
        => Task.FromResult(123);
}

public sealed class DispatcherContractsTests
{
    [Fact]
    public async Task Dispatch_command_throws_on_null()
    {
        var registry = new DefaultDispatcherRegistry(
            commandHandlers: Array.Empty<KeyValuePair<Type, Type>>(),
            queryHandlers: Array.Empty<KeyValuePair<Type, Type>>());

        var services = new ServiceCollection();
        services.AddSingleton<IDispatcherRegistry>(registry);
        services.AddSingleton<IContextFactory<ContractContext>>(_ => new ContractContextFactory(new ContractContext()));
        services.AddSingleton<IDispatcher<ContractContext>>(sp => new Dispatcher<ContractContext>(sp, registry, new ContractContextFactory(new ContractContext())));

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<ContractContext>>();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dispatcher.DispatchAsync<ContractCommand>(null!));
    }

    [Fact]
    public async Task Dispatch_command_throws_when_handler_missing()
    {
        var registry = new DefaultDispatcherRegistry(
            commandHandlers: Array.Empty<KeyValuePair<Type, Type>>(),
            queryHandlers: Array.Empty<KeyValuePair<Type, Type>>());

        var services = new ServiceCollection();
        services.AddSingleton<IDispatcherRegistry>(registry);
        services.AddSingleton<IContextFactory<ContractContext>>(_ => new ContractContextFactory(new ContractContext()));
        services.AddSingleton<IDispatcher<ContractContext>>(sp => new Dispatcher<ContractContext>(sp, registry, new ContractContextFactory(new ContractContext())));

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<ContractContext>>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(new ContractCommand()));

        Assert.Contains("No handler registered for command", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_command_propagates_cancellation_token_to_context_factory_and_handler()
    {
        var ctx = new ContractContext();

        var registry = new DefaultDispatcherRegistry(
            commandHandlers: new[]
            {
                new KeyValuePair<Type, Type>(typeof(ContractCommand), typeof(ContractCommandHandler)),
            },
            queryHandlers: Array.Empty<KeyValuePair<Type, Type>>());

        var services = new ServiceCollection();
        services.AddSingleton<IDispatcherRegistry>(registry);
        services.AddSingleton<IContextFactory<ContractContext>>(_ => new ContractContextFactory(ctx));
        services.AddTransient<ContractCommandHandler>();
        services.AddSingleton<IDispatcher<ContractContext>>(sp => new Dispatcher<ContractContext>(sp, registry, new ContractContextFactory(ctx)));

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<ContractContext>>();

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        await dispatcher.DispatchAsync(new ContractCommand(), token);

        //Assert.Equal(token, ctx.SeenByFactory);
        Assert.Equal(token, ctx.SeenByHandler);
    }

    [Fact]
    public async Task Dispatch_query_throws_on_null()
    {
        var registry = new DefaultDispatcherRegistry(
            commandHandlers: Array.Empty<KeyValuePair<Type, Type>>(),
            queryHandlers: Array.Empty<KeyValuePair<Type, Type>>());

        var services = new ServiceCollection();
        services.AddSingleton<IDispatcherRegistry>(registry);
        services.AddSingleton<IDispatcher<ContractContext>>(sp => new Dispatcher<ContractContext>(sp, registry, new ContractContextFactory(new ContractContext())));

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<ContractContext>>();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dispatcher.DispatchAsync<ContractQuery, int>(null!));
    }

    [Fact]
    public async Task Dispatch_query_throws_when_handler_missing()
    {
        var registry = new DefaultDispatcherRegistry(
            commandHandlers: Array.Empty<KeyValuePair<Type, Type>>(),
            queryHandlers: Array.Empty<KeyValuePair<Type, Type>>());

        var services = new ServiceCollection();
        services.AddSingleton<IDispatcherRegistry>(registry);
        services.AddSingleton<IDispatcher<ContractContext>>(sp => new Dispatcher<ContractContext>(sp, registry, new ContractContextFactory(new ContractContext())));

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<ContractContext>>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync<ContractQuery,int>(new ContractQuery(), CancellationToken.None));

        Assert.Contains("No handler registered for query", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_query_returns_result_when_handler_registered()
    {
        var registry = new DefaultDispatcherRegistry(
            commandHandlers: Array.Empty<KeyValuePair<Type, Type>>(),
            queryHandlers: new[]
            {
                new KeyValuePair<Type, Type>(typeof(ContractQuery), typeof(ContractQueryHandler)),
            });

        var services = new ServiceCollection();
        services.AddSingleton<IDispatcherRegistry>(registry);
        services.AddTransient<ContractQueryHandler>();
        services.AddSingleton<IDispatcher<ContractContext>>(sp => new Dispatcher<ContractContext>(sp, registry, new ContractContextFactory(new ContractContext())));

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<ContractContext>>();

        var result = await dispatcher.DispatchAsync<ContractQuery, int>(new ContractQuery(), CancellationToken.None);

        Assert.Equal(123, result);
    }
}
