using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Pipeline;

namespace TinyDispatcher.Samples.MultiLaneDispatching.NoOp;

public static class NoOpModule
{
    public static IServiceCollection AddNoOpLane(this IServiceCollection services)
    {
        services.AddTransient(typeof(NoOpTraceMiddleware<,>));

        services.UseTinyNoOpContext(tiny =>
        {
            tiny.UseGlobalMiddleware(typeof(NoOpTraceMiddleware<,>));
        });

        return services;
    }
}

public sealed record PingNoOp(string Value) : ICommand;

public sealed class PingNoOpHandler : ICommandHandler<PingNoOp, NoOpContext>
{
    public Task HandleAsync(PingNoOp command, NoOpContext context, CancellationToken ct = default)
    {
        Console.WriteLine($"handler noop {command.Value}");
        return Task.CompletedTask;
    }
}

public sealed class NoOpTraceMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"noop trace -> {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
        Console.WriteLine($"noop trace <- {typeof(TCommand).Name}");
    }
}
