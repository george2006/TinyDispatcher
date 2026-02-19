using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;

namespace TinyDispatcher.Samples.Pipelines;

public static class GlobalMiddlewareSample
{
    public static async Task Run()
    {
        var services = new ServiceCollection();

        services.AddTiny();
       
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<AppContext>>();

        Console.WriteLine("=== Global middleware sample ===");

        await dispatcher.DispatchAsync(new Ping("one"), CancellationToken.None);
        await dispatcher.DispatchAsync(new Pong("two"), CancellationToken.None);

        Console.WriteLine("================================");
    }

    // -----------------------
    // Commands
    // -----------------------

    public sealed record Ping(string Value) : ICommand;

    public sealed record Pong(string Value) : ICommand;

    // -----------------------
    // Handlers (AUTO-WIRED by TinyDispatcher)
    // -----------------------

    public sealed class PingHandler : ICommandHandler<Ping, AppContext>
    {
        public Task HandleAsync(Ping command, AppContext ctx, CancellationToken ct = default)
        {
            Console.WriteLine($"[Handler] Ping: {command.Value}");
            return Task.CompletedTask;
        }
    }

    public sealed class PongHandler : ICommandHandler<Pong, AppContext>
    {
        public Task HandleAsync(Pong command, AppContext ctx, CancellationToken ct = default)
        {
            Console.WriteLine($"[Handler] Pong: {command.Value}");
            return Task.CompletedTask;
        }
    }

    // -----------------------
    // Global middleware
    // -----------------------

    public sealed class GlobalLoggingMiddleware<TCommand, TContext>
        : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async ValueTask InvokeAsync(
            TCommand command,
            TContext ctx,
            ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct = default)
        {
            Console.WriteLine($"[GlobalLogging] -> {typeof(TCommand).Name}");

            await runtime.NextAsync(command, ctx, ct).ConfigureAwait(false);

            Console.WriteLine($"[GlobalLogging] <- {typeof(TCommand).Name}");
        }
    }
}
