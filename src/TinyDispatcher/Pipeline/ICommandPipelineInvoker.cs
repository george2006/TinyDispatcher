using System.Threading;
using System.Threading.Tasks;

namespace TinyDispatcher.Pipeline;

public interface ICommandPipelineInvoker<TCommand, TContext> where TCommand : ICommand
{
    ValueTask ExecuteAsync(TCommand command, TContext context, ICommandHandler<TCommand, TContext> handler, CancellationToken ct = default);
}
