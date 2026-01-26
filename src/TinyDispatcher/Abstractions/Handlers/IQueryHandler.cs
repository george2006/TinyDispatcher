using System.Threading;
using System.Threading.Tasks;

namespace TinyDispatcher;

// Queries are context-less (for now)
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
