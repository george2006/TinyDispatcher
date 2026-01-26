using System.Threading;
using System.Threading.Tasks;

namespace TinyDispatcher.Pipeline;

public interface ICommandPipelineInvoker<TCommand, TContext>
    where TCommand : ICommand
{
    Task ExecuteAsync(
        TCommand command,
        TContext ctx,
        ICommandHandler<TCommand, TContext> handler,
        CancellationToken ct = default);
}
