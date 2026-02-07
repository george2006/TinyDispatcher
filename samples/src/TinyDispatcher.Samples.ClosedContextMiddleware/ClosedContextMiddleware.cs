// Program.cs
#nullable enable

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;

namespace TinyDispatcher.Samples.ClosedContextMiddleware;

public static class ClosedContextMiddlewareSample
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();

        // Handler
        services.AddTransient<ICommandHandler<Ping, AppContext>, PingHandler>();

        // Middlewares (open generic arity-1)
        services.AddTransient(typeof(RequestIdMiddleware<>));
        services.AddTransient(typeof(TimingMiddleware<>));

        services.UseTinyDispatcher<AppContext>(tiny =>
        {
            // Plug a feature into AppContext
            tiny.AddFeatureInitializer<RequestIdFeatureInitializer>();

            // Register closed-context middleware types (arity-1)
            tiny.UseGlobalMiddleware(typeof(RequestIdMiddleware<>));
            tiny.UseGlobalMiddleware(typeof(TimingMiddleware<>));
        });

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<AppContext>>();

        await dispatcher.DispatchAsync(
            new Ping("hello from closed-context middleware sample")
        ).ConfigureAwait(false);
    }
}

// ============================================================
// 1) Feature (stored in AppContext FeatureCollection)
// ============================================================

public sealed record RequestIdFeature(string Value);

public sealed class RequestIdFeatureInitializer : IFeatureInitializer
{
    public void Initialize(IFeatureCollection features)
    {
        var rid = $"rid_{Guid.NewGuid():N}";
        features.Add(new RequestIdFeature(rid));
    }
}

// ============================================================
// 2) Closed-context middlewares (generic ONLY on TCommand)
//    - Context is fixed to AppContext
// ============================================================

public sealed class RequestIdMiddleware<TCommand> : ICommandMiddleware<TCommand, AppContext>
    where TCommand : ICommand
{
    public async Task InvokeAsync(
        TCommand command,
        AppContext ctx,
        CommandDelegate<TCommand, AppContext> next,
        CancellationToken ct)
    {
        var rid = ctx.GetFeature<RequestIdFeature>().Value;

        Console.WriteLine($"[RequestId MW] -> {typeof(TCommand).Name} (rid={rid})");
        await next(command, ctx, ct).ConfigureAwait(false);
        Console.WriteLine($"[RequestId MW] <- {typeof(TCommand).Name} (rid={rid})");
    }
}

public sealed class TimingMiddleware<TCommand> : ICommandMiddleware<TCommand, AppContext>
    where TCommand : ICommand
{
    public async Task InvokeAsync(
        TCommand command,
        AppContext ctx,
        CommandDelegate<TCommand, AppContext> next,
        CancellationToken ct)
    {
        var rid = ctx.GetFeature<RequestIdFeature>().Value;

        var sw = Stopwatch.StartNew();
        try
        {
            await next(command, ctx, ct).ConfigureAwait(false);
        }
        finally
        {
            sw.Stop();
            Console.WriteLine(
                $"[Timing MW] {typeof(TCommand).Name} took {sw.ElapsedMilliseconds}ms (rid={rid})"
            );
        }
    }
}

// ============================================================
// 3) Command + Handler (AppContext)
// ============================================================

public sealed record Ping(string Message) : ICommand;

public sealed class PingHandler : ICommandHandler<Ping, AppContext>
{
    public Task HandleAsync(Ping command, AppContext ctx, CancellationToken ct = default)
    {
        var rid = ctx.GetFeature<RequestIdFeature>().Value;
        Console.WriteLine($"[Handler] {command.Message} (rid={rid})");
        return Task.CompletedTask;
    }
}
