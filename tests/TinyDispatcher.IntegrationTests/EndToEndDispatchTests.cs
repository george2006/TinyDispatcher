using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Internal;
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

    private sealed class Handler : ICommandHandler<CreateThing, TestContext>
    {
        public static int Calls;
        public Task HandleAsync(CreateThing command, TestContext ctx, CancellationToken ct = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class Contribution : IMapContribution
    {
        public IEnumerable<KeyValuePair<Type, Type>> CommandHandlers
            => new[] { new KeyValuePair<Type, Type>(typeof(CreateThing), typeof(Handler)) };

        public IEnumerable<KeyValuePair<Type, Type>> QueryHandlers
            => Array.Empty<KeyValuePair<Type, Type>>();
    }

    [Fact]
    public async Task UseTinyDispatcher_registers_dispatcher_and_dispatches_command()
    {
        // arrange
        Handler.Calls = 0;
        DispatcherBootstrap.AddContribution(new Contribution());

        var services = new ServiceCollection();
        services.AddScoped<IContextFactory<TestContext>, ContextFactory>();
        services.AddScoped<Handler>();

        services.UseTinyDispatcher<TestContext>(tiny => { /* no middleware */ });

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();

        // act
        await dispatcher.DispatchAsync(new CreateThing("x"));

        // assert
        Assert.Equal(1, Handler.Calls);
    }
}
