using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using Xunit;

namespace TinyDispatcher.IntegrationTests;

public sealed class EndToEndDispatchTests
{
    public sealed record CreateThing(string Name) : ICommand;

    public sealed class TestContext { public Guid RequestId { get; } = Guid.NewGuid(); }

    public sealed class ContextFactory : IContextFactory<TestContext>
    {
        public ValueTask<TestContext> CreateAsync(CancellationToken ct = default)
            => new(new TestContext());
    }

    public sealed class HandlerWithContext : ICommandHandler<CreateThing, TestContext>
    {
        public int Calls;
        public Task HandleAsync(CreateThing command, TestContext ctx, CancellationToken ct = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    public sealed class HandlerNoOp : ICommandHandler<CreateThing, NoOpContext>
    {
        public int Calls;
        public Task HandleAsync(CreateThing command, NoOpContext ctx, CancellationToken ct = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task UseTinyDispatcher_registers_dispatcher_and_dispatches_command_with_context()
    {
        var c = new HandlerWithContext { Calls = 0 };

        var services = new ServiceCollection();
        services.AddScoped<IContextFactory<TestContext>, ContextFactory>();
        services.AddScoped<ICommandHandler<CreateThing, TestContext>>(_ => c);

        services.UseTinyDispatcher<TestContext>(tiny => { });

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();

        await dispatcher.DispatchAsync(new CreateThing("x"));

        Assert.Equal(1, c.Calls);
    }

    [Fact]
    public async Task UseTinyNoOpContext_registers_dispatcher_and_dispatches_command_without_context()
    {
        var c = new HandlerNoOp { Calls = 0 };

        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<CreateThing, NoOpContext>>(_ => c);

        services.UseTinyNoOpContext(tiny => { });

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<NoOpContext>>();

        await dispatcher.DispatchAsync(new CreateThing("x"));

        Assert.Equal(1, c.Calls);
    }
}