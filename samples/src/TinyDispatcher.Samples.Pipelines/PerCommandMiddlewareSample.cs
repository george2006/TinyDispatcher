using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;
using static TinyDispatcher.Samples.Pipelines.GlobalMiddlewareSample;

namespace TinyDispatcher.Samples.Pipelines;

public static class PerCommandMiddlewareSample
{
    public static async Task Run()
    {
        var services = new ServiceCollection();

        services.AddTiny();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<AppContext>>();

        Console.WriteLine("=== Per-command middleware sample ===");

        Console.WriteLine("-> Dispatch Pay (middleware SHOULD run)");
        await dispatcher.DispatchAsync(new Pay(42), CancellationToken.None);

        Console.WriteLine();

        Console.WriteLine("-> Dispatch Refund (middleware should NOT run)");
        await dispatcher.DispatchAsync(new Refund(7), CancellationToken.None);

        Console.WriteLine("=====================================");
    }

    // -----------------------
    // Commands
    // -----------------------

    public sealed record Pay(int Amount) : ICommand;

    public sealed record Refund(int Amount) : ICommand;

    // -----------------------
    // Handlers (auto-wired by source generator)
    // -----------------------

    public sealed class PayHandler : ICommandHandler<Pay, AppContext>
    {
        public Task HandleAsync(Pay command, AppContext ctx, CancellationToken ct = default)
        {
            Console.WriteLine($"[Handler] Pay: {command.Amount}");
            return Task.CompletedTask;
        }
    }

    public sealed class RefundHandler : ICommandHandler<Refund, AppContext>
    {
        public Task HandleAsync(Refund command, AppContext ctx, CancellationToken ct = default)
        {
            Console.WriteLine($"[Handler] Refund: {command.Amount}");
            return Task.CompletedTask;
        }
    }

    // -----------------------
    // Middleware (ONLY for Pay)
    // -----------------------

    public sealed class OnlyForPayMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async ValueTask InvokeAsync(
            TCommand command,
            TContext ctx,
            ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct = default)
        {
            Console.WriteLine($"[OnlyForPayMiddleware] -> {typeof(TCommand).Name}");

            await runtime.NextAsync(command, ctx, ct).ConfigureAwait(false);

            Console.WriteLine($"[OnlyForPayMiddleware] <- {typeof(TCommand).Name}");
        }
    }
}
