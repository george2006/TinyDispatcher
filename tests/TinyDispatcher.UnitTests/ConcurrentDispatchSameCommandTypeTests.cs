#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;
using Xunit;

namespace TinyDispatcher.UnitTests;

public sealed class ConcurrentDispatchSameCommandTypeTests
{
    public sealed record Ping(int Id) : ICommand;

    [Fact]
    public async Task Concurrent_dispatch_same_command_type_within_same_scope_is_thread_safe()
    {
        // Arrange
        var services = new ServiceCollection();

        // Register handler
        services.AddTransient<ICommandHandler<Ping, TestContext>, PingHandler>();

        // Register probe state as singleton so both concurrent calls share the same recorder (for assertions)
        services.AddSingleton<ProbeState>();
        services.AddScoped<IContextFactory<TestContext>, TestContextFactory>();
        services.AddTransient<ICommandHandler<TestCommand, TestContext>, TestHandler>();
        TinyDispatcher.Generated.ThisAssemblyPipelineContribution.Add(services);

        // Dispatcher
        services.AddScoped<IDispatcher<TestContext>>(sp =>
            new Dispatcher<TestContext>(sp, sp.GetRequiredService<IContextFactory<TestContext>>()));

        var sp = services.BuildServiceProvider(validateScopes: true);

        // Create ONE scope (simulating a single request scope)
        using var scope = sp.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher<TestContext>>();
        var state = scope.ServiceProvider.GetRequiredService<ProbeState>();

        // Act: run TWO concurrent dispatches of the SAME command type in the SAME scope
        var t1 = dispatcher.DispatchAsync(new Ping(1));
        var t2 = dispatcher.DispatchAsync(new Ping(2));

        await Task.WhenAll(t1, t2);

        // Assert:
        // Each command must have observed the full pipeline (M0 -> M1 -> Handler) exactly once.
        // And the order must be correct per command id.
        Assert.True(state.TryGetTrace(1, out var trace1), "Missing trace for command 1.");
        Assert.True(state.TryGetTrace(2, out var trace2), "Missing trace for command 2.");

        Assert.Equal("M0-Pre,M1-Pre,Handler,M1-Post,M0-Post", trace1);
        Assert.Equal("M0-Pre,M1-Pre,Handler,M1-Post,M0-Post", trace2);

        // Also ensure handler count is exactly 2
        Assert.Equal(2, state.HandlerHits);
    }

    /// <summary>
    /// Shared recorder across the two concurrent executions.
    /// Stores per-command ordered events.
    /// </summary>
    public sealed class ProbeState
    {
        private readonly ConcurrentDictionary<int, ConcurrentQueue<string>> _events = new();
        private int _handlerHits;

        public int HandlerHits => Volatile.Read(ref _handlerHits);

        public void Record(int id, string evt)
        {
            var q = _events.GetOrAdd(id, _ => new ConcurrentQueue<string>());
            q.Enqueue(evt);
        }

        public void HitHandler() => Interlocked.Increment(ref _handlerHits);

        public bool TryGetTrace(int id, out string trace)
        {
            trace = string.Empty;

            if (!_events.TryGetValue(id, out var q))
                return false;

            trace = string.Join(",", q.ToArray());
            return true;
        }
    }

    public sealed class PingHandler : ICommandHandler<Ping, TestContext>
    {
        private readonly ProbeState _state;

        public PingHandler(ProbeState state) => _state = state;

        public Task HandleAsync(Ping command, TestContext context, CancellationToken cancellationToken = default)
        {
            _state.Record(command.Id, "Handler");
            _state.HitHandler();
            return Task.CompletedTask;
        }
    }

    // Middleware 0: forces an async boundary to increase likelihood of interleaving
    public sealed class ProbeMiddleware0<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        private readonly ProbeState _state;

        public ProbeMiddleware0(ProbeState state) => _state = state;

        public async ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
        {
            if (command is Ping p)
                _state.Record(p.Id, "M0-Pre");

            // Force interleaving
            await Task.Yield();

            await runtime.NextAsync(command, context, ct).ConfigureAwait(false);

            if (command is Ping p2)
                _state.Record(p2.Id, "M0-Post");
        }
    }

    // Middleware 1: forces another async boundary
    public sealed class ProbeMiddleware1<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        private readonly ProbeState _state;

        public ProbeMiddleware1(ProbeState state) => _state = state;

        public async ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
        {
            if (command is Ping p)
                _state.Record(p.Id, "M1-Pre");

            await Task.Yield();

            await runtime.NextAsync(command, context, ct).ConfigureAwait(false);

            if (command is Ping p2)
                _state.Record(p2.Id, "M1-Post");
        }
    }
}