#nullable enable

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.UnitTests;
using Xunit;
using static TinyDispatcher.UnitTets.PipelineSelectionTests;

namespace TinyDispatcher.UnitTets;

public sealed record ContractCommand() : ICommand;
internal sealed class ContractContext
{
    public CancellationToken SeenByFactory { get; set; }
    public CancellationToken SeenByHandler { get; set; }
}

internal sealed class ContractContextFactory : IContextFactory<TestContext>
{
    private readonly TestContext _ctx;
    public ContractContextFactory(TestContext ctx) => _ctx = ctx;

    public ValueTask<TestContext> CreateAsync(CancellationToken ct = default)
    {
        _ctx.SeenByFactory = ct;
        return ValueTask.FromResult(_ctx);
    }
}

public sealed class ContractCommandHandler : ICommandHandler<ContractCommand, TestContext>
{
    public Task HandleAsync(ContractCommand command, TestContext ctx, CancellationToken ct = default)
    {
        ctx.SeenByHandler = ct;
        return Task.CompletedTask;
    }
}

public sealed class DispatcherContractsTests
{
    [Fact]
    public async Task Dispatch_command_propagates_cancellation_token_to_context_factory_and_handler()
    {
        var ctx = new TestContext();
        var services = new ServiceCollection();
        services.AddTransient(typeof(GlobalLogMiddleware<,>));
        services.AddTransient(typeof(PerCommandLogMiddleware<,>));
        services.AddTransient<ICommandHandler<ContractCommand,TestContext>, ContractCommandHandler>();
        services.AddScoped<IContextFactory<TestContext>>(_ => new ContractContextFactory(ctx));
        services.AddScoped<IDispatcher<TestContext>>(sp => new Dispatcher<TestContext>(sp, new ContractContextFactory(ctx)));

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        await dispatcher.DispatchAsync(new ContractCommand(), token);

        Assert.Equal(token, ctx.SeenByFactory);
        Assert.Equal(token, ctx.SeenByHandler);
    }
}
