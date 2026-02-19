using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Pipeline;

namespace TinyDispatcher.Samples.Aspnet;

// ============================================================
// Tiny sample composition (registrations + types)
// ============================================================

public static class TinySample
{
    // Entry point: register everything the sample needs
    public static IServiceCollection AddTinySample(this IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // Needed by the feature initializer (reads current request headers)
        services.AddHttpContextAccessor();

        // Middlewares (open generic; context is AppContext via ICommandMiddleware<TCommand, AppContext>)
        services.AddTransient(typeof(RequestIdMiddleware<>));
        services.AddTransient(typeof(TimingMiddleware<>));

        // Tiny dispatcher + pipeline declaration (source-gen hook)
        services.UseTinyDispatcher<AppContext>(tiny =>
        {
            // Plug request feature into AppContext
            tiny.AddFeatureInitializer<RequestIdFromHttpFeatureInitializer>();

            // Global middleware registrations
            tiny.UseGlobalMiddleware(typeof(RequestIdMiddleware<>));
            tiny.UseGlobalMiddleware(typeof(TimingMiddleware<>));
        });

        return services;
    }
}

// ============================================================
// Request model for Minimal API (keep it here or in Program.cs)
// ============================================================

public sealed record PingRequest(string? Message);

// ============================================================
// Feature (stored in AppContext FeatureCollection)
// ============================================================

public sealed record RequestIdFeature(string Value);

public sealed class RequestIdFromHttpFeatureInitializer : IFeatureInitializer
{
    private readonly IHttpContextAccessor _http;

    public RequestIdFromHttpFeatureInitializer(IHttpContextAccessor http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public void Initialize(IFeatureCollection features)
    {
        var http = _http.HttpContext;

        string rid;
        if (http is null)
        {
            rid = $"rid_{Guid.NewGuid():N}";
        }
        else if (http.Request.Headers.TryGetValue("X-Request-Id", out var values) &&
                 !string.IsNullOrWhiteSpace(values.ToString()))
        {
            rid = values.ToString();
        }
        else
        {
            rid = $"rid_{Guid.NewGuid():N}";
        }

        features.Add(new RequestIdFeature(rid));
    }
}

// ============================================================
// Middlewares (runtime-based signature)
// ============================================================

public sealed class RequestIdMiddleware<TCommand> : ICommandMiddleware<TCommand, AppContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        AppContext ctx,
        ICommandPipelineRuntime<TCommand, AppContext> runtime,
        CancellationToken ct = default)
    {
        var rid = ctx.GetFeature<RequestIdFeature>().Value;

        Console.WriteLine($"[RequestId MW] -> {typeof(TCommand).Name} (rid={rid})");
        await runtime.NextAsync(command, ctx, ct).ConfigureAwait(false);
        Console.WriteLine($"[RequestId MW] <- {typeof(TCommand).Name} (rid={rid})");
    }
}

public sealed class TimingMiddleware<TCommand> : ICommandMiddleware<TCommand, AppContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        AppContext ctx,
        ICommandPipelineRuntime<TCommand, AppContext> runtime,
        CancellationToken ct = default)
    {
        var rid = ctx.GetFeature<RequestIdFeature>().Value;

        var sw = Stopwatch.StartNew();
        try
        {
            await runtime.NextAsync(command, ctx, ct).ConfigureAwait(false);
        }
        finally
        {
            sw.Stop();
            Console.WriteLine($"[Timing MW] {typeof(TCommand).Name} took {sw.ElapsedMilliseconds}ms (rid={rid})");
        }
    }
}

// ============================================================
// Command + handler
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
