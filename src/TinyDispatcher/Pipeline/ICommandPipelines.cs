using TinyDispatcher.Pipeline;

namespace TinyDispatcher;

public interface ICommandPipeline<TCommand, TContext> where TCommand : ICommand
{
    ValueTask ExecuteAsync(TCommand command, TContext context, ICommandHandler<TCommand, TContext> handler, CancellationToken ct = default);
}