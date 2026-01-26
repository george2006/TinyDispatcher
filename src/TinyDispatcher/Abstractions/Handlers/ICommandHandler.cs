using System.Threading;
using System.Threading.Tasks;

namespace TinyDispatcher;

// Commands are context-aware
public interface ICommandHandler<in TCommand, in TContext>
    where TCommand : ICommand
{
    Task HandleAsync(TCommand command, TContext ctx, CancellationToken ct = default);
}
