using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
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

    public sealed class Handler : ICommandHandler<CreateThing, TestContext>
    {
        public int Calls;
        public Task HandleAsync(CreateThing command, TestContext ctx, CancellationToken ct = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task UseTinyDispatcher_registers_dispatcher_and_dispatches_command()
    {
        // arrange
        Handler c = new Handler();
        c.Calls = 0;
        
        var services = new ServiceCollection();
        services.AddScoped<IContextFactory<TestContext>, ContextFactory>();
        services.AddScoped<ICommandHandler<CreateThing,TestContext>>(sp => 
        {
            return c;
        });
        services.UseTinyDispatcher<TestContext>(tiny => { /* no middleware */ });

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();

        // act
        await dispatcher.DispatchAsync(new CreateThing("x"));

        // assert
        Assert.Equal(1, c.Calls);
    }
}
