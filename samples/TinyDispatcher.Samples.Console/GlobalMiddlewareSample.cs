using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Dispatching;

namespace TinyDispatcher.Samples;

public static class GlobalMiddlewareSample
{
    public static async Task Run()
    {
        var services = new ServiceCollection();

        services.UseTinyDispatcher<AppContext>(tiny =>
        {
            // Applies to ALL commands
            tiny.UseGlobalMiddleware(typeof(GlobalLoggingMiddleware<,>));
        });


        // 👉 ONLY middleware must be registered manually
        services.AddTransient(typeof(GlobalLoggingMiddleware<,>));


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
        public async Task InvokeAsync(
            TCommand command,
            TContext ctx,
            CommandDelegate<TCommand, TContext> next,
            CancellationToken ct)
        {
            Console.WriteLine($"[GlobalLogging] -> {typeof(TCommand).Name}");

            await next(command, ctx, ct);

            Console.WriteLine($"[GlobalLogging] <- {typeof(TCommand).Name}");
        }
    }
}
