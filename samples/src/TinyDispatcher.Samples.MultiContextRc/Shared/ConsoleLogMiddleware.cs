using TinyDispatcher;
using TinyDispatcher.Pipeline;

namespace TinyDispatcher.Samples.MultiContextRc.Shared;

public sealed class ConsoleLogMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"global {typeof(TContext).Name} -> {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
        Console.WriteLine($"global {typeof(TContext).Name} <- {typeof(TCommand).Name}");
    }
}
