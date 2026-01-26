using System.Threading;
using System.Threading.Tasks;

namespace TinyDispatcher.Dispatching;

public interface IDispatcher<TContext>
{
    Task DispatchAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand;

    Task<TResult> DispatchAsync<TQuery, TResult>(TQuery query, CancellationToken ct = default)
        where TQuery : IQuery<TResult>;
}
